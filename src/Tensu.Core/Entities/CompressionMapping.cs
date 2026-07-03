namespace Tensu.Core.Entities;

public class CompressionMapping
{
    public long Id { get; set; }
    public string DecompressionKey { get; set; } = string.Empty;
    public string Strategy { get; set; } = string.Empty;
    public string? OriginalBody { get; set; }
    public string CompressedBody { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
