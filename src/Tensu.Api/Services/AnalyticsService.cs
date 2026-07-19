using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;

namespace Tensu.Api.Services;

public class AnalyticsService
{
    private readonly TensuDbContext _db;
    private readonly SettingsService? _settings;

    public AnalyticsService(TensuDbContext db)
    {
        _db = db;
    }

    public AnalyticsService(TensuDbContext db, SettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    /// <summary>
    /// Currency conversion: rate[X] = units of X per 1 USD (e.g. CNY = 7.2).
    /// Returns (defaultCurrency, rates). Rates always contain USD = 1.
    /// </summary>
    private async Task<(string defaultCurrency, Dictionary<string, decimal> rates)> GetCurrencyConfigAsync()
    {
        var defaultCurrency = "USD";
        var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["USD"] = 1m };

        if (_settings != null)
        {
            var all = await _settings.GetAllAsync();
            if (all.TryGetValue("currency.default", out var d) && !string.IsNullOrWhiteSpace(d))
                defaultCurrency = d.Trim().ToUpperInvariant();

            foreach (var kv in all)
            {
                if (kv.Key.StartsWith("currency.rate.", StringComparison.OrdinalIgnoreCase) &&
                    decimal.TryParse(kv.Value, out var rate) && rate > 0)
                {
                    rates[kv.Key["currency.rate.".Length..].Trim().ToUpperInvariant()] = rate;
                }
            }
        }

        if (!rates.ContainsKey(defaultCurrency))
            rates[defaultCurrency] = 1m;

        return (defaultCurrency, rates);
    }

    /// <summary>
    /// Converts an amount from the given currency to the default currency.
    /// </summary>
    private static decimal ConvertCurrency(decimal amount, string? fromCurrency, string defaultCurrency, Dictionary<string, decimal> rates)
    {
        var from = string.IsNullOrWhiteSpace(fromCurrency) ? defaultCurrency : fromCurrency.Trim().ToUpperInvariant();
        if (from == defaultCurrency) return amount;
        if (!rates.TryGetValue(from, out var fromRate) || fromRate <= 0) return amount;
        var defaultRate = rates[defaultCurrency];
        return amount / fromRate * defaultRate;
    }

    public async Task<object> GetDashboardAsync(int? orgId = null)
    {
        var today = DateTime.UtcNow.Date;
        var weekAgo = today.AddDays(-7);

        var todayQuery = _db.RequestLogs.Where(r => r.Timestamp >= today);
        if (orgId.HasValue) todayQuery = todayQuery.Where(r => r.OrganizationId == orgId.Value);
        var todayLogs = await todayQuery.ToListAsync();

        var dailyStatsQuery = _db.DailyStats.Where(s => s.Date >= weekAgo && s.Date < today);
        if (orgId.HasValue) dailyStatsQuery = dailyStatsQuery.Where(s => s.OrganizationId == orgId.Value);
        var dailyStats = await dailyStatsQuery.ToListAsync();

        var dailyTrend = dailyStats
            .GroupBy(s => s.Date)
            .Select(g => new
            {
                date = g.Key.ToString("MM-dd"),
                requests = g.Sum(s => s.TotalRequests),
                tokens = (int)(g.Sum(s => s.TotalInputTokens) + g.Sum(s => s.TotalOutputTokens)),
                cost = g.Sum(s => s.TotalInputCost + s.TotalOutputCost),
                successRate = g.Sum(s => s.TotalRequests) > 0
                    ? Math.Round((double)g.Sum(s => s.SuccessRequests) / g.Sum(s => s.TotalRequests) * 100, 1) : 0,
                avgLatency = g.Where(s => s.AvgLatencyMs.HasValue).Select(s => (double)s.AvgLatencyMs!.Value).DefaultIfEmpty(0).Average()
            })
            .OrderBy(x => x.date)
            .ToList();

        var modelDistribution = dailyStats
            .GroupBy(s => s.ModelName)
            .Select(g => new { name = g.Key ?? "unknown", value = g.Sum(s => s.TotalRequests) })
            .OrderByDescending(x => x.value)
            .Take(8)
            .ToList();

        var providers = await _db.Providers
            .Select(p => new { p.Name, p.HealthStatus, p.IsEnabled })
            .ToListAsync();

        return new
        {
            today = new
            {
                requests = todayLogs.Count,
                tokens = todayLogs.Sum(r => (r.InputTokens ?? 0) + (r.OutputTokens ?? 0)),
                cost = Math.Round(todayLogs.Sum(r => (r.InputCost ?? 0) + (r.OutputCost ?? 0)), 4),
                cacheHitRate = todayLogs.Count > 0 ? Math.Round((double)todayLogs.Count(r => r.CacheHit) / todayLogs.Count * 100, 1) : 0,
                avgLatency = todayLogs.Where(r => r.TotalDurationMs.HasValue).Select(r => (double)r.TotalDurationMs!.Value).DefaultIfEmpty(0).Average(),
                successRate = todayLogs.Count > 0 ? Math.Round((double)todayLogs.Count(r => r.Status == Core.Enums.RequestStatus.Success) / todayLogs.Count * 100, 1) : 0,
            },
            dailyTrend,
            modelDistribution,
            providers
        };
    }

