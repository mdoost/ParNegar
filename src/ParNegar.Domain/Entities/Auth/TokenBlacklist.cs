using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ParNegar.Domain.Entities.Auth;

[Table("TokenBlacklists", Schema = "Auth")]
public class TokenBlacklist : BaseEntity
{
    [MaxLength(100)]
    public string? TokenId { get; set; }

    [MaxLength(100)]
    public string? SessionId { get; set; }

    [Required]
    public long UserId { get; set; }

    [Required]
    [Column(TypeName = "datetimeoffset(7)")]
    public DateTimeOffset BlacklistedAt { get; set; }

    [Required]
    [Column(TypeName = "datetimeoffset(7)")]
    public DateTimeOffset ExpiresAt { get; set; }

    [Required]
    [MaxLength(200)]
    public string Reason { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Type { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? IpAddress { get; set; }
}
