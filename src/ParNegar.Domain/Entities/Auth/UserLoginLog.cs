using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ParNegar.Domain.Entities.Auth;

[Table("UserLoginLogs", Schema = "Auth")]
public class UserLoginLog : BaseEntity
{
    public long? UserID { get; set; }

    [Required]
    [MaxLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string IPAddress { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    [Required]
    [MaxLength(20)]
    public string LoginType { get; set; } = string.Empty;

    [Required]
    public bool IsSuccessful { get; set; }

    [MaxLength(200)]
    public string? FailureReason { get; set; }

    [MaxLength(100)]
    public string? SessionID { get; set; }

    [Required]
    [Column(TypeName = "datetimeoffset(7)")]
    public DateTimeOffset LoginDate { get; set; }

    [Required]
    [MaxLength(10)]
    [Column(TypeName = "char(10)")]
    public string sLoginDate { get; set; } = string.Empty;

    [Column(TypeName = "datetimeoffset(7)")]
    public DateTimeOffset? LogoutDate { get; set; }

    [MaxLength(10)]
    [Column(TypeName = "char(10)")]
    public string? sLogoutDate { get; set; }

    // Navigation properties
    [ForeignKey(nameof(UserID))]
    public virtual User? User { get; set; }
}