    public async Task<object> GetHealthAsync(int? orgId = null)
    {
        var today = DateTime.UtcNow.Date;

        var query = _db.RequestLogs.Where(r => r.Timestamp >= today);
        if (orgId.HasValue) query = query.Where(r => r.OrganizationId == orgId.Value);

        var todayLogs = await query.ToListAsync();

        var total = todayLogs.Count;
        var success = todayLogs.Count(r => r.Status == Core.Enums.RequestStatus.Success);
        var failed = todayLogs.Count(r => r.Status == Core.Enums.RequestStatus.Failed);
        var timeout = todayLogs.Count(r => r.Status == Core.Enums.RequestStatus.Timeout);
        var interrupted = todayLogs.Count(r => r.Status == Core.Enums.RequestStatus.Interrupted);
        var rateLimited = todayLogs.Count(r => r.Status == Core.Enums.RequestStatus.RateLimited);
        var cacheHits = todayLogs.Count(r => r.CacheHit);

        var errorRate = total > 0 ? (double)(failed + timeout) / total : 0;
        var rateLimitRate = total > 0 ? (double)rateLimited / total : 0;
        var cacheHitRate = total > 0 ? (double)cacheHits / total : 0;
        var avgLatency = todayLogs.Where(r => r.TotalDurationMs.HasValue).Select(r => (double)r.TotalDurationMs!.Value).DefaultIfEmpty(0).Average();
        var p95Latency = Percentile(todayLogs.Where(r => r.TotalDurationMs.HasValue).Select(r => (double)r.TotalDurationMs!.Value).DefaultIfEmpty(0).ToList(), 95);
        var p99Latency = Percentile(todayLogs.Where(r => r.TotalDurationMs.HasValue).Select(r => (double)r.TotalDurationMs!.Value).DefaultIfEmpty(0).ToList(), 99);

        var providerNames = todayLogs.Select(r => r.ProviderName ?? "unknown").Distinct();
        var providerEntities = await _db.Providers.AsNoTracking().ToListAsync();

        var providers = providerNames.Select(name =>
        {
            var logs = todayLogs.Where(r => (r.ProviderName ?? "unknown") == name).ToList();
            var pEntity = providerEntities.FirstOrDefault(p => p.Name == name);
            var pTotal = logs.Count;
            var pSuccess = pTotal > 0 ? (double)logs.Count(r => r.Status == Core.Enums.RequestStatus.Success) / pTotal : 0;
            var pLatency = logs.Where(r => r.TotalDurationMs.HasValue).Select(r => (double)r.TotalDurationMs!.Value).DefaultIfEmpty(0).Average();

            return new
            {
                name,
                healthStatus = pEntity?.HealthStatus.ToString() ?? "Unknown",
                isEnabled = pEntity?.IsEnabled ?? true,
                successRate = pSuccess,
                avgLatency = pLatency,
                lastHealthCheckAt = pEntity?.LastHealthCheckAt,
                requests = pTotal
            };
        }).OrderByDescending(p => p.requests).ToList();

        var topModels = todayLogs
            .GroupBy(r => r.ModelName)
            .Select(g => new { model = g.Key, requests = g.Count() })
            .OrderByDescending(x => x.requests)
            .Take(5)
            .ToList();

        var topApiKeys = todayLogs
            .Where(r => r.ApiKeyId.HasValue)
            .GroupBy(r => r.ApiKeyId!.Value)
            .Select(g => new { apiKeyId = g.Key, requests = g.Count() })
            .OrderByDescending(x => x.requests)
            .Take(5)
            .ToList();

        return new
        {
            today = new
            {
                totalRequests = total,
                success,
                failed,
                timeout,
                interrupted,
                rateLimited,
                errorRate = Math.Round(errorRate * 100, 2),
                rateLimitRate = Math.Round(rateLimitRate * 100, 2),
                cacheHitRate = Math.Round(cacheHitRate * 100, 2),
                avgLatency = Math.Round(avgLatency, 1),
                p95Latency = Math.Round(p95Latency, 1),
                p99Latency = Math.Round(p99Latency, 1)
            },
            providers,
            topModels,
            topApiKeys
        };
    }

