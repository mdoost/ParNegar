using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ParNegar.Domain.Entities.Auth;

[Table("RefreshTokens", Schema = "Auth")]
public class RefreshToken : BaseEntity
{
    [Required]
    public long UserID { get; set; }

    [Required]
    [MaxLength(500)]
    public string Token { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string JwtId { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? SessionId { get; set; }

    [Required]
    [Column(TypeName = "datetimeoffset(7)")]
    public DateTimeOffset ExpiresAt { get; set; }

    [Required]
    public bool IsUsed { get; set; } = false;

    [Required]
    public bool IsRevoked { get; set; } = false;

    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    [MaxLength(100)]
    public string? DeviceId { get; set; }

    // Navigation properties
    [ForeignKey(nameof(UserID))]
    public virtual User? User { get; set; }
}
