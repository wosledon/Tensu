using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tensu.Api.Data;

namespace Tensu.Api.Services;

public class NotificationService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(IServiceScopeFactory scopeFactory, IHttpClientFactory httpClientFactory, ILogger<NotificationService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task NotifyAsync(string eventType, object payload)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TensuDbContext>();

        await ProcessRetriesAsync(db, eventType, payload);

        var webhooks = await db.Set<Core.Entities.WebhookNotification>()
            .Where(w => w.IsEnabled)
            .ToListAsync();

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        foreach (var webhook in webhooks)
        {
            string? body = null;
            try
            {
                var events = new List<string>();
                if (!string.IsNullOrEmpty(webhook.Events) && webhook.Events.StartsWith("["))
                {
                    try
                    {
                        events = JsonSerializer.Deserialize<List<string>>(webhook.Events) ?? [];
                    }
                    catch
                    {
                        events = new List<string>();
                    }
                }
                else if (!string.IsNullOrEmpty(webhook.Events))
                {
                    events.Add(webhook.Events);
                }

                if (events.Count > 0 && !events.Contains("*") && !events.Contains(eventType))
                    continue;

                body = JsonSerializer.Serialize(new
                {
                    @event = eventType,
                    timestamp = DateTime.UtcNow,
                    data = payload
                });

                var request = new HttpRequestMessage(HttpMethod.Post, webhook.Url)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };

                if (!string.IsNullOrEmpty(webhook.Secret))
                {
                    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhook.Secret));
                    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
                    request.Headers.Add("X-Tensu-Signature", Convert.ToHexString(hash));
                }

                var response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    db.WebhookDeliveries.Add(new Core.Entities.WebhookDelivery
                    {
                        WebhookNotificationId = webhook.Id,
                        EventType = eventType,
                        Payload = body,
                        AttemptCount = 1,
                        MaxAttempts = 3,
                        LastStatusCode = ((int)response.StatusCode).ToString(),
                        IsSuccess = true,
                        LastAttemptAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    });
                    await db.SaveChangesAsync();
                }
                else
                {
                    _logger.LogWarning("Webhook {Name} returned {StatusCode}", webhook.Name, response.StatusCode);
                    await RecordFailedDeliveryAsync(db, webhook.Id, eventType, body ?? string.Empty, response.StatusCode.ToString(), null);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send webhook {Name}", webhook.Name);
                await RecordFailedDeliveryAsync(db, webhook.Id, eventType, body ?? string.Empty, null, ex.Message);
            }
        }
    }

    private async Task ProcessRetriesAsync(TensuDbContext db, string eventType, object payload)
    {
        var now = DateTime.UtcNow;
        var pendingRetries = await db.WebhookDeliveries
            .Where(d => !d.IsSuccess && d.NextRetryAt <= now && d.AttemptCount < d.MaxAttempts)
            .OrderBy(d => d.NextRetryAt)
            .Take(10)
            .ToListAsync();

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(10);

        foreach (var delivery in pendingRetries)
        {
            try
            {
                var webhook = await db.WebhookNotifications.FindAsync(delivery.WebhookNotificationId);
                if (webhook == null || !webhook.IsEnabled)
                {
                    delivery.IsSuccess = true;
                    delivery.LastErrorMessage = "Webhook disabled or deleted";
                    delivery.LastAttemptAt = now;
                    await db.SaveChangesAsync();
                    continue;
                }

                var events = new List<string>();
                if (!string.IsNullOrEmpty(webhook.Events) && webhook.Events.StartsWith("["))
                {
                    try
                    {
                        events = JsonSerializer.Deserialize<List<string>>(webhook.Events) ?? [];
                    }
                    catch
                    {
                        events = new List<string>();
                    }
                }
                else if (!string.IsNullOrEmpty(webhook.Events))
                {
                    events.Add(webhook.Events);
                }

                if (events.Count > 0 && !events.Contains("*") && !events.Contains(eventType))
                    continue;

                var originalEventType = delivery.EventType;
                var originalPayload = delivery.Payload;
                var body = string.IsNullOrEmpty(originalPayload)
                    ? JsonSerializer.Serialize(new { @event = originalEventType, timestamp = now, data = payload })
                    : originalPayload;

                var request = new HttpRequestMessage(HttpMethod.Post, webhook.Url)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };

                if (!string.IsNullOrEmpty(webhook.Secret))
                {
                    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhook.Secret));
                    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
                    request.Headers.Add("X-Tensu-Signature", Convert.ToHexString(hash));
                }

                var response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    delivery.IsSuccess = true;
                    delivery.LastStatusCode = ((int)response.StatusCode).ToString();
                    delivery.LastAttemptAt = now;
                }
                else
                {
                    delivery.AttemptCount++;
                    delivery.LastStatusCode = ((int)response.StatusCode).ToString();
                    delivery.LastErrorMessage = $"HTTP {(int)response.StatusCode}";
                    delivery.NextRetryAt = now.AddMinutes(Math.Pow(2, delivery.AttemptCount));
                    delivery.LastAttemptAt = now;
                }
            }
            catch (Exception ex)
            {
                delivery.AttemptCount++;
                delivery.LastErrorMessage = ex.Message;
                delivery.NextRetryAt = now.AddMinutes(Math.Pow(2, delivery.AttemptCount));
                delivery.LastAttemptAt = now;
            }

            await db.SaveChangesAsync();
        }
    }

    private async Task RecordFailedDeliveryAsync(TensuDbContext db, int webhookId, string eventType, string payload, string? statusCode, string? error)
    {
        var backoff = 2;
        db.WebhookDeliveries.Add(new Core.Entities.WebhookDelivery
        {
            WebhookNotificationId = webhookId,
            EventType = eventType,
            Payload = payload,
            AttemptCount = 1,
            MaxAttempts = 3,
            LastStatusCode = statusCode,
            LastErrorMessage = error,
            NextRetryAt = DateTime.UtcNow.AddMinutes(backoff),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}
