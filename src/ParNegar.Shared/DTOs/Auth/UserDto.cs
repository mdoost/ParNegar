namespace ParNegar.Shared.DTOs.Auth;

public class UserDto
{
    public long ID { get; set; }
    public Guid GUID { get; set; }
    public long BranchID { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public bool IsActive { get; set; }
    public bool IsLocked { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
}
