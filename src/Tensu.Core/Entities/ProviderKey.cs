using Tensu.Core.Enums;

namespace Tensu.Core.Entities;

public class ProviderKey
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyValue { get; set; } = string.Empty; // encrypted
    public int Weight { get; set; } = 1;
    public KeyStatus Status { get; set; } = KeyStatus.Active;
    public int? RateLimitRpm { get; set; }
    public int? RateLimitTpm { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastHealthCheckAt { get; set; }

    // Navigation
    public Provider Provider { get; set; } = null!;
}
