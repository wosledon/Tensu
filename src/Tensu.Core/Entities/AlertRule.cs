namespace Tensu.Core.Entities;

public class AlertRule
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string? Severity { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string WebhookIds { get; set; } = "[]";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
