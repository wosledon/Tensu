namespace Tensu.Core.Entities;

public class ModelCapability
{
    public long Id { get; set; }
    public int ModelId { get; set; }
    public string Dimension { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? Evidence { get; set; }
    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Model Model { get; set; } = null!;
}
