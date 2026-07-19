using Tensu.Core.Enums;

namespace Tensu.Core.Entities;

/// <summary>
/// Cold-storage archive of request audit logs. RequestId is the primary key
/// so archived records can be looked up directly without a surrogate id.
/// </summary>
public class ArchivedRequestLog
{
    public string RequestId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int? ApiKeyId { get; set; }
    public int? OrganizationId { get; set; }
    public int? UserId { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string? ResolvedModelName { get; set; }
    public int? ProviderId { get; set; }
    public string? ProviderName { get; set; }

    public int? InputTokens { get; set; }
    public int? InputTokensAfterCompression { get; set; }
    public int? OutputTokens { get; set; }
    public int? CachedInputTokens { get; set; }
    public int? ReasoningTokens { get; set; }
    public bool CacheHit { get; set; }
    public bool SemanticCacheHit { get; set; }

    public long? TimeToFirstTokenMs { get; set; }
    public long? TotalDurationMs { get; set; }
    public double? OutputTokensPerSecond { get; set; }

    public decimal? InputCost { get; set; }
    public decimal? OutputCost { get; set; }
    public string Currency { get; set; } = "USD";

    public bool CompressionApplied { get; set; }
    public string? CompressionStrategy { get; set; }
    public string? CompressionMappingKey { get; set; }

    public RequestStatus Status { get; set; } = RequestStatus.Success;
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public bool IsStream { get; set; }

    public string? RequestContent { get; set; }
    public string? ResponseContent { get; set; }
}
