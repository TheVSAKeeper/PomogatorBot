using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PomogatorBot.Web.Infrastructure.Entities;

[Index(nameof(KeyHash), IsUnique = true)]
public class ExternalClientEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    [Required]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public bool IsEnabled { get; set; } = true;

    [Required]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public long? CreatedByAdminUserId { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public DateTime? LastUsedAtUtc { get; set; }

    public long UsageCount { get; set; }

    [Required]
    [MaxLength(64)]
    [Column(TypeName = "varchar(64)")]
    public string KeyHash { get; set; } = string.Empty;
}
