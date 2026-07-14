using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Net;
using System.Text.Json;
using Tensu.Api.Data;
using Tensu.Api.Services;
using Tensu.Core.Entities;
using Xunit;

namespace Tensu.Tests;

public class NotificationServiceTests : IDisposable
{
    private readonly TensuDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Mock<ILogger<NotificationService>> _loggerMock;

    public NotificationServiceTests()
    {
        var options = new DbContextOptionsBuilder<TensuDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new TensuDbContext(options);
        _db.Database.EnsureCreated();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(_db);
        serviceCollection.AddSingleton<IHttpClientFactory, FixedHttpClientFactory>();
        var serviceProvider = serviceCollection.BuildServiceProvider();
        _scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        _loggerMock = new Mock<ILogger<NotificationService>>();
    }

    private NotificationService CreateService(HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var factory = new FixedHttpClientFactory(statusCode);
        return new NotificationService(_scopeFactory, factory, _loggerMock.Object);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task NotifyAsync_RecordsFailedDelivery_OnHttpError()
    {
        _db.WebhookNotifications.Add(new WebhookNotification
        {
            Name = "Test",
            Url = "https://example.com/hook",
            Events = "[\"anomaly.detected\"]",
            IsEnabled = true
        });
        await _db.SaveChangesAsync();

        var service = CreateService(HttpStatusCode.BadRequest);

        await service.NotifyAsync("anomaly.detected", new Dictionary<string, object> { ["message"] = "test" });

        var delivery = await _db.WebhookDeliveries.FirstOrDefaultAsync();
        Assert.NotNull(delivery);
        Assert.False(delivery!.IsSuccess);
        Assert.Equal("BadRequest", delivery.LastStatusCode);
        Assert.Equal(1, delivery.AttemptCount);
        Assert.True(delivery.NextRetryAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task NotifyAsync_SkipsUnmatchedEventType()
    {
        _db.WebhookNotifications.Add(new WebhookNotification
        {
            Name = "Test",
            Url = "https://example.com/hook",
            Events = "[\"other.event\"]",
            IsEnabled = true
        });
        await _db.SaveChangesAsync();

        var service = CreateService(HttpStatusCode.OK);

        await service.NotifyAsync("anomaly.detected", new Dictionary<string, object> { ["message"] = "test" });

        Assert.Empty(_db.WebhookDeliveries);
    }

    [Fact]
    public async Task NotifyAsync_RetriesPendingDelivery_WhenDue()
    {
        var payload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["event"] = "anomaly.detected",
            ["timestamp"] = DateTime.UtcNow,
            ["data"] = new Dictionary<string, object> { ["message"] = "test" }
        });
        var webhook = new WebhookNotification
        {
            Name = "Test",
            Url = "https://example.com/hook",
            Events = "*",
            IsEnabled = true
        };
        _db.WebhookNotifications.Add(webhook);
        await _db.SaveChangesAsync();

        _db.WebhookDeliveries.Add(new WebhookDelivery
        {
            WebhookNotificationId = webhook.Id,
            EventType = "anomaly.detected",
            Payload = payload,
            AttemptCount = 1,
            MaxAttempts = 3,
            NextRetryAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await _db.SaveChangesAsync();

        var service = CreateService(HttpStatusCode.OK);

        await service.NotifyAsync("anomaly.detected", new Dictionary<string, object> { ["message"] = "new" });

        var delivery = await _db.WebhookDeliveries.FirstAsync();
        Assert.True(delivery.IsSuccess);
        Assert.Equal(1, delivery.AttemptCount);
    }

    [Fact]
    public async Task NotifyAsync_PreservesOriginalPayload_OnRetry()
    {
        var originalPayload = JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["event"] = "anomaly.detected",
            ["timestamp"] = DateTime.UtcNow,
            ["data"] = new Dictionary<string, object> { ["message"] = "original" }
        });
        var webhook = new WebhookNotification
        {
            Name = "Test",
            Url = "https://example.com/hook",
            Events = "*",
            IsEnabled = true
        };
        _db.WebhookNotifications.Add(webhook);
        await _db.SaveChangesAsync();

        _db.WebhookDeliveries.Add(new WebhookDelivery
        {
            WebhookNotificationId = webhook.Id,
            EventType = "anomaly.detected",
            Payload = originalPayload,
            AttemptCount = 1,
            MaxAttempts = 3,
            NextRetryAt = DateTime.UtcNow.AddMinutes(-1)
        });
        await _db.SaveChangesAsync();

        var service = CreateService(HttpStatusCode.OK);

        await service.NotifyAsync("anomaly.detected", new Dictionary<string, object> { ["message"] = "new payload" });

        var delivery = await _db.WebhookDeliveries.FirstAsync();
        Assert.True(delivery.IsSuccess);
        Assert.Equal(originalPayload, delivery.Payload);
    }

    private class FixedHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpStatusCode _statusCode;

        public FixedHttpClientFactory(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new FakeHttpMessageHandler(_statusCode)) { BaseAddress = new Uri("https://example.com") };
        }
    }

    private class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public FakeHttpMessageHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode));
        }
    }
}
