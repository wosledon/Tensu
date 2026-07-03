using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Core.Entities;

namespace Tensu.Api.Infrastructure;

/// <summary>
/// Rate limiter using sliding window counters backed by the database for cross-instance consistency.
/// </summary>
public class RateLimiter
{
    private const int WindowSeconds = 60;
    private readonly TensuDbContext _db;
    private readonly ILogger<RateLimiter> _logger;

    public RateLimiter(TensuDbContext db, ILogger<RateLimiter> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Check if a request is allowed under the rate limit.
    /// Returns (allowed, retryAfterSeconds).
    /// </summary>
    public async Task<(bool allowed, int retryAfterSeconds)> CheckRateLimitAsync(int apiKeyId, int? rpmLimit, int? tpmLimit, int estimatedTokens = 0)
    {
        var keyPrefix = $"key:{apiKeyId}";
        var now = DateTime.UtcNow;
        var cutoff = now.AddSeconds(-WindowSeconds);

        // Cleanup stale buckets before reading
        await CleanupOldBucketsAsync(keyPrefix, cutoff);

        var sum = await SumWindowAsync(keyPrefix, cutoff);

        var currentRpm = sum.GetValueOrDefault("rpm");
        if (rpmLimit.HasValue && rpmLimit.Value > 0 && currentRpm >= rpmLimit.Value)
        {
            var retryAfter = await GetSecondsUntilSlotAvailable($"{keyPrefix}:rpm", cutoff, now);
            _logger.LogWarning("Rate limit exceeded for API key {ApiKeyId}: RPM {Current}/{Limit}", apiKeyId, currentRpm, rpmLimit.Value);
            return (false, retryAfter);
        }

        var currentTpm = sum.GetValueOrDefault("tpm");
        if (tpmLimit.HasValue && tpmLimit.Value > 0 && estimatedTokens > 0 && currentTpm + estimatedTokens > tpmLimit.Value)
        {
            var retryAfter = await GetSecondsUntilSlotAvailable($"{keyPrefix}:tpm", cutoff, now);
            _logger.LogWarning("TPM limit exceeded for API key {ApiKeyId}: TPM {Current}+{Estimated}/{Limit}", apiKeyId, currentTpm, estimatedTokens, tpmLimit.Value);
            return (false, retryAfter);
        }

        return (true, 0);
    }

    /// <summary>
    /// Record a completed request for rate limiting counters.
    /// </summary>
    public void RecordRequest(int apiKeyId, int tokensUsed)
    {
        // Signature remains synchronous to keep existing callers unchanged.
        // The DB work is small and happens after the upstream response is received.
        IncrementAsync($"key:{apiKeyId}:rpm", 1).GetAwaiter().GetResult();
        IncrementAsync($"key:{apiKeyId}:tpm", tokensUsed).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Increment the per-second bucket for the given scope.
    /// </summary>
    public async Task IncrementAsync(string scope, int delta)
    {
        if (delta <= 0) return;

        var bucket = GetBucketStart(DateTime.UtcNow);
        var maxRetries = 3;
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var existing = await _db.RateLimitCounters
                    .FirstOrDefaultAsync(c => c.Scope == scope && c.WindowStart == bucket);

                if (existing != null)
                {
                    existing.Value += delta;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _db.RateLimitCounters.Add(new RateLimitCounter
                    {
                        Scope = scope,
                        WindowStart = bucket,
                        WindowSeconds = WindowSeconds,
                        Value = delta,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                await _db.SaveChangesAsync();
                return;
            }
            catch (DbUpdateException ex) when (IsUniqueConflict(ex))
            {
                _logger.LogWarning(ex, "Concurrent rate limit counter update for {Scope}, attempt {Attempt}", scope, attempt + 1);
                if (attempt < maxRetries - 1)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(10 * (attempt + 1)));
                }
            }
        }
    }

    /// <summary>
    /// Try to acquire a concurrency slot for the model. Returns true if a slot was acquired.
    /// Call ReleaseConcurrencyAsync in a finally block to release the slot.
    /// </summary>
    public async Task<bool> TryAcquireConcurrencyAsync(int modelId, int limit)
    {
        if (limit <= 0) return true;

        var scope = $"model:{modelId}:concurrent";
        var maxRetries = 3;
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var counter = await _db.RateLimitCounters
                    .FirstOrDefaultAsync(c => c.Scope == scope);

                if (counter == null)
                {
                    _db.RateLimitCounters.Add(new RateLimitCounter
                    {
                        Scope = scope,
                        WindowStart = DateTime.UtcNow.Date,
                        WindowSeconds = int.MaxValue,
                        Value = 1,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                    await _db.SaveChangesAsync();
                    return true;
                }

                if (counter.Value >= limit)
                {
                    _logger.LogWarning("Concurrency limit exceeded for model {ModelId}: {Current}/{Limit}", modelId, counter.Value, limit);
                    return false;
                }

                counter.Value += 1;
                counter.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                return true;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrent concurrency counter update for model {ModelId}, attempt {Attempt}", modelId, attempt + 1);
                if (attempt < maxRetries - 1) await Task.Delay(TimeSpan.FromMilliseconds(10 * (attempt + 1)));
            }
            catch (DbUpdateException ex) when (IsUniqueConflict(ex))
            {
                _logger.LogWarning(ex, "Concurrent concurrency counter insert for model {ModelId}, attempt {Attempt}", modelId, attempt + 1);
                if (attempt < maxRetries - 1) await Task.Delay(TimeSpan.FromMilliseconds(10 * (attempt + 1)));
            }
        }

        return false;
    }

    /// <summary>
    /// Release a concurrency slot acquired for the model.
    /// </summary>
    public async Task ReleaseConcurrencyAsync(int modelId)
    {
        var scope = $"model:{modelId}:concurrent";
        var maxRetries = 3;
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var counter = await _db.RateLimitCounters
                    .FirstOrDefaultAsync(c => c.Scope == scope);

                if (counter != null)
                {
                    counter.Value = Math.Max(0, counter.Value - 1);
                    counter.UpdatedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                }
                return;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex, "Concurrent concurrency counter release for model {ModelId}, attempt {Attempt}", modelId, attempt + 1);
                if (attempt < maxRetries - 1) await Task.Delay(TimeSpan.FromMilliseconds(10 * (attempt + 1)));
            }
        }
    }

    private async Task<Dictionary<string, long>> SumWindowAsync(string keyPrefix, DateTime cutoff)
    {
        var rpmScope = $"{keyPrefix}:rpm";
        var tpmScope = $"{keyPrefix}:tpm";

        var rpmSum = await _db.RateLimitCounters
            .Where(c => c.Scope == rpmScope && c.WindowStart >= cutoff)
            .SumAsync(c => (long?)c.Value) ?? 0;

        var tpmSum = await _db.RateLimitCounters
            .Where(c => c.Scope == tpmScope && c.WindowStart >= cutoff)
            .SumAsync(c => (long?)c.Value) ?? 0;

        return new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            ["rpm"] = rpmSum,
            ["tpm"] = tpmSum
        };
    }

    private async Task CleanupOldBucketsAsync(string keyPrefix, DateTime cutoff)
    {
        try
        {
            await _db.RateLimitCounters
                .Where(c => c.Scope.StartsWith(keyPrefix) && c.WindowStart < cutoff)
                .ExecuteDeleteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cleanup old rate limit buckets for {KeyPrefix}", keyPrefix);
        }
    }

    private async Task<int> GetSecondsUntilSlotAvailable(string scope, DateTime cutoff, DateTime now)
    {
        var oldest = await _db.RateLimitCounters
            .Where(c => c.Scope == scope && c.WindowStart >= cutoff)
            .OrderBy(c => c.WindowStart)
            .Select(c => c.WindowStart)
            .FirstOrDefaultAsync();
        if (oldest == default) return 1;
        return Math.Max(1, (int)(oldest.AddSeconds(WindowSeconds) - now).TotalSeconds + 1);
    }

    private static DateTime GetBucketStart(DateTime timestamp) =>
        new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, timestamp.Minute, timestamp.Second, DateTimeKind.Utc);

    private static bool IsUniqueConflict(DbUpdateException ex) =>
        ex.InnerException is System.Data.Common.DbException dbEx &&
        (dbEx.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
         dbEx.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
}