    public async Task<object> GetUsageAsync(DateTime from, DateTime to, string granularity = "day", int? orgId = null)
    {
        if (granularity == "day" || granularity == "week" || granularity == "month")
        {
            var statsQuery = _db.DailyStats.Where(s => s.Date >= from.Date && s.Date <= to.Date);
            if (orgId.HasValue) statsQuery = statsQuery.Where(s => s.OrganizationId == orgId.Value);
            var stats = await statsQuery.ToListAsync();

            if (stats.Any())
            {
                var grouped = granularity switch
                {
                    "week" => stats.GroupBy(s => s.Date.AddDays(-(int)s.Date.DayOfWeek)),
                    "month" => stats.GroupBy(s => new DateTime(s.Date.Year, s.Date.Month, 1)),
                    _ => stats.GroupBy(s => s.Date)
                };

                return grouped.Select(g => new
                {
                    time = g.Key.ToString(granularity == "hour" ? "MM-dd HH:mm" : "yyyy-MM-dd"),
                    inputTokens = (long)g.Sum(s => s.TotalInputTokens),
                    outputTokens = (long)g.Sum(s => s.TotalOutputTokens),
                    totalTokens = (long)(g.Sum(s => s.TotalInputTokens) + g.Sum(s => s.TotalOutputTokens)),
                    requests = g.Sum(s => s.TotalRequests),
                    success = g.Sum(s => s.SuccessRequests),
                    failed = g.Sum(s => s.FailedRequests),
                    cacheHits = g.Sum(s => s.CacheHits),
                    compressionSaved = (long)(g.Sum(s => s.TotalInputTokens) - g.Sum(s => s.TotalInputTokensAfterCompression)),
                }).OrderBy(x => x.time).ToList();
            }
        }

        var query = _db.RequestLogs.Where(r => r.Timestamp >= from && r.Timestamp <= to);
        if (orgId.HasValue) query = query.Where(r => r.OrganizationId == orgId.Value);

        var logs = await query.ToListAsync();

        var logGrouped = granularity switch
        {
            "minute" => logs.GroupBy(r => new DateTime(r.Timestamp.Year, r.Timestamp.Month, r.Timestamp.Day, r.Timestamp.Hour, r.Timestamp.Minute, 0)),
            "hour" => logs.GroupBy(r => new DateTime(r.Timestamp.Year, r.Timestamp.Month, r.Timestamp.Day, r.Timestamp.Hour, 0, 0)),
            "week" => logs.GroupBy(r => r.Timestamp.Date.AddDays(-(int)r.Timestamp.DayOfWeek)),
            "month" => logs.GroupBy(r => new DateTime(r.Timestamp.Year, r.Timestamp.Month, 1)),
            _ => logs.GroupBy(r => r.Timestamp.Date)
        };

        return logGrouped.Select(g => new
        {
            time = g.Key.ToString(granularity is "minute" or "hour" ? "MM-dd HH:mm" : "yyyy-MM-dd"),
            inputTokens = g.Sum(r => r.InputTokens ?? 0),
            outputTokens = g.Sum(r => r.OutputTokens ?? 0),
            totalTokens = g.Sum(r => (r.InputTokens ?? 0) + (r.OutputTokens ?? 0)),
            requests = g.Count(),
            success = g.Count(r => r.Status == Core.Enums.RequestStatus.Success),
            failed = g.Count(r => r.Status == Core.Enums.RequestStatus.Failed),
            cacheHits = g.Count(r => r.CacheHit),
            compressionSaved = g.Sum(r => (r.InputTokens ?? 0) - (r.InputTokensAfterCompression ?? r.InputTokens ?? 0)),
        }).OrderBy(x => x.time).ToList();
    }

