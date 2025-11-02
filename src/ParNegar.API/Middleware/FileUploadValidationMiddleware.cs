using System.Text.Json;

namespace ParNegar.API.Middleware;

/// <summary>
/// Middleware for validating file upload requests
/// Provides early validation and security checks before reaching the controller
/// </summary>
public class FileUploadValidationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<FileUploadValidationMiddleware> _logger;
    private readonly IConfiguration _configuration;

    // Configuration for file upload validation
    private static readonly HashSet<string> FileUploadPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/files/upload",
        "/api/attachments/upload",
        "/api/documents/upload"
    };

    public FileUploadValidationMiddleware(
        RequestDelegate next,
        ILogger<FileUploadValidationMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Check if this is a file upload request
        if (IsFileUploadRequest(context))
        {
            var validationResult = await ValidateFileUploadRequestAsync(context);
            if (!validationResult.IsValid)
            {
                await WriteErrorResponseAsync(context, validationResult.StatusCode, validationResult.ErrorMessage);
                return;
            }
        }

        await _next(context);
    }

    private static bool IsFileUploadRequest(HttpContext context)
    {
        return context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) &&
               FileUploadPaths.Contains(context.Request.Path.Value ?? string.Empty) &&
               context.Request.ContentType?.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase) == true;
    }

    private async Task<ValidationResult> ValidateFileUploadRequestAsync(HttpContext context)
    {
        try
        {
            // Check content length against global limits
            var contentLength = context.Request.ContentLength;
            if (contentLength.HasValue)
            {
                // Get max file size from configuration (default: 100MB)
                var maxFileSize = _configuration.GetValue<long>("FileUpload:MaxFileSizeBytes", 104857600); // 100MB

                // Allow some overhead for multipart form data (metadata, boundaries, etc.)
                var maxContentLength = maxFileSize * 2; // Double the file size limit for total content

                if (contentLength.Value > maxContentLength)
                {
                    _logger.LogWarning("Request content length {ContentLength} exceeds maximum allowed {MaxLength}",
                        contentLength.Value, maxContentLength);

                    return ValidationResult.Invalid(413,
                        $"Request size ({FormatBytes(contentLength.Value)}) exceeds maximum allowed ({FormatBytes(maxContentLength)})");
                }
            }

            // Validate Content-Type header
            var contentType = context.Request.ContentType;
            if (string.IsNullOrEmpty(contentType) ||
                !contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Invalid content type for file upload: {ContentType}", contentType);
                return ValidationResult.Invalid(415, "Content-Type must be multipart/form-data for file uploads");
            }

            // Check for potential security threats in headers
            if (HasSuspiciousHeaders(context.Request.Headers))
            {
                _logger.LogWarning("Suspicious headers detected in file upload request from {RemoteIp}",
                    context.Connection.RemoteIpAddress);
                return ValidationResult.Invalid(400, "Request contains potentially malicious headers");
            }

            // Validate request path for path traversal attempts
            var path = context.Request.Path.Value;
            if (!string.IsNullOrEmpty(path) && ContainsPathTraversal(path))
            {
                _logger.LogWarning("Path traversal attempt detected: {Path}", path);
                return ValidationResult.Invalid(400, "Invalid request path");
            }

            return ValidationResult.Valid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during file upload validation");
            return ValidationResult.Invalid(500, "Internal server error during validation");
        }
    }

    private static bool HasSuspiciousHeaders(IHeaderDictionary headers)
    {
        var suspiciousPatterns = new[]
        {
            "../", "..\\", "%2e%2e", "%2E%2E",
            "<script", "javascript:", "data:",
            "cmd.exe", "powershell", "/bin/sh",
            "eval(", "exec(", "system("
        };

        foreach (var header in headers)
        {
            var headerValue = header.Value.ToString().ToLowerInvariant();
            if (suspiciousPatterns.Any(pattern => headerValue.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsPathTraversal(string path)
    {
        var normalizedPath = path.Replace('\\', '/').ToLowerInvariant();
        return normalizedPath.Contains("../") ||
               normalizedPath.Contains("%2e%2e") ||
               normalizedPath.Contains("%2E%2E") ||
               normalizedPath.Contains("..\\");
    }

    private static async Task WriteErrorResponseAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var errorResponse = new
        {
            error = message,
            statusCode,
            timestamp = DateTimeOffset.UtcNow,
            path = context.Request.Path.Value
        };

        var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(jsonResponse);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";

        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    private class ValidationResult
    {
        public bool IsValid { get; private set; }
        public int StatusCode { get; private set; }
        public string ErrorMessage { get; private set; } = string.Empty;

        private ValidationResult() { }

        public static ValidationResult Valid()
        {
            return new ValidationResult { IsValid = true };
        }

        public static ValidationResult Invalid(int statusCode, string errorMessage)
        {
            return new ValidationResult
            {
                IsValid = false,
                StatusCode = statusCode,
                ErrorMessage = errorMessage
            };
        }
    }
}

/// <summary>
/// Extension methods for adding file upload validation middleware
/// </summary>
public static class FileUploadValidationMiddlewareExtensions
{
    /// <summary>
    /// Add file upload validation middleware to the pipeline
    /// </summary>
    public static IApplicationBuilder UseFileUploadValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<FileUploadValidationMiddleware>();
    }
}
