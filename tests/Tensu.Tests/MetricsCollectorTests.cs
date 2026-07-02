using Tensu.Api.Infrastructure;
using Xunit;

namespace Tensu.Tests;

public class MetricsCollectorTests
{
    [Fact]
    public void Export_ContainsExpectedMetrics()
    {
        var collector = new MetricsCollector();
        collector.RecordRequest("gpt-4o", "OpenAI", success: true, cacheHit: false, latencyMs: 150, tokensIn: 100, tokensOut: 50, rateLimited: false);
        collector.RecordRequest("gpt-4o", "OpenAI", success: true, cacheHit: true, latencyMs: 50, tokensIn: 80, tokensOut: 30, rateLimited: false);

        var output = collector.Export();

        Assert.Contains("tensu_requests_total 2", output);
        Assert.Contains("tensu_requests_success_total 2", output);
        Assert.Contains("tensu_cache_hits_total 1", output);
        Assert.Contains("tensu_cache_misses_total 1", output);
        Assert.Contains("tensu_tokens_input_total 180", output);
        Assert.Contains("tensu_tokens_output_total 80", output);
        Assert.Contains("tensu_requests_by_model{model=\"gpt-4o\"} 2", output);
    }

    [Fact]
    public void Export_ContainsPrometheusHeaders()
    {
        var collector = new MetricsCollector();
        var output = collector.Export();

        Assert.Contains("# HELP", output);
        Assert.Contains("# TYPE", output);
    }

    [Fact]
    public void Export_FailedRequests_TrackedCorrectly()
    {
        var collector = new MetricsCollector();
        collector.RecordRequest("model1", "provider1", success: false, cacheHit: false, latencyMs: 0, tokensIn: 0, tokensOut: 0, rateLimited: false);

        var output = collector.Export();
        Assert.Contains("tensu_requests_failed_total 1", output);
        Assert.Contains("tensu_errors_by_provider{provider=\"provider1\"} 1", output);
    }

    [Fact]
    public void Export_RateLimited_TrackedCorrectly()
    {
        var collector = new MetricsCollector();
        collector.RecordRequest("model1", "provider1", success: false, cacheHit: false, latencyMs: 0, tokensIn: 0, tokensOut: 0, rateLimited: true);

        var output = collector.Export();
        Assert.Contains("tensu_rate_limited_total 1", output);
    }
}