    public async Task<object> GetCostAsync(DateTime from, DateTime to, int? orgId = null)
    {
        var statsQuery = _db.DailyStats.Where(s => s.Date >= from.Date && s.Date <= to.Date);
        if (orgId.HasValue) statsQuery = statsQuery.Where(s => s.OrganizationId == orgId.Value);
        var stats = await statsQuery.ToListAsync();

        if (stats.Any())
        {
            var byModel = stats
                .GroupBy(s => s.ModelName ?? "unknown")
                .Select(g => new
                {
                    model = g.Key,
                    totalCost = Math.Round(g.Sum(s => s.TotalInputCost + s.TotalOutputCost), 6),
                    requests = g.Sum(s => s.TotalRequests),
                    avgCostPerRequest = g.Sum(s => s.TotalRequests) > 0
                        ? Math.Round(g.Sum(s => s.TotalInputCost + s.TotalOutputCost) / g.Sum(s => s.TotalRequests), 6) : 0,
                })
                .OrderByDescending(x => x.totalCost)
                .ToList();

            var byProvider = stats
                .GroupBy(s => s.ProviderName ?? "unknown")
                .Select(g => new
                {
                    provider = g.Key,
                    totalCost = Math.Round(g.Sum(s => s.TotalInputCost + s.TotalOutputCost), 6),
                    requests = g.Sum(s => s.TotalRequests),
                })
                .OrderByDescending(x => x.totalCost)
                .ToList();

            var daily = stats
                .GroupBy(s => s.Date)
                .Select(g => new
                {
                    date = g.Key.ToString("yyyy-MM-dd"),
                    cost = Math.Round(g.Sum(s => s.TotalInputCost + s.TotalOutputCost), 6),
                })
                .OrderBy(x => x.date)
                .ToList();

            var totalCost = Math.Round(stats.Sum(s => s.TotalInputCost + s.TotalOutputCost), 6);
            var totalRequests = stats.Sum(s => s.TotalRequests);

            // Compression cost comparison: per-row saved input share applied to input cost.
            var statsCompressionSavedTokens = stats.Sum(s => s.TotalInputTokens - s.TotalInputTokensAfterCompression);
            var statsCompressionSavings = Math.Round(stats.Sum(s =>
                s.TotalInputTokens > 0
                    ? s.TotalInputCost * (s.TotalInputTokens - s.TotalInputTokensAfterCompression) / s.TotalInputTokens
                    : 0m), 6);

            return new
            {
                totalCost,
                totalRequests,
                avgCostPerRequest = totalRequests > 0 ? Math.Round(totalCost / totalRequests, 6) : 0,
                compressionSavings = statsCompressionSavings,
                compressionSavedTokens = statsCompressionSavedTokens,
                byModel,
                byProvider,
                daily,
                forecastNext7Days = ForecastNext7Days(daily.Select(d => (d.date, d.cost)).ToList()),
                // Aggregated stats do not retain per-row currency, so no conversion is possible here.
                currencyConversionApplied = false,
            };
        }

        var query = _db.RequestLogs.Where(r => r.Timestamp >= from && r.Timestamp <= to);
        if (orgId.HasValue) query = query.Where(r => r.OrganizationId == orgId.Value);

        var logs = await query.ToListAsync();
        var (defaultCurrency, rates) = await GetCurrencyConfigAsync();

        var logByModel = logs
            .GroupBy(r => r.ModelName)
            .Select(g => new
            {
                model = g.Key,
                totalCost = Math.Round(g.Sum(r => (r.InputCost ?? 0) + (r.OutputCost ?? 0)), 6),
                requests = g.Count(),
                avgCostPerRequest = g.Count() > 0 ? Math.Round(g.Sum(r => (r.InputCost ?? 0) + (r.OutputCost ?? 0)) / g.Count(), 6) : 0,
            })
            .OrderByDescending(x => x.totalCost)
            .ToList();

        var logByProvider = logs
            .GroupBy(r => r.ProviderName ?? "unknown")
            .Select(g => new
            {
                provider = g.Key,
                totalCost = Math.Round(g.Sum(r => (r.InputCost ?? 0) + (r.OutputCost ?? 0)), 6),
                requests = g.Count(),
            })
            .OrderByDescending(x => x.totalCost)
            .ToList();

        var logDaily = logs
            .GroupBy(r => r.Timestamp.Date)
            .Select(g => new
            {
                date = g.Key.ToString("yyyy-MM-dd"),
                cost = Math.Round(g.Sum(r => (r.InputCost ?? 0) + (r.OutputCost ?? 0)), 6),
            })
            .OrderBy(x => x.date)
            .ToList();

        var logByCurrency = logs
            .GroupBy(r => string.IsNullOrWhiteSpace(r.Currency) ? defaultCurrency : r.Currency!.ToUpperInvariant())
            .Select(g => new
            {
                currency = g.Key,
                totalCost = Math.Round(g.Sum(r => (r.InputCost ?? 0) + (r.OutputCost ?? 0)), 6),
                requests = g.Count(),
            })
            .OrderByDescending(x => x.totalCost)
            .ToList();

        var convertedTotal = Math.Round(logs.Sum(r =>
            ConvertCurrency((r.InputCost ?? 0) + (r.OutputCost ?? 0), r.Currency, defaultCurrency, rates)), 6);

        // Compression cost comparison: saved input tokens share of input cost,
        // converted to the default currency (PRD §3.6.2 压缩成本对比).
        var compressionSavedTokens = logs.Sum(r =>
            (r.InputTokens ?? 0) - (r.InputTokensAfterCompression ?? r.InputTokens ?? 0));
        var compressionSavings = Math.Round(logs.Sum(r =>
        {
            var input = r.InputTokens ?? 0;
            var after = r.InputTokensAfterCompression ?? input;
            if (input <= 0 || after >= input) return 0m;
            var saved = (r.InputCost ?? 0) * (input - after) / input;
            return ConvertCurrency(saved, r.Currency, defaultCurrency, rates);
        }), 6);

        return new
        {
            totalCost = Math.Round(logs.Sum(r => (r.InputCost ?? 0) + (r.OutputCost ?? 0)), 6),
            totalRequests = logs.Count,
            avgCostPerRequest = logs.Count > 0 ? Math.Round(logs.Sum(r => (r.InputCost ?? 0) + (r.OutputCost ?? 0)) / logs.Count, 6) : 0,
            compressionSavings,
            compressionSavedTokens,
            byModel = logByModel,
            byProvider = logByProvider,
            daily = logDaily,
            forecastNext7Days = ForecastNext7Days(logDaily.Select(d => (d.date, d.cost)).ToList()),
            defaultCurrency,
            totalCostInDefaultCurrency = convertedTotal,
            byCurrency = logByCurrency,
            currencyConversionApplied = true,
        };
    }

