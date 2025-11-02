namespace ParNegar.Shared.DTOs.Auth;

public class UserFilterDto
{
    public string? SearchTerm { get; set; }
    public long? BranchID { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsLocked { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = true;
}
