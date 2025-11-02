using System.Diagnostics;

namespace ParNegar.API.Middleware;

/// <summary>
/// Middleware to log detailed HTTP request and response information for debugging and monitoring
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.TraceIdentifier;
        var stopwatch = Stopwatch.StartNew();

        // Log request
        await LogRequest(context, correlationId);

        // Capture response
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{CorrelationId}] Unhandled exception in request pipeline: {ExceptionType} - {Message}",
                correlationId, ex.GetType().Name, ex.Message);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            await LogResponse(context, correlationId, responseBody, stopwatch.ElapsedMilliseconds);

            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBodyStream);
        }
    }

    private async Task LogRequest(HttpContext context, string correlationId)
    {
        var request = context.Request;

        _logger.LogInformation("[{CorrelationId}] HTTP REQUEST: {Method} {Path}{QueryString} from {RemoteIP}",
            correlationId, request.Method, request.Path, request.QueryString,
            context.Connection.RemoteIpAddress?.ToString() ?? "Unknown");

        // Log headers (excluding sensitive ones)
        var headers = request.Headers
            .Where(h => !IsSensitiveHeader(h.Key))
            .ToDictionary(h => h.Key, h => string.Join(", ", h.Value.ToArray()));

        if (headers.Any())
        {
            _logger.LogDebug("[{CorrelationId}] Request Headers: {@Headers}", correlationId, headers);
        }

        // Log body for non-file uploads
        if (request.ContentLength > 0 && request.ContentType != null &&
            !request.ContentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            if (request.ContentLength < 10000) // Only log small payloads
            {
                request.EnableBuffering();
                var body = await new StreamReader(request.Body).ReadToEndAsync();
                request.Body.Position = 0;

                if (!string.IsNullOrEmpty(body))
                {
                    _logger.LogDebug("[{CorrelationId}] Request Body: {RequestBody}", correlationId, body);
                }
            }
            else
            {
                _logger.LogDebug("[{CorrelationId}] Request Body: [Large payload - {Size} bytes]",
                    correlationId, request.ContentLength);
            }
        }
    }

    private async Task LogResponse(HttpContext context, string correlationId, MemoryStream responseBody, long elapsedMs)
    {
        var response = context.Response;

        _logger.LogInformation("[{CorrelationId}] HTTP RESPONSE: {StatusCode} {ReasonPhrase} in {ElapsedMs}ms - Size: {Size} bytes",
            correlationId, response.StatusCode, GetReasonPhrase(response.StatusCode), elapsedMs, responseBody.Length);

        // Log response headers
        var headers = response.Headers
            .Where(h => !IsSensitiveHeader(h.Key))
            .ToDictionary(h => h.Key, h => string.Join(", ", h.Value.ToArray()));

        if (headers.Any())
        {
            _logger.LogDebug("[{CorrelationId}] Response Headers: {@Headers}", correlationId, headers);
        }

        // Log response body for errors or small successful responses
        if (response.StatusCode >= 400 || (responseBody.Length > 0 && responseBody.Length < 5000))
        {
            responseBody.Seek(0, SeekOrigin.Begin);
            var bodyContent = await new StreamReader(responseBody).ReadToEndAsync();

            if (!string.IsNullOrEmpty(bodyContent))
            {
                if (response.StatusCode >= 400)
                {
                    _logger.LogWarning("[{CorrelationId}] Error Response Body: {ResponseBody}", correlationId, bodyContent);
                }
                else
                {
                    _logger.LogDebug("[{CorrelationId}] Response Body: {ResponseBody}", correlationId, bodyContent);
                }
            }
        }

        // Log performance warnings
        if (elapsedMs > 5000)
        {
            _logger.LogWarning("[{CorrelationId}] SLOW REQUEST: Took {ElapsedMs}ms to complete", correlationId, elapsedMs);
        }
    }

    private static bool IsSensitiveHeader(string headerName)
    {
        var sensitiveHeaders = new[] { "authorization", "cookie", "set-cookie", "x-api-key", "x-auth-token" };
        return sensitiveHeaders.Contains(headerName.ToLowerInvariant());
    }

    private static string GetReasonPhrase(int statusCode)
    {
        return statusCode switch
        {
            200 => "OK",
            201 => "Created",
            400 => "Bad Request",
            401 => "Unauthorized",
            403 => "Forbidden",
            404 => "Not Found",
            413 => "Payload Too Large",
            415 => "Unsupported Media Type",
            422 => "Unprocessable Entity",
            429 => "Too Many Requests",
            500 => "Internal Server Error",
            _ => "Unknown"
        };
    }
}

/// <summary>
/// Extension methods for adding request logging middleware
/// </summary>
public static class RequestLoggingMiddlewareExtensions
{
    /// <summary>
    /// Add request logging middleware to the pipeline
    /// </summary>
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}
