using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Api.Infrastructure;
using Tensu.Core.Enums;

namespace Tensu.Api.Services;

public record AnomalyResult(
    string Type,
    string Severity,
    string Dimension,
    string Message,
    double CurrentValue,
    double BaselineValue,
    DateTime DetectedAt
);

public class AnomalyDetectionService
{
    private readonly TensuDbContext _db;
    private readonly SettingsService _settings;
    private readonly AuditChannel? _auditChannel;
    private readonly NotificationService? _notificationService;

    public AnomalyDetectionService(TensuDbContext db, SettingsService settings, AuditChannel? auditChannel = null, NotificationService? notificationService = null)
    {
        _db = db;
        _settings = settings;
        _auditChannel = auditChannel;
        _notificationService = notificationService;
    }

    public async Task<List<AnomalyResult>> DetectAsync(DateTime from, DateTime to, int? orgId = null)
    {
        from = from.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(from, DateTimeKind.Utc) : from.ToUniversalTime();
        to = to.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(to, DateTimeKind.Utc) : to.ToUniversalTime();

        var baselineFrom = from.AddDays(-7);
        var baselineTo = to.AddDays(-7);

        var logs = await FetchLogsAsync(from, to, orgId);
        var baselineLogs = await FetchLogsAsync(baselineFrom, baselineTo, orgId);

        var thresholds = await LoadThresholdsAsync();
        var anomalies = new List<AnomalyResult>();

        DetectUsageAndCost(logs, baselineLogs, thresholds, anomalies);
        DetectPerformance(logs, baselineLogs, thresholds, anomalies);
        DetectProviderDegradation(logs, baselineLogs, thresholds, anomalies);

        anomalies = anomalies.OrderByDescending(a => a.Severity).ThenBy(a => a.DetectedAt).ToList();

        await WriteHighSeverityAuditsAsync(anomalies, orgId);

        if (_notificationService != null && anomalies.Any(a => a.Severity is "High" or "Critical"))
        {
            await _notificationService.NotifyAsync("anomaly.detected", new { anomalies = anomalies.Where(a => a.Severity is "High" or "Critical"), detectedAt = DateTime.UtcNow });
        }

        return anomalies;
    }

    private async Task<List<Core.Entities.RequestLog>> FetchLogsAsync(DateTime from, DateTime to, int? orgId)
    {
        var query = _db.RequestLogs.AsNoTracking().Where(r => r.Timestamp >= from && r.Timestamp <= to);
        if (orgId.HasValue) query = query.Where(r => r.OrganizationId == orgId.Value);
        return await query.ToListAsync();
    }

    private async Task<Thresholds> LoadThresholdsAsync()
    {
        async Task<double> Get(string key, double fallback)
        {
            var value = await _settings.GetAsync(key);
            return double.TryParse(value, out var parsed) ? parsed : fallback;
        }

        return new Thresholds
        {
            UsageSpikeMultiplier = await Get("anomaly.usageSpikeMultiplier", 2.0),
            UsageDropMultiplier = await Get("anomaly.usageDropMultiplier", 0.5),
            CostSpikeMultiplier = await Get("anomaly.costSpikeMultiplier", 2.0),
            LatencySpikeMultiplier = await Get("anomaly.latencySpikeMultiplier", 2.0),
            ErrorRateThreshold = await Get("anomaly.errorRateThreshold", 0.1),
            RateLimitThreshold = await Get("anomaly.rateLimitThreshold", 0.05),
            ProviderSuccessRateThreshold = await Get("anomaly.providerSuccessRateThreshold", 0.95)
        };
    }

