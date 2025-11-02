namespace ParNegar.Shared.DTOs.Auth;

public class SessionDto
{
    public long ID { get; set; }
    public Guid GUID { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? DeviceId { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsCurrentSession { get; set; }
    public bool IsValid { get; set; }
}
