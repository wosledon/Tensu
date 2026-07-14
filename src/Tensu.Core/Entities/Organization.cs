namespace Tensu.Core.Entities;

public class Organization
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty; // materialized path, e.g. "/1/5/"
    public string? Description { get; set; }
    public bool EnableContentLogging { get; set; } = true;
    public bool CompressionEnabled { get; set; } = true;
    public int DataRetentionDays { get; set; } = 30;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Organization? Parent { get; set; }
    public ICollection<Organization> Children { get; set; } = [];
    public ICollection<User> Users { get; set; } = [];
    public ICollection<ApiKey> ApiKeys { get; set; } = [];
    public ICollection<Quota> Quotas { get; set; } = [];
}
