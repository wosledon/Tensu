using Tensu.Core.Enums;

namespace Tensu.Core.Entities;

public class Provider
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProtocolType Protocol { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ProviderHealthStatus HealthStatus { get; set; } = ProviderHealthStatus.Unknown;
    public DateTime? LastHealthCheckAt { get; set; }
    public bool IsEnabled { get; set; } = true;
    public LoadBalanceStrategy KeyLoadBalanceStrategy { get; set; } = LoadBalanceStrategy.RoundRobin;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<ProviderKey> Keys { get; set; } = [];
    public ICollection<Model> Models { get; set; } = [];
}
