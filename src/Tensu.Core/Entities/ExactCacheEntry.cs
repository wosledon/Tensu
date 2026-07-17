namespace Tensu.Core.Entities;

/// <summary>
/// Database-backed exact cache entry, used when cache.backend = "database"
/// for cross-instance cache sharing (multi-instance deployments).
/// </summary>
public class ExactCacheEntry
{
    public long Id { get; set; }
    public string CacheKey { get; set; } = string.Empty;
    public string ResponseBody { get; set; } = string.Empty;
    public bool IsStream { get; set; }
    public int HitCount { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