    /// <summary>
    /// Projects the next 7 days of cost from the daily series using least-squares linear
    /// regression. Returns null when fewer than 2 data points exist.
    /// </summary>
    private static decimal? ForecastNext7Days(List<(string date, decimal cost)> daily)
    {
        if (daily.Count < 2) return null;

        var n = daily.Count;
        var xs = Enumerable.Range(0, n).Select(i => (double)i).ToArray();
        var ys = daily.Select(d => (double)d.cost).ToArray();

        var meanX = xs.Average();
        var meanY = ys.Average();
        var numerator = xs.Zip(ys, (x, y) => (x - meanX) * (y - meanY)).Sum();
        var denominator = xs.Sum(x => (x - meanX) * (x - meanX));
        if (denominator == 0) return null;

        var slope = numerator / denominator;
        var intercept = meanY - slope * meanX;

        var forecast = 0.0;
        for (var i = n; i < n + 7; i++)
            forecast += Math.Max(0, intercept + slope * i);

        return Math.Round((decimal)forecast, 6);
    }

    public async Task<object> GetPerformanceAsync(DateTime from, DateTime to, int? orgId = null)
    {
        var statsQuery = _db.DailyStats.Where(s => s.Date >= from.Date && s.Date <= to.Date);
        if (orgId.HasValue) statsQuery = statsQuery.Where(s => s.OrganizationId == orgId.Value);
        var stats = await statsQuery.ToListAsync();

        if (stats.Any())
        {
            var byModel = stats
                .GroupBy(s => s.ModelName ?? "unknown")
                .Select(g => new
                {
                    model = g.Key,
                    avgLatency = Math.Round(g.Where(s => s.AvgLatencyMs.HasValue).Select(s => (double)s.AvgLatencyMs!.Value).DefaultIfEmpty(0).Average(), 1),
                    p95Latency = g.Max(s => s.P95LatencyMs) ?? 0,
                    avgSpeed = Math.Round(g.Where(s => s.AvgOutputTokensPerSecond.HasValue).Select(s => s.AvgOutputTokensPerSecond!.Value).DefaultIfEmpty(0).Average(), 1),
                    requests = g.Sum(s => s.TotalRequests),
                })
                .OrderByDescending(x => x.requests)
                .ToList();

            var allLatencies = stats.Where(s => s.AvgLatencyMs.HasValue).Select(s => (double)s.AvgLatencyMs!.Value).OrderBy(x => x).ToList();

            return new
            {
                overall = new
                {
                    avgLatency = Math.Round(allLatencies.DefaultIfEmpty(0).Average(), 1),
                    p50Latency = stats.Where(s => s.P50LatencyMs.HasValue).Select(s => (double)s.P50LatencyMs!.Value).OrderBy(x => x).DefaultIfEmpty(0).FirstOrDefault(),
                    p95Latency = stats.Max(s => s.P95LatencyMs) ?? 0,
                    p99Latency = stats.Max(s => s.P99LatencyMs) ?? 0,
                    avgTtft = Math.Round(stats.Where(s => s.AvgTtftMs.HasValue).Select(s => (double)s.AvgTtftMs!.Value).DefaultIfEmpty(0).Average(), 1),
                    p95Ttft = 0.0,
                    avgSpeed = Math.Round(stats.Where(s => s.AvgOutputTokensPerSecond.HasValue).Select(s => s.AvgOutputTokensPerSecond!.Value).DefaultIfEmpty(0).Average(), 1),
                },
                byModel
            };
        }

        var query = _db.RequestLogs.Where(r => r.Timestamp >= from && r.Timestamp <= to && r.Status == Core.Enums.RequestStatus.Success);
        if (orgId.HasValue) query = query.Where(r => r.OrganizationId == orgId.Value);

        var logs = await query.ToListAsync();

        var latencies = logs.Where(r => r.TotalDurationMs.HasValue).Select(r => (double)r.TotalDurationMs!.Value).OrderBy(x => x).ToList();
        var ttfts = logs.Where(r => r.TimeToFirstTokenMs.HasValue).Select(r => (double)r.TimeToFirstTokenMs!.Value).OrderBy(x => x).ToList();
        var speeds = logs.Where(r => r.OutputTokensPerSecond.HasValue).Select(r => r.OutputTokensPerSecond!.Value).OrderBy(x => x).ToList();

        // Thinking (reasoning) content share of output tokens (PRD §3.6.1 输出指标).
        var outputTokensTotal = logs.Sum(r => r.OutputTokens ?? 0);
        var reasoningTokensTotal = logs.Sum(r => r.ReasoningTokens ?? 0);
        var cachedInputTokensTotal = logs.Sum(r => r.CachedInputTokens ?? 0);

        var logByModel = logs
            .GroupBy(r => r.ModelName)
            .Select(g => new
            {
                model = g.Key,
                avgLatency = Math.Round(g.Where(r => r.TotalDurationMs.HasValue).Select(r => (double)r.TotalDurationMs!.Value).DefaultIfEmpty(0).Average(), 1),
                p95Latency = Percentile(g.Where(r => r.TotalDurationMs.HasValue).Select(r => (double)r.TotalDurationMs!.Value).OrderBy(x => x).ToList(), 95),
                avgSpeed = Math.Round(g.Where(r => r.OutputTokensPerSecond.HasValue).Select(r => r.OutputTokensPerSecond!.Value).DefaultIfEmpty(0).Average(), 1),
                requests = g.Count(),
            })
            .OrderByDescending(x => x.requests)
            .ToList();

        return new
        {
            overall = new
            {
                avgLatency = Math.Round(latencies.DefaultIfEmpty(0).Average(), 1),
                p50Latency = Percentile(latencies, 50),
                p95Latency = Percentile(latencies, 95),
                p99Latency = Percentile(latencies, 99),
                avgTtft = Math.Round(ttfts.DefaultIfEmpty(0).Average(), 1),
                p95Ttft = Percentile(ttfts, 95),
                avgSpeed = speeds.Count > 0 ? (double)Math.Round(speeds.Average(), 1) : 0,
                reasoningTokensTotal,
                cachedInputTokensTotal,
                reasoningShare = outputTokensTotal > 0 ? Math.Round((double)reasoningTokensTotal / outputTokensTotal * 100, 1) : 0,
            },
            byModel = logByModel
        };
    }

