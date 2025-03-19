using System.ComponentModel.DataAnnotations;

namespace PomogatorBot.Web.Infrastructure.Entities;

public class User
{
    [Key]
    public long UserId { get; set; }

    [Required]
    [MaxLength(64)]
    public string Username { get; set; } = null!;

    [Required]
    [MaxLength(255)]
    public string FirstName { get; set; } = null!;

    [MaxLength(255)]
    public string? LastName { get; set; }

    [Required]
    public DateTime CreatedAt { get; set; }
}
