namespace Tensu.Core.Entities;

public class DailyStat
{
    public long Id { get; set; }
    public DateTime Date { get; set; }
    public int? OrganizationId { get; set; }
    public string? ModelName { get; set; }
    public string? ProviderName { get; set; }

    public int TotalRequests { get; set; }
    public int SuccessRequests { get; set; }
    public int FailedRequests { get; set; }
    public int RateLimitedRequests { get; set; }
    public int CacheHits { get; set; }

    public long TotalInputTokens { get; set; }
    public long TotalOutputTokens { get; set; }
    public long TotalInputTokensAfterCompression { get; set; }

    public decimal TotalInputCost { get; set; }
    public decimal TotalOutputCost { get; set; }

    public long? AvgLatencyMs { get; set; }
    public long? P50LatencyMs { get; set; }
    public long? P95LatencyMs { get; set; }
    public long? P99LatencyMs { get; set; }
    public long? AvgTtftMs { get; set; }
    public double? AvgOutputTokensPerSecond { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