    public async Task<object> GetCacheStatsAsync(DateTime from, DateTime to, int? orgId = null)
    {
        // Saved tokens/cost always come from request logs (per-hit token data
        // is not retained in daily aggregates).
        var savings = await GetCacheSavingsAsync(from, to, orgId);

        var statsQuery = _db.DailyStats.Where(s => s.Date >= from.Date && s.Date <= to.Date);
        if (orgId.HasValue) statsQuery = statsQuery.Where(s => s.OrganizationId == orgId.Value);
        var stats = await statsQuery.ToListAsync();

        if (stats.Any())
        {
            var total = stats.Sum(s => s.TotalRequests);
            var hits = stats.Sum(s => s.CacheHits);

            var byModel = stats
                .GroupBy(s => s.ModelName ?? "unknown")
                .Select(g => new
                {
                    model = g.Key,
                    total = g.Sum(s => s.TotalRequests),
                    hits = g.Sum(s => s.CacheHits),
                    hitRate = g.Sum(s => s.TotalRequests) > 0 ? Math.Round((double)g.Sum(s => s.CacheHits) / g.Sum(s => s.TotalRequests) * 100, 1) : 0,
                })
                .OrderByDescending(x => x.hits)
                .ToList();

            return new
            {
                totalRequests = total,
                cacheHits = hits,
                hitRate = total > 0 ? Math.Round((double)hits / total * 100, 1) : 0,
                byModel,
                savings.savedTokens,
                savings.savedCost,
                savings.defaultCurrency,
            };
        }

        var query = _db.RequestLogs.Where(r => r.Timestamp >= from && r.Timestamp <= to);
        if (orgId.HasValue) query = query.Where(r => r.OrganizationId == orgId.Value);

        var logs = await query.ToListAsync();
        var logTotal = logs.Count;
        var logHits = logs.Count(r => r.CacheHit);

        var logByModel = logs
            .GroupBy(r => r.ModelName)
            .Select(g => new
            {
                model = g.Key,
                total = g.Count(),
                hits = g.Count(r => r.CacheHit),
                hitRate = g.Count() > 0 ? Math.Round((double)g.Count(r => r.CacheHit) / g.Count() * 100, 1) : 0,
            })
            .OrderByDescending(x => x.hits)
            .ToList();

        return new
        {
            totalRequests = logTotal,
            cacheHits = logHits,
            hitRate = logTotal > 0 ? Math.Round((double)logHits / logTotal * 100, 1) : 0,
            byModel = logByModel,
            savings.savedTokens,
            savings.savedCost,
            savings.defaultCurrency,
        };
    }

