namespace Tensu.Core.Entities;

public class Quota
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public int? ApiKeyId { get; set; } // null = org-level quota
    public int? Rpm { get; set; } // requests per minute
    public int? Tpm { get; set; } // tokens per minute
    public long? DailyTokenLimit { get; set; }
    public long? MonthlyTokenLimit { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Organization Organization { get; set; } = null!;
    public ApiKey? ApiKey { get; set; }
}