    private static void DetectUsageAndCost(
        List<Core.Entities.RequestLog> logs,
        List<Core.Entities.RequestLog> baselineLogs,
        Thresholds thresholds,
        List<AnomalyResult> anomalies)
    {
        var dimensions = new (string Prefix, Func<Core.Entities.RequestLog, string> Selector)[]
        {
            ("org", r => $"org:{r.OrganizationId ?? 0}"),
            ("model", r => $"model:{r.ModelName}"),
            ("key", r => $"key:{r.ApiKeyId ?? 0}")
        };

        foreach (var (prefix, selector) in dimensions)
        {
            var currentByDim = logs.GroupBy(selector).ToDictionary(g => g.Key, g => g.ToList());
            var baselineByDim = baselineLogs.GroupBy(selector).ToDictionary(g => g.Key, g => g.ToList());
            var allKeys = currentByDim.Keys.Union(baselineByDim.Keys).Distinct();

            foreach (var key in allKeys)
            {
                var currentList = currentByDim.TryGetValue(key, out var c) ? c : [];
                var baselineList = baselineByDim.TryGetValue(key, out var b) ? b : [];

                var currentRequests = currentList.Count;
                var baselineRequests = baselineList.Count;
                var currentTokens = currentList.Sum(r => (r.InputTokens ?? 0) + (r.OutputTokens ?? 0));
                var baselineTokens = baselineList.Sum(r => (r.InputTokens ?? 0) + (r.OutputTokens ?? 0));
                var currentCost = (double)currentList.Sum(r => (r.InputCost ?? 0) + (r.OutputCost ?? 0));
                var baselineCost = (double)baselineList.Sum(r => (r.InputCost ?? 0) + (r.OutputCost ?? 0));

                if (baselineRequests > 0)
                {
                    var currentRate = currentRequests;
                    var baselineRate = baselineRequests / 7.0;
                    var (severity, message) = EvaluateSpike(currentRate, baselineRate, thresholds.UsageSpikeMultiplier, $"{prefix} usage");
                    if (severity != null)
                    {
                        anomalies.Add(new AnomalyResult(
                            "UsageSpike",
                            severity,
                            key,
                            message,
                            currentRate,
                            baselineRate,
                            DateTime.UtcNow));
                    }
                    else
                    {
                        (severity, message) = EvaluateDrop(currentRate, baselineRate, thresholds.UsageDropMultiplier, $"{prefix} usage");
                        if (severity != null)
                        {
                            anomalies.Add(new AnomalyResult(
                                "UsageDrop",
                                severity,
                                key,
                                message,
                                currentRate,
                                baselineRate,
                                DateTime.UtcNow));
                        }
                    }
                }
                else if (currentRequests > 10)
                {
                    anomalies.Add(new AnomalyResult(
                        "UsageSpike",
                        "Medium",
                        key,
                        $"{prefix} usage increased from no baseline to {currentRequests} requests",
                        currentRequests,
                        0,
                        DateTime.UtcNow));
                }

                if (baselineCost > 0)
                {
                    var (severity, message) = EvaluateSpike(currentCost, baselineCost, thresholds.CostSpikeMultiplier, $"{prefix} cost");
                    if (severity != null)
                    {
                        anomalies.Add(new AnomalyResult(
                            "CostSpike",
                            severity,
                            key,
                            message,
                            currentCost,
                            baselineCost,
                            DateTime.UtcNow));
                    }
                }

                if (baselineTokens > 0)
                {
                    var (severity, message) = EvaluateSpike(currentTokens, baselineTokens, thresholds.UsageSpikeMultiplier, $"{prefix} tokens");
                    if (severity != null)
                    {
                        anomalies.Add(new AnomalyResult(
                            "TokenSpike",
                            severity,
                            key,
                            message,
                            currentTokens,
                            baselineTokens,
                            DateTime.UtcNow));
                    }
                    else
                    {
                        (severity, message) = EvaluateDrop(currentTokens, baselineTokens, thresholds.UsageDropMultiplier, $"{prefix} tokens");
                        if (severity != null)
                        {
                            anomalies.Add(new AnomalyResult(
                                "TokenDrop",
                                severity,
                                key,
                                message,
                                currentTokens,
                                baselineTokens,
                                DateTime.UtcNow));
                        }
                    }
                }
                else if (currentTokens > 10000)
                {
                    anomalies.Add(new AnomalyResult(
                        "TokenSpike",
                        "Medium",
                        key,
                        $"{prefix} tokens increased from no baseline to {currentTokens}",
                        currentTokens,
                        0,
                        DateTime.UtcNow));
                }
            }
        }
    }

