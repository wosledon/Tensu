using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace Tensu.Api.Infrastructure;

/// <summary>
/// Collects and exposes Prometheus-format metrics for the gateway.
/// </summary>
public class MetricsCollector
{
    private long _totalRequests;
    private long _successRequests;
    private long _failedRequests;
    private long _cacheHits;
    private long _cacheMisses;
    private long _rateLimitedRequests;
    private long _totalTokensIn;
    private long _totalTokensOut;
    private double _totalLatencyMs;
    private readonly ConcurrentDictionary<string, long> _modelRequests = new();
    private readonly ConcurrentDictionary<string, long> _providerErrors = new();
    private readonly Stopwatch _uptime = Stopwatch.StartNew();

    public void RecordRequest(string model, string provider, bool success, bool cacheHit, long latencyMs, int tokensIn, int tokensOut, bool rateLimited)
    {
        Interlocked.Increment(ref _totalRequests);
        if (success) Interlocked.Increment(ref _successRequests);
        else Interlocked.Increment(ref _failedRequests);

        if (cacheHit) Interlocked.Increment(ref _cacheHits);
        else Interlocked.Increment(ref _cacheMisses);

        if (rateLimited) Interlocked.Increment(ref _rateLimitedRequests);

        Interlocked.Add(ref _totalTokensIn, tokensIn);
        Interlocked.Add(ref _totalTokensOut, tokensOut);
        lock (this) { _totalLatencyMs += latencyMs; }

        _modelRequests.AddOrUpdate(model, 1, (_, v) => v + 1);
        if (!success)
            _providerErrors.AddOrUpdate(provider, 1, (_, v) => v + 1);
    }

    /// <summary>
    /// Export metrics in Prometheus text format.
    /// </summary>
    public string Export()
    {
        var sb = new StringBuilder();
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        sb.AppendLine("# HELP tensu_requests_total Total number of gateway requests");
        sb.AppendLine("# TYPE tensu_requests_total counter");
        sb.AppendLine($"tensu_requests_total {Interlocked.Read(ref _totalRequests)} {ts}");

        sb.AppendLine("# HELP tensu_requests_success_total Successful requests");
        sb.AppendLine("# TYPE tensu_requests_success_total counter");
        sb.AppendLine($"tensu_requests_success_total {Interlocked.Read(ref _successRequests)} {ts}");

        sb.AppendLine("# HELP tensu_requests_failed_total Failed requests");
        sb.AppendLine("# TYPE tensu_requests_failed_total counter");
        sb.AppendLine($"tensu_requests_failed_total {Interlocked.Read(ref _failedRequests)} {ts}");

        sb.AppendLine("# HELP tensu_rate_limited_total Rate limited requests");
        sb.AppendLine("# TYPE tensu_rate_limited_total counter");
        sb.AppendLine($"tensu_rate_limited_total {Interlocked.Read(ref _rateLimitedRequests)} {ts}");

        sb.AppendLine("# HELP tensu_cache_hits_total Cache hit count");
        sb.AppendLine("# TYPE tensu_cache_hits_total counter");
        sb.AppendLine($"tensu_cache_hits_total {Interlocked.Read(ref _cacheHits)} {ts}");

        sb.AppendLine("# HELP tensu_cache_misses_total Cache miss count");
        sb.AppendLine("# TYPE tensu_cache_misses_total counter");
        sb.AppendLine($"tensu_cache_misses_total {Interlocked.Read(ref _cacheMisses)} {ts}");

        sb.AppendLine("# HELP tensu_tokens_input_total Total input tokens");
        sb.AppendLine("# TYPE tensu_tokens_input_total counter");
        sb.AppendLine($"tensu_tokens_input_total {Interlocked.Read(ref _totalTokensIn)} {ts}");

        sb.AppendLine("# HELP tensu_tokens_output_total Total output tokens");
        sb.AppendLine("# TYPE tensu_tokens_output_total counter");
        sb.AppendLine($"tensu_tokens_output_total {Interlocked.Read(ref _totalTokensOut)} {ts}");

        sb.AppendLine("# HELP tensu_latency_ms_total Cumulative latency in ms");
        sb.AppendLine("# TYPE tensu_latency_ms_total counter");
        sb.AppendLine($"tensu_latency_ms_total {Math.Round(_totalLatencyMs)} {ts}");

        sb.AppendLine("# HELP tensu_uptime_seconds Uptime in seconds");
        sb.AppendLine("# TYPE tensu_uptime_seconds gauge");
        sb.AppendLine($"tensu_uptime_seconds {_uptime.Elapsed.TotalSeconds:F0} {ts}");

        sb.AppendLine("# HELP tensu_requests_by_model Requests per model");
        sb.AppendLine("# TYPE tensu_requests_by_model counter");
        foreach (var kv in _modelRequests.OrderBy(k => k.Key))
            sb.AppendLine($"tensu_requests_by_model{{model=\"{kv.Key}\"}} {kv.Value} {ts}");

        sb.AppendLine("# HELP tensu_errors_by_provider Errors per provider");
        sb.AppendLine("# TYPE tensu_errors_by_provider counter");
        foreach (var kv in _providerErrors.OrderBy(k => k.Key))
            sb.AppendLine($"tensu_errors_by_provider{{provider=\"{kv.Key}\"}} {kv.Value} {ts}");

        return sb.ToString();
    }
}
