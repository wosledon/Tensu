using System.Threading.Channels;
using Tensu.Core.Entities;

namespace Tensu.Api.Infrastructure;

public class AuditChannel
{
    private readonly Channel<RequestLog> _channel;

    public AuditChannel()
    {
        _channel = Channel.CreateBounded<RequestLog>(new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = false
        });
    }

    public ValueTask EnqueueAsync(RequestLog log, CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(log, cancellationToken);

    public IAsyncEnumerable<RequestLog> ReadAllAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAllAsync(cancellationToken);

    public ChannelReader<RequestLog> Reader => _channel.Reader;
}

public class AuditBackgroundService : BackgroundService
{
    private readonly AuditChannel _auditChannel;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly EncryptionService _encryptionService;
    private readonly ILogger<AuditBackgroundService> _logger;

    public AuditBackgroundService(
        AuditChannel auditChannel,
        IServiceScopeFactory scopeFactory,
        EncryptionService encryptionService,
        ILogger<AuditBackgroundService> logger)
    {
        _auditChannel = auditChannel;
        _scopeFactory = scopeFactory;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var log in _auditChannel.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<Data.TensuDbContext>();
                var settings = scope.ServiceProvider.GetRequiredService<Services.SettingsService>();

                var encryptContent = string.Equals(
                    await settings.GetAsync("audit.encryptContent"), "true", StringComparison.OrdinalIgnoreCase);
                if (encryptContent)
                {
                    log.RequestContent = _encryptionService.EncryptAuditContent(log.RequestContent);
                    log.ResponseContent = _encryptionService.EncryptAuditContent(log.ResponseContent);
                }

                db.RequestLogs.Add(log);
                await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist audit log for request {RequestId}", log.RequestId);
            }
        }
    }
}
