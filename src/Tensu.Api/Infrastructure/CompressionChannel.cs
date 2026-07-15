using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Tensu.Core.Entities;

namespace Tensu.Api.Infrastructure;

public class CompressionMappingEntry
{
    public string OriginalBody { get; set; } = string.Empty;
    public string CompressedBody { get; set; } = string.Empty;
    public string Strategy { get; set; } = string.Empty;
    public string DecompressionKey { get; set; } = string.Empty;
}

public class CompressionChannel
{
    private readonly Channel<CompressionMappingEntry> _channel;

    public CompressionChannel()
    {
        _channel = Channel.CreateBounded<CompressionMappingEntry>(new BoundedChannelOptions(5000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = false
        });
    }

    public ValueTask EnqueueAsync(CompressionMappingEntry entry, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(entry, cancellationToken);

    public ChannelReader<CompressionMappingEntry> Reader => _channel.Reader;
}

public class CompressionBackgroundService : BackgroundService
{
    private readonly CompressionChannel _compressionChannel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CompressionBackgroundService> _logger;

    public CompressionBackgroundService(
        CompressionChannel compressionChannel,
        IServiceScopeFactory scopeFactory,
        ILogger<CompressionBackgroundService> logger)
    {
        _compressionChannel = compressionChannel;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var entry in _compressionChannel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<Data.TensuDbContext>();
                await PersistMappingAsync(db, entry, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to persist compression mapping for key {Key}", entry.DecompressionKey);
            }
        }
    }

    private static async Task PersistMappingAsync(Data.TensuDbContext db, CompressionMappingEntry entry, CancellationToken cancellationToken)
    {
        const int defaultMaxMappingSize = 10000;
        var maxMappingSizeSetting = await db.Settings
            .AsNoTracking()
            .Where(s => s.Key == "compression.maxMappingSize")
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);
        var maxMappingSize = int.TryParse(maxMappingSizeSetting, out var parsed) ? parsed : defaultMaxMappingSize;

        var mapping = await db.CompressionMappings
            .FirstOrDefaultAsync(m => m.DecompressionKey == entry.DecompressionKey && m.Strategy == entry.Strategy, cancellationToken);

        if (mapping != null)
        {
            mapping.CompressedBody = entry.CompressedBody;
            mapping.OriginalBody = entry.OriginalBody.Length <= maxMappingSize ? entry.OriginalBody : null;
            mapping.CreatedAt = DateTime.UtcNow;
        }
        else
        {
            db.CompressionMappings.Add(new CompressionMapping
            {
                DecompressionKey = entry.DecompressionKey,
                Strategy = entry.Strategy,
                CompressedBody = entry.CompressedBody,
                OriginalBody = entry.OriginalBody.Length <= maxMappingSize ? entry.OriginalBody : null,
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
