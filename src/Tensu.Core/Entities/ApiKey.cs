using Tensu.Core.Enums;

namespace Tensu.Core.Entities;

public class ApiKey
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public int? UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyValue { get; set; } = string.Empty; // encrypted
    public string KeyPrefix { get; set; } = string.Empty; // first 8 chars for display
    public DateTime? ExpiresAt { get; set; }
    public string? AllowedModels { get; set; } // JSON array of model names, null = all
    public string? IpWhitelist { get; set; } // JSON array of IPs, null = no restriction
    public KeyStatus Status { get; set; } = KeyStatus.Active;
    public int? RateLimitRpm { get; set; }
    public int? RateLimitTpm { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Organization Organization { get; set; } = null!;
    public User? User { get; set; }
}
