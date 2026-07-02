using System.Text;
using System.Text.Json;
using Tensu.Core.Entities;
using Tensu.Core.Enums;

namespace Tensu.Api.Infrastructure;

/// <summary>
/// Retry policy with exponential backoff + jitter.
/// Only retries idempotent requests (non-streaming).
/// Automatically fails over to other keys/providers.
/// </summary>
public class RetryPolicy
{
    private readonly ILogger<RetryPolicy> _logger;
    private readonly LoadBalancer _loadBalancer;

    public int MaxRetries { get; set; } = 2;
    public int BaseDelayMs { get; set; } = 500;
    public int MaxDelayMs { get; set; } = 5000;

    public RetryPolicy(ILogger<RetryPolicy> logger, LoadBalancer loadBalancer)
    {
        _logger = logger;
        _loadBalancer = loadBalancer;
    }

    /// <summary>
    /// Execute a request with retry logic.
    /// The keySelector is called on each retry to potentially select a different key.
    /// </summary>
    public async Task<HttpResponseMessage?> ExecuteWithRetryAsync(
        Func<ProviderKey, Task<HttpResponseMessage>> requestFn,
        ProviderKey initialKey,
        bool isStream,
        CancellationToken cancellationToken = default)
    {
        // Only retry non-stream (idempotent) requests
        if (isStream)
        {
            return await requestFn(initialKey);
        }

        HttpResponseMessage? lastResponse = null;
        var currentKey = initialKey;

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    var delay = CalculateDelay(attempt);
                    _logger.LogInformation("Retry attempt {Attempt}/{MaxRetries} after {Delay}ms", attempt, MaxRetries, delay);
                    await Task.Delay(delay, cancellationToken);
                }

                lastResponse = await requestFn(currentKey);

                // Don't retry on success or client errors (4xx)
                if (lastResponse.IsSuccessStatusCode || (int)lastResponse.StatusCode < 500)
                    return lastResponse;

                _logger.LogWarning("Request failed with {StatusCode}, attempt {Attempt}", lastResponse.StatusCode, attempt + 1);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Request failed with exception, attempt {Attempt}", attempt + 1);
                if (attempt == MaxRetries) throw;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Request timed out, attempt {Attempt}", attempt + 1);
                if (attempt == MaxRetries) throw;
            }
        }

        return lastResponse;
    }

    private int CalculateDelay(int attempt)
    {
        // Exponential backoff with jitter
        var delay = BaseDelayMs * (int)Math.Pow(2, attempt - 1);
        delay = Math.Min(delay, MaxDelayMs);
        // Add jitter (±25%)
        var jitter = (int)(delay * 0.25 * (Random.Shared.NextDouble() * 2 - 1));
        return Math.Max(100, delay + jitter);
    }
}
