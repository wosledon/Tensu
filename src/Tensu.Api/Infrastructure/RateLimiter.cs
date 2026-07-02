using System.Collections.Concurrent;

namespace Tensu.Api.Infrastructure;

/// <summary>
/// Rate limiter using sliding window counter.
/// Checks RPM and TPM per API key. Uses in-memory counters (database-backed for multi-instance).
/// </summary>
public class RateLimiter
{
    private static readonly ConcurrentDictionary<string, SlidingWindow> _rpmWindows = new();
    private static readonly ConcurrentDictionary<string, SlidingWindow> _tpmWindows = new();
    private readonly ILogger<RateLimiter> _logger;

    public RateLimiter(ILogger<RateLimiter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Check if a request is allowed under the rate limit.
    /// Returns (allowed, retryAfterSeconds).
    /// </summary>
    public (bool allowed, int retryAfterSeconds) CheckRateLimit(int apiKeyId, int? rpmLimit, int? tpmLimit, int estimatedTokens = 0)
    {
        var keyPrefix = $"key:{apiKeyId}";

        // Check RPM
        if (rpmLimit.HasValue && rpmLimit.Value > 0)
        {
            var rpmKey = $"{keyPrefix}:rpm";
            var window = _rpmWindows.GetOrAdd(rpmKey, _ => new SlidingWindow(60));
            var currentRpm = window.Count();
            if (currentRpm >= rpmLimit.Value)
            {
                var retryAfter = window.SecondsUntilSlotAvailable();
                _logger.LogWarning("Rate limit exceeded for API key {ApiKeyId}: RPM {Current}/{Limit}", apiKeyId, currentRpm, rpmLimit.Value);
                return (false, retryAfter);
            }
        }

        // Check TPM
        if (tpmLimit.HasValue && tpmLimit.Value > 0 && estimatedTokens > 0)
        {
            var tpmKey = $"{keyPrefix}:tpm";
            var window = _tpmWindows.GetOrAdd(tpmKey, _ => new SlidingWindow(60));
            var currentTpm = window.Total();
            if (currentTpm + estimatedTokens > tpmLimit.Value)
            {
                var retryAfter = window.SecondsUntilSlotAvailable();
                _logger.LogWarning("TPM limit exceeded for API key {ApiKeyId}: TPM {Current}+{Estimated}/{Limit}", apiKeyId, currentTpm, estimatedTokens, tpmLimit.Value);
                return (false, retryAfter);
            }
        }

        return (true, 0);
    }

    /// <summary>
    /// Record a completed request for rate limiting counters.
    /// </summary>
    public void RecordRequest(int apiKeyId, int tokensUsed)
    {
        var keyPrefix = $"key:{apiKeyId}";

        var rpmKey = $"{keyPrefix}:rpm";
        _rpmWindows.GetOrAdd(rpmKey, _ => new SlidingWindow(60)).Increment(1);

        var tpmKey = $"{keyPrefix}:tpm";
        _tpmWindows.GetOrAdd(tpmKey, _ => new SlidingWindow(60)).Increment(tokensUsed);
    }

    /// <summary>
    /// Sliding window counter using 1-second buckets.
    /// </summary>
    private class SlidingWindow
    {
        private readonly int _windowSeconds;
        private readonly ConcurrentDictionary<long, int> _buckets = new();
        private long _totalValue;

        public SlidingWindow(int windowSeconds)
        {
            _windowSeconds = windowSeconds;
        }

        private long CurrentSecond => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public void Increment(int value)
        {
            var second = CurrentSecond;
            _buckets.AddOrUpdate(second, value, (_, existing) => existing + value);
            Interlocked.Add(ref _totalValue, value);
            Cleanup();
        }

        public int Count()
        {
            Cleanup();
            var cutoff = CurrentSecond - _windowSeconds;
            return _buckets.Where(kv => kv.Key > cutoff).Sum(kv => kv.Value);
        }

        public int Total()
        {
            Cleanup();
            var cutoff = CurrentSecond - _windowSeconds;
            return _buckets.Where(kv => kv.Key > cutoff).Sum(kv => kv.Value);
        }

        public int SecondsUntilSlotAvailable()
        {
            Cleanup();
            var cutoff = CurrentSecond - _windowSeconds;
            var oldest = _buckets.Keys.Where(k => k > cutoff).DefaultIfEmpty(CurrentSecond).Min();
            return Math.Max(1, (int)(oldest + _windowSeconds - CurrentSecond));
        }

        private void Cleanup()
        {
            var cutoff = CurrentSecond - _windowSeconds - 1;
            foreach (var key in _buckets.Keys.Where(k => k <= cutoff))
            {
                _buckets.TryRemove(key, out _);
            }
        }
    }
}
