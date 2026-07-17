using System.ComponentModel.DataAnnotations.Schema;

namespace Tensu.Core.Entities;

public class Quota
{
    public int Id { get; set; }
    public int OrganizationId { get; set; }
    public int? ApiKeyId { get; set; } // null = org-level quota
    public int? ModelId { get; set; } // null = not model-scoped
    public int? Rpm { get; set; } // requests per minute
    public int? Tpm { get; set; } // tokens per minute
    public int? ConcurrentRequestLimit { get; set; }
    public long? DailyTokenLimit { get; set; }
    public long? MonthlyTokenLimit { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    private string? _scope;

    /// <summary>
    /// Quota scope: org (no ApiKey/Model), key (ApiKeyId set), model (ModelId set).
    /// Not persisted; derived from the FK fields. A setter is provided so API clients
    /// can indicate the intended scope when creating/updating.
    /// </summary>
    [NotMapped]
    public string Scope
    {
        get => _scope ?? (ModelId.HasValue ? "model" : ApiKeyId.HasValue ? "key" : "org");
        set => _scope = value;
    }

    // Navigation
    public Organization? Organization { get; set; }
    public ApiKey? ApiKey { get; set; }
    public Model? Model { get; set; }
}
