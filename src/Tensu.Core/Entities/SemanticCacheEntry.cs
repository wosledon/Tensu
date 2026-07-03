namespace Tensu.Core.Entities;

public class SemanticCacheEntry
{
    public long Id { get; set; }
    public string Model { get; set; } = string.Empty;
    public string Embedding { get; set; } = "[]";
    public string RequestBody { get; set; } = string.Empty;
    public string ResponseBody { get; set; } = string.Empty;
    public bool IsStream { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