    private static void DetectPerformance(
        List<Core.Entities.RequestLog> logs,
        List<Core.Entities.RequestLog> baselineLogs,
        Thresholds thresholds,
        List<AnomalyResult> anomalies)
    {
        var dimensions = new (string Prefix, Func<Core.Entities.RequestLog, string> Selector)[]
        {
            ("model", r => $"model:{r.ModelName}"),
            ("provider", r => $"provider:{r.ProviderName ?? "unknown"}")
        };

        foreach (var (prefix, selector) in dimensions)
        {
            var currentByDim = logs.GroupBy(selector).ToDictionary(g => g.Key, g => g.ToList());
            var baselineByDim = baselineLogs.GroupBy(selector).ToDictionary(g => g.Key, g => g.ToList());
            var allKeys = currentByDim.Keys.Union(baselineByDim.Keys).Distinct();

            foreach (var key in allKeys)
            {
                var currentList = currentByDim.TryGetValue(key, out var c) ? c : [];
                var baselineList = baselineByDim.TryGetValue(key, out var b) ? b : [];

                var currentLatencies = currentList.Where(r => r.TotalDurationMs.HasValue).Select(r => (double)r.TotalDurationMs!.Value).ToList();
                var baselineLatencies = baselineList.Where(r => r.TotalDurationMs.HasValue).Select(r => (double)r.TotalDurationMs!.Value).ToList();

                var currentAvgLatency = currentLatencies.Count > 0 ? currentLatencies.Average() : 0;
                var baselineAvgLatency = baselineLatencies.Count > 0 ? baselineLatencies.Average() : 0;
                var currentP95 = currentLatencies.Count > 0 ? Percentile(currentLatencies, 95) : 0;
                var baselineP95 = baselineLatencies.Count > 0 ? Percentile(baselineLatencies, 95) : 0;

                if (baselineAvgLatency > 0 && currentAvgLatency > baselineAvgLatency * thresholds.LatencySpikeMultiplier)
                {
                    anomalies.Add(new AnomalyResult(
                        "LatencySpike",
                        SeverityForMultiplier(currentAvgLatency / baselineAvgLatency),
                        key,
                        $"{prefix} average latency spiked to {currentAvgLatency:F1}ms (baseline {baselineAvgLatency:F1}ms)",
                        currentAvgLatency,
                        baselineAvgLatency,
                        DateTime.UtcNow));
                }

                if (baselineP95 > 0 && currentP95 > baselineP95 * thresholds.LatencySpikeMultiplier)
                {
                    anomalies.Add(new AnomalyResult(
                        "LatencySpike",
                        SeverityForMultiplier(currentP95 / baselineP95),
                        key,
                        $"{prefix} P95 latency spiked to {currentP95:F1}ms (baseline {baselineP95:F1}ms)",
                        currentP95,
                        baselineP95,
                        DateTime.UtcNow));
                }

                var currentTotal = currentList.Count;
                var currentErrors = currentList.Count(r => r.Status == RequestStatus.Failed || r.Status == RequestStatus.Timeout);
                var currentErrorRate = currentTotal > 0 ? (double)currentErrors / currentTotal : 0;

                if (currentErrorRate >= thresholds.ErrorRateThreshold && currentTotal >= 10)
                {
                    anomalies.Add(new AnomalyResult(
                        "ErrorRateSpike",
                        currentErrorRate >= 0.2 ? "Critical" : "High",
                        key,
                        $"{prefix} error rate reached {currentErrorRate:P1} (threshold {thresholds.ErrorRateThreshold:P1})",
                        currentErrorRate,
                        thresholds.ErrorRateThreshold,
                        DateTime.UtcNow));
                }

                var currentRateLimited = currentList.Count(r => r.Status == RequestStatus.RateLimited);
                var currentRateLimitRate = currentTotal > 0 ? (double)currentRateLimited / currentTotal : 0;

                if (currentRateLimitRate >= thresholds.RateLimitThreshold && currentTotal >= 10)
                {
                    anomalies.Add(new AnomalyResult(
                        "RateLimitSpike",
                        currentRateLimitRate >= 0.1 ? "High" : "Medium",
                        key,
                        $"{prefix} rate-limit rate reached {currentRateLimitRate:P1} (threshold {thresholds.RateLimitThreshold:P1})",
                        currentRateLimitRate,
                        thresholds.RateLimitThreshold,
                        DateTime.UtcNow));
                }
            }
        }
    }

