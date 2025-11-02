using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ParNegar.Domain.Entities.AuthBase;

[Table("SystemRoles", Schema = "AuthBase")]
public class SystemRole : BaseEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string NameFa { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Description { get; set; }

    [Required]
    public bool IsSystemRole { get; set; }

    [Required]
    public bool IsCustomerRole { get; set; }

    // Navigation properties
    public virtual ICollection<Auth.UserRole> UserRoles { get; set; } = new List<Auth.UserRole>();
}
