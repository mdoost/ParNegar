using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Text.Json;

namespace ParNegar.API.Middleware;

/// <summary>
/// Rate limiting middleware specifically for file upload operations
/// Implements sliding window rate limiting based on IP address and user ID
/// </summary>
public class FileUploadRateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<FileUploadRateLimitingMiddleware> _logger;
    private readonly IConfiguration _configuration;

    // Track rate limiting for different endpoints
    private static readonly Dictionary<string, RateLimitConfig> EndpointConfigs = new()
    {
        { "/api/files/upload", new RateLimitConfig { RequestsPerMinute = 10, WindowSizeMinutes = 1 } },
        { "/api/attachments/upload", new RateLimitConfig { RequestsPerMinute = 10, WindowSizeMinutes = 1 } },
        { "/api/documents/upload", new RateLimitConfig { RequestsPerMinute = 10, WindowSizeMinutes = 1 } },
        { "/api/files/retry", new RateLimitConfig { RequestsPerMinute = 5, WindowSizeMinutes = 1 } }
    };

    private static readonly ConcurrentDictionary<string, Queue<DateTimeOffset>> RequestTracker = new();

    public FileUploadRateLimitingMiddleware(
        RequestDelegate next,
        ILogger<FileUploadRateLimitingMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Check if this endpoint needs rate limiting
        var path = context.Request.Path.Value?.ToLowerInvariant();
        if (string.IsNullOrEmpty(path) || !EndpointConfigs.ContainsKey(path))
        {
            await _next(context);
            return;
        }

        var config = EndpointConfigs[path];

        // Try to get custom rate limit from configuration
        var customRateLimit = _configuration.GetValue<int>("FileUpload:RateLimitPerMinute", 0);
        if (customRateLimit > 0)
        {
            config = config with { RequestsPerMinute = customRateLimit };
        }

        // Generate rate limit key based on IP and user
        var rateLimitKey = GenerateRateLimitKey(context, path);

        // Check rate limit
        var isAllowed = IsRequestAllowed(rateLimitKey, config);
        if (!isAllowed)
        {
            _logger.LogWarning("Rate limit exceeded for key: {RateLimitKey}, endpoint: {Path}", rateLimitKey, path);
            await WriteRateLimitExceededResponseAsync(context, config);
            return;
        }

        // Track this request
        TrackRequest(rateLimitKey, config);

        await _next(context);
    }

    private string GenerateRateLimitKey(HttpContext context, string path)
    {
        var ip = GetClientIpAddress(context);
        var userId = GetUserId(context);

        // Combine IP and user ID for more granular rate limiting
        return $"{path}:{ip}:{userId ?? "anonymous"}";
    }

    private string GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded headers first (for load balancers/proxies)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ips = forwardedFor.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (ips.Length > 0)
            {
                return ips[0].Trim();
            }
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp.Trim();
        }

        // Fallback to connection remote IP
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private string? GetUserId(HttpContext context)
    {
        // Try to get user ID from JWT claims
        var userIdClaim = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                       ?? context.User?.FindFirst("sub")
                       ?? context.User?.FindFirst("userId");
        return userIdClaim?.Value;
    }

    private bool IsRequestAllowed(string key, RateLimitConfig config)
    {
        var now = DateTimeOffset.UtcNow;
        var windowStart = now.AddMinutes(-config.WindowSizeMinutes);

        if (!RequestTracker.TryGetValue(key, out var requests))
        {
            return true; // First request for this key
        }

        lock (requests)
        {
            // Remove old requests outside the window
            while (requests.Count > 0 && requests.Peek() < windowStart)
            {
                requests.Dequeue();
            }

            // Check if we're within the limit
            return requests.Count < config.RequestsPerMinute;
        }
    }

    private void TrackRequest(string key, RateLimitConfig config)
    {
        var now = DateTimeOffset.UtcNow;
        var requests = RequestTracker.GetOrAdd(key, _ => new Queue<DateTimeOffset>());

        lock (requests)
        {
            requests.Enqueue(now);

            // Keep only requests within the window to prevent memory leaks
            var windowStart = now.AddMinutes(-config.WindowSizeMinutes);
            while (requests.Count > 0 && requests.Peek() < windowStart)
            {
                requests.Dequeue();
            }

            // Additional cleanup: if queue is empty, remove the key
            if (requests.Count == 0)
            {
                RequestTracker.TryRemove(key, out _);
            }
        }
    }

    private static async Task WriteRateLimitExceededResponseAsync(HttpContext context, RateLimitConfig config)
    {
        context.Response.StatusCode = 429; // Too Many Requests
        context.Response.ContentType = "application/json";

        // Add rate limit headers
        context.Response.Headers.Append("Retry-After", "60");
        context.Response.Headers.Append("X-RateLimit-Limit", config.RequestsPerMinute.ToString());
        context.Response.Headers.Append("X-RateLimit-Window", $"{config.WindowSizeMinutes}m");

        var errorResponse = new
        {
            error = $"Rate limit exceeded. Maximum {config.RequestsPerMinute} requests allowed per {config.WindowSizeMinutes} minute(s).",
            statusCode = 429,
            timestamp = DateTimeOffset.UtcNow,
            path = context.Request.Path.Value,
            retryAfter = "60 seconds"
        };

        var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(jsonResponse);
    }

    private record RateLimitConfig
    {
        public int RequestsPerMinute { get; init; }
        public int WindowSizeMinutes { get; init; }
    }
}

/// <summary>
/// Extension methods for registering the rate limiting middleware
/// </summary>
public static class FileUploadRateLimitingMiddlewareExtensions
{
    public static IApplicationBuilder UseFileUploadRateLimiting(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<FileUploadRateLimitingMiddleware>();
    }
}
