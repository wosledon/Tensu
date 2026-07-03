namespace Tensu.Core.Entities;

/// <summary>
/// Database-backed shared counter for rate limiting, quotas and concurrency.
/// </summary>
public class RateLimitCounter
{
    public long Id { get; set; }
    public string Scope { get; set; } = string.Empty;
    public DateTime WindowStart { get; set; }
    public int WindowSeconds { get; set; }
    public long Value { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
