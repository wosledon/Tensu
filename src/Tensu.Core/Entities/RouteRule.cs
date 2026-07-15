using Tensu.Core.Enums;

namespace Tensu.Core.Entities;

public class RouteRule
{
    public int Id { get; set; }
    public int RouteModelId { get; set; }
    public RouteRuleType Type { get; set; }
    public string Condition { get; set; } = string.Empty; // JSON: keywords, regex pattern, or context size threshold
    public int TargetModelId { get; set; }
    public int Priority { get; set; } // lower = higher priority
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public RouteModel? RouteModel { get; set; }
    public Model? TargetModel { get; set; }
}
