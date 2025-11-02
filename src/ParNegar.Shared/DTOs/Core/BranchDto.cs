namespace ParNegar.Shared.DTOs.Core;

public class BranchDto
{
    public long ID { get; set; }
    public Guid GUID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameFa { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? ManagerName { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
}
