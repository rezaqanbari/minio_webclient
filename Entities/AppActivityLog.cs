using System;
using System.ComponentModel.DataAnnotations;

namespace minio_csharpClient.Entities;

public class AppActivityLog
{
    [Key]
    public long Id { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string Username { get; set; } = "ناشناس";

    [Required]
    [MaxLength(150)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Category { get; set; } = "General";

    [MaxLength(2000)]
    public string Details { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? IpAddress { get; set; }

    public bool IsSuccess { get; set; } = true;

    [MaxLength(20)]
    public string LogLevel { get; set; } = "Info";

    public string? ErrorMessage { get; set; }
}
