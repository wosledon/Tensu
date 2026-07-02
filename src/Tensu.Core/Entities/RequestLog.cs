using Tensu.Core.Enums;

namespace Tensu.Core.Entities;

public class RequestLog
{
    public long Id { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public int? ApiKeyId { get; set; }
    public int? OrganizationId { get; set; }
    public int? UserId { get; set; }
    public string ModelName { get; set; } = string.Empty; // requested model name
    public string? ResolvedModelName { get; set; } // actual model after routing
    public int? ProviderId { get; set; }
    public string? ProviderName { get; set; }

    // Token metrics
    public int? InputTokens { get; set; }
    public int? InputTokensAfterCompression { get; set; }
    public int? OutputTokens { get; set; }
    public bool CacheHit { get; set; }

    // Performance
    public long? TimeToFirstTokenMs { get; set; }
    public long? TotalDurationMs { get; set; }
    public double? OutputTokensPerSecond { get; set; }

    // Cost
    public decimal? InputCost { get; set; }
    public decimal? OutputCost { get; set; }
    public string Currency { get; set; } = "USD";

    // Compression
    public bool CompressionApplied { get; set; }
    public string? CompressionStrategy { get; set; }

    // Status
    public RequestStatus Status { get; set; } = RequestStatus.Success;
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public bool IsStream { get; set; }

    // Content (optional, per org config)
    public string? RequestContent { get; set; }
    public string? ResponseContent { get; set; }
}
