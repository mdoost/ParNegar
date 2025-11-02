namespace ParNegar.Shared.DTOs.Common;

/// <summary>
/// Standard error response model
/// </summary>
public class ErrorResponse
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string TraceId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public object? Details { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }
}
