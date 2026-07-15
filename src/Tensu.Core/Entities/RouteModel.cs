using Tensu.Core.Enums;

namespace Tensu.Core.Entities;

public class RouteModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty; // virtual model name exposed to users
    public string? Description { get; set; }
    public RouteModelMode Mode { get; set; } = RouteModelMode.Shadow;
    public int? FallbackModelId { get; set; } // route mode fallback
    public int? RoutingModelId { get; set; } // route mode: LLM used for intent recognition
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Model? FallbackModel { get; set; }
    public Model? RoutingModel { get; set; }
    public ICollection<RouteModelTarget> Targets { get; set; } = [];
    public ICollection<RouteRule> Rules { get; set; } = [];
}
