namespace Tensu.Core.Entities;

public class DataDeletionRequest
{
    public int Id { get; set; }
    public int? OrganizationId { get; set; }
    public int? UserId { get; set; }
    public string? ApiKeyId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "pending"; // pending, processing, completed, failed
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int DeletedRequestLogs { get; set; }
    public int DeletedArchivedLogs { get; set; }
    public string? RequestId { get; set; }
}
