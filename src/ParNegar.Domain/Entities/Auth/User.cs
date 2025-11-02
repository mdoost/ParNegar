using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ParNegar.Domain.Entities.Auth;

[Table("Users", Schema = "Auth")]
public class User : BaseEntity
{
    [Required]
    public long BranchID { get; set; }

    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string PasswordSalt { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(20)]
    public string? Mobile { get; set; }

    [Required]
    public bool IsLocked { get; set; } = false;

    [Column(TypeName = "datetimeoffset(7)")]
    public DateTimeOffset? PasswordChangeDate { get; set; }

    [MaxLength(10)]
    [Column(TypeName = "char(10)")]
    public string? sPasswordChangeDate { get; set; }

    [Required]
    public int FailedLoginAttempts { get; set; } = 0;

    [Column(TypeName = "datetimeoffset(7)")]
    public DateTimeOffset? FirstFailedLoginDate { get; set; }

    [MaxLength(10)]
    [Column(TypeName = "char(10)")]
    public string? sFirstFailedLoginDate { get; set; }

    // Navigation properties
    [ForeignKey(nameof(BranchID))]
    public virtual Core.Branch? Branch { get; set; }

    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public virtual ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public virtual ICollection<UserLoginLog> UserLoginLogs { get; set; } = new List<UserLoginLog>();
}
