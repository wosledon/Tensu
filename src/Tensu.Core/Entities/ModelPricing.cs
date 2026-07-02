namespace Tensu.Core.Entities;

public class ModelPricing
{
    public int Id { get; set; }
    public int ModelId { get; set; }
    public decimal InputPricePerMillionTokens { get; set; }
    public decimal OutputPricePerMillionTokens { get; set; }
    public decimal? CachedInputPricePerMillionTokens { get; set; }
    public decimal? ThinkingPricePerMillionTokens { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal ExchangeRate { get; set; } = 1.0m; // rate to USD
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Model Model { get; set; } = null!;
}
