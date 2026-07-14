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
    /// The provider is used to select a different key on each retry.
    /// </summary>
    public async Task<HttpResponseMessage?> ExecuteWithRetryAsync(
        Func<ProviderKey, Task<HttpResponseMessage>> requestFn,
        Provider provider,
        ProviderKey initialKey,
        bool isStream,
        int? maxRetries = null,
        int? baseDelayMs = null,
        int? maxDelayMs = null,
        CancellationToken cancellationToken = default)
    {
        // Only retry non-stream (idempotent) requests
        if (isStream)
        {
            return await requestFn(initialKey);
        }

        var effectiveMaxRetries = maxRetries ?? MaxRetries;
        var effectiveBaseDelay = baseDelayMs ?? BaseDelayMs;
        var effectiveMaxDelay = maxDelayMs ?? MaxDelayMs;

        HttpResponseMessage? lastResponse = null;
        var currentKey = initialKey;
        var failedKeys = new HashSet<int> { initialKey.Id };

        for (var attempt = 0; attempt <= effectiveMaxRetries; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    var delay = CalculateDelay(attempt, effectiveBaseDelay, effectiveMaxDelay);
                    _logger.LogInformation("Retry attempt {Attempt}/{MaxRetries} after {Delay}ms", attempt, effectiveMaxRetries, delay);
                    await Task.Delay(delay, cancellationToken);

                    // Try to select a different key after a failure
                    var nextKey = _loadBalancer.SelectKey(provider, failedKeys);
                    if (nextKey != null)
                    {
                        currentKey = nextKey;
                        failedKeys.Add(currentKey.Id);
                    }
                    else
                    {
                        _logger.LogError("No alternative keys available for provider {Provider} after failures", provider.Name);
                        throw new InvalidOperationException($"No available keys for provider {provider.Name} after retry attempts");
                    }
                }

                lastResponse = await requestFn(currentKey);

                // Don't retry on success or client errors (4xx)
                if (lastResponse.IsSuccessStatusCode || (int)lastResponse.StatusCode < 500)
                    return lastResponse;

                _logger.LogWarning("Request failed with {StatusCode}, attempt {Attempt}", lastResponse.StatusCode, attempt + 1);
                failedKeys.Add(currentKey.Id);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Request failed with exception, attempt {Attempt}", attempt + 1);
                failedKeys.Add(currentKey.Id);
                if (attempt == effectiveMaxRetries) throw;
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Request timed out, attempt {Attempt}", attempt + 1);
                failedKeys.Add(currentKey.Id);
                if (attempt == effectiveMaxRetries) throw;
            }
        }

        return lastResponse;
    }

    private int CalculateDelay(int attempt, int baseDelayMs, int maxDelayMs)
    {
        // Exponential backoff with jitter
        var delay = baseDelayMs * (int)Math.Pow(2, attempt - 1);
        delay = Math.Min(delay, maxDelayMs);
        // Add jitter (±25%)
        var jitter = (int)(delay * 0.25 * (Random.Shared.NextDouble() * 2 - 1));
        return Math.Max(100, delay + jitter);
    }
}
