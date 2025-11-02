using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text.Json;
using ParNegar.Application.Interfaces.Services.Auth;
using ParNegar.Shared.DTOs.Common;

namespace ParNegar.API.Middleware;

/// <summary>
/// Middleware برای اعتبارسنجی Session و چک کردن Token Blacklist
/// </summary>
public class SessionValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SessionValidationMiddleware> _logger;

    public SessionValidationMiddleware(
        RequestDelegate next,
        ILogger<SessionValidationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITokenService tokenService)
    {
        // فقط درخواست‌هایی که نیاز به احراز هویت دارند را چک کن
        var endpoint = context.GetEndpoint();
        var requiresAuth = endpoint?.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAuthorizeData>() != null;

        if (!requiresAuth)
        {
            await _next(context);
            return;
        }

        // استخراج Authorization header
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        try
        {
            var token = authHeader.Substring("Bearer ".Length).Trim();

            // استخراج session_id از JWT token
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var sessionIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "session_id");

            if (sessionIdClaim == null)
            {
                // توکن session_id ندارد - احتمالاً توکن قدیمی است
                await _next(context);
                return;
            }

            var sessionId = sessionIdClaim.Value;

            // چک کردن blacklist
            var isBlacklisted = await tokenService.IsSessionBlacklistedAsync(sessionId);

            if (isBlacklisted)
            {
                _logger.LogWarning("Blacklisted session detected: {SessionId}", sessionId);

                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                context.Response.ContentType = "application/json";

                var errorResponse = new ErrorResponse
                {
                    StatusCode = 401,
                    Title = "Unauthorized",
                    Message = "Your session has been revoked. Please login again.",
                    TraceId = context.TraceIdentifier,
                    Timestamp = DateTime.UtcNow,
                    Path = context.Request.Path,
                    Method = context.Request.Method
                };

                var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await context.Response.WriteAsync(jsonResponse);
                return;
            }
        }
        catch (Exception ex)
        {
            // در صورت خطا در پردازش توکن، اجازه بده که به مراحل بعدی pipeline برود
            // Authentication middleware خطای مناسب را برمی‌گرداند
            _logger.LogWarning(ex, "Error validating session");
        }

        await _next(context);
    }
}

/// <summary>
/// Extension method for adding middleware
/// </summary>
public static class SessionValidationMiddlewareExtensions
{
    public static IApplicationBuilder UseSessionValidation(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SessionValidationMiddleware>();
    }
}