    /// <summary>
    /// Tokens and upstream cost saved by cache hits (the tokens/cost those requests
    /// would have consumed without the cache).
    /// </summary>
    private async Task<(long savedTokens, decimal savedCost, string defaultCurrency)> GetCacheSavingsAsync(DateTime from, DateTime to, int? orgId)
    {
        var query = _db.RequestLogs
            .Where(r => r.CacheHit && r.Timestamp >= from && r.Timestamp <= to);
        if (orgId.HasValue) query = query.Where(r => r.OrganizationId == orgId.Value);

        var hitLogs = await query
            .Select(r => new { r.InputTokens, r.OutputTokens, r.InputCost, r.OutputCost, r.Currency })
            .ToListAsync();

        var (defaultCurrency, rates) = await GetCurrencyConfigAsync();

        var savedTokens = hitLogs.Sum(r => (long)(r.InputTokens ?? 0) + (r.OutputTokens ?? 0));
        var savedCost = Math.Round(hitLogs.Sum(r =>
            ConvertCurrency((r.InputCost ?? 0) + (r.OutputCost ?? 0), r.Currency, defaultCurrency, rates)), 6);

        return (savedTokens, savedCost, defaultCurrency);
    }

    /// <summary>
    /// Cross-organization usage comparison (SuperAdmin only).
    /// </summary>
    public async Task<object> GetByOrganizationAsync(DateTime from, DateTime to)
    {
        var logs = await _db.RequestLogs
            .Where(r => r.Timestamp >= from && r.Timestamp <= to && r.OrganizationId.HasValue)
            .Select(r => new
            {
                r.OrganizationId,
                r.InputTokens,
                r.OutputTokens,
                r.InputCost,
                r.OutputCost,
                r.CacheHit,
                r.Currency,
            })
            .ToListAsync();

        var orgNames = await _db.Organizations
            .Select(o => new { o.Id, o.Name })
            .ToDictionaryAsync(o => o.Id, o => o.Name);

        var (defaultCurrency, rates) = await GetCurrencyConfigAsync();

        var items = logs
            .GroupBy(r => r.OrganizationId!.Value)
            .Select(g => new
            {
                organizationId = g.Key,
                organizationName = orgNames.GetValueOrDefault(g.Key, $"#{g.Key}"),
                requests = g.Count(),
                totalTokens = g.Sum(r => (r.InputTokens ?? 0) + (r.OutputTokens ?? 0)),
                totalCost = Math.Round(g.Sum(r => ConvertCurrency((r.InputCost ?? 0) + (r.OutputCost ?? 0), r.Currency, defaultCurrency, rates)), 6),
                cacheHits = g.Count(r => r.CacheHit),
                cacheHitRate = g.Count() > 0 ? Math.Round((double)g.Count(r => r.CacheHit) / g.Count() * 100, 1) : 0,
            })
            .OrderByDescending(x => x.totalTokens)
            .ToList();

        return new { defaultCurrency, items };
    }

