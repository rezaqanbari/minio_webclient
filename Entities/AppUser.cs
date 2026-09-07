using System;
using System.ComponentModel.DataAnnotations;

namespace minio_csharpClient.Entities;

public class AppUser
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Role { get; set; } = "User"; // "Admin" or "User"

    [MaxLength(100)]
    public string? AllowedBucket { get; set; }

    [MaxLength(500)]
    public string? AllowedPrefix { get; set; } // e.g. "reports/" or "media/audio/"

    public bool IsActive { get; set; } = true;

    public bool CanViewAuditLogs { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
