namespace ParNegar.Shared.DTOs.Auth;

/// <summary>
/// DTO for User combobox - contains ID, GUID and display name
/// </summary>
public class UserComboboxDto
{
    public long ID { get; set; }
    public Guid GUID { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