    public async Task<object> GetByApiKeyAsync(DateTime from, DateTime to, int? orgId = null)
    {
        var query = _db.RequestLogs.Where(r => r.Timestamp >= from && r.Timestamp <= to && r.ApiKeyId.HasValue);
        if (orgId.HasValue) query = query.Where(r => r.OrganizationId == orgId.Value);

        var logs = await query.Select(r => new
        {
            r.ApiKeyId,
            r.Status,
            r.InputTokens,
            r.OutputTokens,
            r.InputCost,
            r.OutputCost
        }).ToListAsync();

        var groups = logs
            .GroupBy(r => r.ApiKeyId!.Value)
            .Select(g => new
            {
                apiKeyId = g.Key,
                requests = g.Count(),
                success = g.Count(r => r.Status == Core.Enums.RequestStatus.Success),
                inputTokens = g.Sum(r => r.InputTokens ?? 0),
                outputTokens = g.Sum(r => r.OutputTokens ?? 0),
                totalCost = Math.Round(g.Sum(r => (r.InputCost ?? 0) + (r.OutputCost ?? 0)), 6),
            })
            .OrderByDescending(x => x.requests)
            .ToList();

        var keyIds = groups.Select(g => g.apiKeyId).ToList();
        var keyMap = await _db.ApiKeys
            .Include(k => k.User)
            .Include(k => k.Organization)
            .Where(k => keyIds.Contains(k.Id))
            .ToDictionaryAsync(k => k.Id);

        var items = groups.Select(g =>
        {
            keyMap.TryGetValue(g.apiKeyId, out var key);
            return new
            {
                g.apiKeyId,
                name = key?.Name ?? $"key #{g.apiKeyId}", // 密钥已删除时回退
                keyPrefix = key?.KeyPrefix,
                user = key?.User == null ? null : (key.User.DisplayName ?? key.User.Username),
                organization = key?.Organization?.Name,
                g.requests,
                successRate = g.requests > 0 ? Math.Round((double)g.success / g.requests * 100, 1) : 0,
                g.inputTokens,
                g.outputTokens,
                totalTokens = g.inputTokens + g.outputTokens,
                g.totalCost,
            };
        }).ToList();

        return new { items };
    }

    private static double Percentile(List<double> sorted, int percentile)
    {
        if (sorted.Count == 0) return 0;
        var index = (percentile / 100.0) * (sorted.Count - 1);
        var lower = (int)Math.Floor(index);
        var upper = (int)Math.Ceiling(index);
        if (lower == upper) return sorted[lower];
        var weight = index - lower;
        return sorted[lower] * (1 - weight) + sorted[upper] * weight;
    }
}
