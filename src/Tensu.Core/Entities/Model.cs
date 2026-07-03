namespace Tensu.Core.Entities;

public class Model
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public bool SupportsVision { get; set; }
    public bool SupportsReasoning { get; set; }
    public bool SupportsToolUse { get; set; }
    public bool SupportsThinking { get; set; } // thinking/non-thinking mode
    public string? ThinkingStrengths { get; set; } // JSON array: ["none","low","medium","high","max","xhigh"]
    public int InputContextSize { get; set; }
    public int OutputContextSize { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool CompressionEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Provider Provider { get; set; } = null!;
    public ICollection<ModelPricing> Pricings { get; set; } = [];
    public ICollection<ModelCapability> Capabilities { get; set; } = [];
}
