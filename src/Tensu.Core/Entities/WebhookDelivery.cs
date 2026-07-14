namespace Tensu.Core.Entities;

public class WebhookDelivery
{
    public long Id { get; set; }
    public int WebhookNotificationId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 3;
    public string? LastStatusCode { get; set; }
    public string? LastErrorMessage { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastAttemptAt { get; set; }
    public bool IsSuccess { get; set; }
}
