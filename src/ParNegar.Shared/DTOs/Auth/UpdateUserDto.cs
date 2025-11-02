namespace ParNegar.Shared.DTOs.Auth;

public class UpdateUserDto
{
    // ⚠️ NO GUID HERE - comes from URL parameter
    public long BranchID { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Mobile { get; set; }
    public bool IsActive { get; set; }
}