    private static void DetectProviderDegradation(
        List<Core.Entities.RequestLog> logs,
        List<Core.Entities.RequestLog> baselineLogs,
        Thresholds thresholds,
        List<AnomalyResult> anomalies)
    {
        var providers = logs.Select(r => r.ProviderName ?? "unknown").Union(baselineLogs.Select(r => r.ProviderName ?? "unknown")).Distinct();

        foreach (var provider in providers)
        {
            var currentList = logs.Where(r => (r.ProviderName ?? "unknown") == provider).ToList();
            var baselineList = baselineLogs.Where(r => (r.ProviderName ?? "unknown") == provider).ToList();

            var currentTotal = currentList.Count;
            var baselineTotal = baselineList.Count;
            if (currentTotal == 0) continue;

            var currentSuccess = currentList.Count(r => r.Status == RequestStatus.Success);
            var currentSuccessRate = (double)currentSuccess / currentTotal;

            if (currentSuccessRate < thresholds.ProviderSuccessRateThreshold && currentTotal >= 10)
            {
                anomalies.Add(new AnomalyResult(
                    "ProviderDegraded",
                    currentSuccessRate < 0.8 ? "Critical" : "High",
                    $"provider:{provider}",
                    $"Provider {provider} success rate dropped to {currentSuccessRate:P1} (threshold {thresholds.ProviderSuccessRateThreshold:P1})",
                    currentSuccessRate,
                    thresholds.ProviderSuccessRateThreshold,
                    DateTime.UtcNow));
            }

            var currentLatencies = currentList.Where(r => r.TotalDurationMs.HasValue).Select(r => (double)r.TotalDurationMs!.Value).ToList();
            var baselineLatencies = baselineList.Where(r => r.TotalDurationMs.HasValue).Select(r => (double)r.TotalDurationMs!.Value).ToList();
            var currentAvgLatency = currentLatencies.Count > 0 ? currentLatencies.Average() : 0;
            var baselineAvgLatency = baselineLatencies.Count > 0 ? baselineLatencies.Average() : 0;

            if (baselineAvgLatency > 0 && currentAvgLatency > baselineAvgLatency * thresholds.LatencySpikeMultiplier && currentTotal >= 10)
            {
                anomalies.Add(new AnomalyResult(
                    "ProviderDegraded",
                    SeverityForMultiplier(currentAvgLatency / baselineAvgLatency),
                    $"provider:{provider}",
                    $"Provider {provider} average latency increased to {currentAvgLatency:F1}ms (baseline {baselineAvgLatency:F1}ms)",
                    currentAvgLatency,
                    baselineAvgLatency,
                    DateTime.UtcNow));
            }
        }
    }

    private static (string? Severity, string Message) EvaluateSpike(double current, double baseline, double multiplier, string label)
    {
        if (baseline <= 0 || current <= baseline * multiplier) return (null, string.Empty);
        var ratio = current / baseline;
        var severity = ratio >= 4 ? "Critical" : ratio >= 2 ? "High" : "Medium";
        return (severity, $"{label} spiked to {current:F1} (baseline {baseline:F1}, x{ratio:F2})");
    }

    private static (string? Severity, string Message) EvaluateDrop(double current, double baseline, double multiplier, string label)
    {
        if (baseline <= 0 || current >= baseline * multiplier) return (null, string.Empty);
        var ratio = current / baseline;
        var severity = ratio <= 0.25 ? "Critical" : ratio <= 0.5 ? "High" : "Medium";
        return (severity, $"{label} dropped to {current:F1} (baseline {baseline:F1}, x{ratio:F2})");
    }

    private static string SeverityForMultiplier(double ratio)
    {
        return ratio >= 4 ? "Critical" : ratio >= 2 ? "High" : "Medium";
    }

    private static double Percentile(List<double> values, int percentile)
    {
        if (values.Count == 0) return 0;
        var sorted = values.OrderBy(x => x).ToList();
        var index = (percentile / 100.0) * (sorted.Count - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        if (lower == upper) return sorted[lower];
        var weight = index - lower;
        return sorted[lower] * (1 - weight) + sorted[upper] * weight;
    }

    private async Task WriteHighSeverityAuditsAsync(List<AnomalyResult> anomalies, int? orgId)
    {
        if (_auditChannel == null) return;

        foreach (var anomaly in anomalies.Where(a => a.Severity is "High" or "Critical"))
        {
            var log = new Core.Entities.RequestLog
            {
                RequestId = Guid.NewGuid().ToString("N"),
                Timestamp = DateTime.UtcNow,
                ModelName = "system:anomaly",
                Status = RequestStatus.Success,
                OrganizationId = orgId,
                ResponseContent = System.Text.Json.JsonSerializer.Serialize(anomaly),
                ProviderName = anomaly.Dimension.StartsWith("provider:") ? anomaly.Dimension["provider:".Length..] : null
            };
            await _auditChannel.EnqueueAsync(log);
        }
    }

    private class Thresholds
    {
        public double UsageSpikeMultiplier { get; set; }
        public double UsageDropMultiplier { get; set; }
        public double CostSpikeMultiplier { get; set; }
        public double LatencySpikeMultiplier { get; set; }
        public double ErrorRateThreshold { get; set; }
        public double RateLimitThreshold { get; set; }
        public double ProviderSuccessRateThreshold { get; set; }
    }
}
