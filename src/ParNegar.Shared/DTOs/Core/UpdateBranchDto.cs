namespace ParNegar.Shared.DTOs.Core;

public class UpdateBranchDto
{
    public string Name { get; set; } = string.Empty;
    public string NameFa { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? ManagerName { get; set; }
}
