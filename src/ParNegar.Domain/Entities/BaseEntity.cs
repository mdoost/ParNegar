using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ParNegar.Domain.Entities;

/// <summary>
/// Base entity for all domain entities
/// </summary>
public abstract class BaseEntity
{
    [Key]
    [Column("ID")]
    public long ID { get; set; }

    [Required]
    public Guid GUID { get; set; } = Guid.NewGuid();

    [Required]
    public bool IsActive { get; set; } = true;

    [Required]
    public long CreatedBy { get; set; }

    public long? UpdatedBy { get; set; }

    [Required]
    [Column(TypeName = "datetimeoffset(7)")]
    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;

    [Column(TypeName = "datetimeoffset(7)")]
    public DateTimeOffset? UpdatedDate { get; set; }
}
