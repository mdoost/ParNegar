using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ParNegar.Domain.Entities.Auth;

[Table("UserRoles", Schema = "Auth")]
public class UserRole : BaseEntity
{
    [Required]
    public long UserID { get; set; }

    [Required]
    public long SystemRoleID { get; set; }

    [Required]
    [Column(TypeName = "datetimeoffset(7)")]
    public DateTimeOffset AssignedDate { get; set; }

    [Required]
    [MaxLength(10)]
    [Column(TypeName = "char(10)")]
    public string sAssignedDate { get; set; } = string.Empty;

    [Column(TypeName = "datetimeoffset(7)")]
    public DateTimeOffset? ExpiryDate { get; set; }

    [MaxLength(10)]
    [Column(TypeName = "char(10)")]
    public string? sExpiryDate { get; set; }

    [Required]
    public long AssignedByUserID { get; set; }

    // Navigation properties
    [ForeignKey(nameof(UserID))]
    public virtual User? User { get; set; }

    [ForeignKey(nameof(SystemRoleID))]
    public virtual AuthBase.SystemRole? SystemRole { get; set; }
}
