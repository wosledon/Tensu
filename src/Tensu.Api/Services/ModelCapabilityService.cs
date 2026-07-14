using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Core.Common;
using Tensu.Core.Entities;
using Tensu.Core.Enums;

namespace Tensu.Api.Services;

public class ModelCapabilityService : BaseService
{
    private readonly TensuDbContext _db;

    public ModelCapabilityService(TensuDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<ModelCapability>> GetListAsync(PagedRequest request, int? modelId = null, string? dimension = null)
    {
        var query = _db.ModelCapabilities
            .Include(c => c.Model)
            .ThenInclude(m => m.Provider)
            .AsQueryable();

        if (modelId.HasValue)
            query = query.Where(c => c.ModelId == modelId.Value);

        if (!string.IsNullOrEmpty(dimension))
            query = query.Where(c => c.Dimension == dimension);

        if (!string.IsNullOrEmpty(request.Keyword))
            query = query.Where(c => c.Dimension.Contains(request.Keyword) || (c.Evidence != null && c.Evidence.Contains(request.Keyword)));

        var (pagedQuery, total) = await ApplyPagingAsync(query, request);
        var items = await pagedQuery.ToListAsync();
        return ToPagedResult(items, total, request);
    }

    public async Task<ModelCapability?> GetByIdAsync(long id)
    {
        return await _db.ModelCapabilities
            .Include(c => c.Model)
            .ThenInclude(m => m.Provider)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<ModelCapability?> GetByModelAndDimensionAsync(int modelId, string dimension)
    {
        return await _db.ModelCapabilities
            .FirstOrDefaultAsync(c => c.ModelId == modelId && c.Dimension == dimension);
    }

    public async Task<ModelCapability> CreateAsync(ModelCapability capability)
    {
        capability.CreatedAt = DateTime.UtcNow;
        capability.UpdatedAt = DateTime.UtcNow;
        _db.ModelCapabilities.Add(capability);
        await _db.SaveChangesAsync();
        return capability;
    }

    public async Task<ModelCapability?> UpdateAsync(long id, ModelCapability updated)
    {
        var capability = await _db.ModelCapabilities.FindAsync(id);
        if (capability == null) return null;

        capability.ModelId = updated.ModelId;
        capability.Dimension = updated.Dimension;
        capability.Score = updated.Score;
        capability.Source = updated.Source;
        capability.Evidence = updated.Evidence;
        capability.EvaluatedAt = updated.EvaluatedAt;
        capability.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return capability;
    }

    public async Task<bool> DeleteAsync(long id)
    {
        var capability = await _db.ModelCapabilities.FindAsync(id);
        if (capability == null) return false;
        _db.ModelCapabilities.Remove(capability);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<ModelCapability>> ImportAsync(List<ModelCapability> capabilities)
    {
        var results = new List<ModelCapability>();
        foreach (var capability in capabilities)
        {
            var existing = await _db.ModelCapabilities
                .FirstOrDefaultAsync(c => c.ModelId == capability.ModelId && c.Dimension == capability.Dimension);

            if (existing != null)
            {
                existing.Score = capability.Score;
                existing.Source = capability.Source;
                existing.Evidence = capability.Evidence;
                existing.EvaluatedAt = capability.EvaluatedAt;
                existing.UpdatedAt = DateTime.UtcNow;
                results.Add(existing);
            }
            else
            {
                capability.CreatedAt = DateTime.UtcNow;
                capability.UpdatedAt = DateTime.UtcNow;
                _db.ModelCapabilities.Add(capability);
                results.Add(capability);
            }
        }

        await _db.SaveChangesAsync();
        return results;
    }

    public async Task<ModelCapabilityMatrix> GetMatrixAsync(int? providerId = null)
    {
        var query = _db.Models
            .Include(m => m.Provider)
            .Include(m => m.Capabilities)
            .AsQueryable();

        if (providerId.HasValue)
            query = query.Where(m => m.ProviderId == providerId.Value);

        var models = await query.ToListAsync();

        var allDimensions = models
            .SelectMany(m => m.Capabilities)
            .Select(c => c.Dimension)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        var dimensionSet = new HashSet<string>(allDimensions);

        var matrix = new ModelCapabilityMatrix
        {
            Dimensions = allDimensions,
            Models = models.Select(m =>
            {
                var scores = m.Capabilities
                    .Where(c => dimensionSet.Contains(c.Dimension))
                    .GroupBy(c => c.Dimension)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Average(c => c.Score));

                var overallScore = scores.Any() ? scores.Values.Average() : 0m;

                return new ModelCapabilityMatrixItem
                {
                    ModelId = m.Id,
                    ModelName = m.Name,
                    ProviderName = m.Provider?.Name ?? string.Empty,
                    Scores = scores,
                    OverallScore = overallScore
                };
            }).ToList()
        };

        return matrix;
    }

    public async Task<SyntheticEvaluationResult> RunSyntheticEvaluationAsync(int modelId, string[] dimensions)
    {
        var model = await _db.Models
            .Include(m => m.Provider)
            .Include(m => m.Pricings)
            .FirstOrDefaultAsync(m => m.Id == modelId);

        if (model == null)
            return new SyntheticEvaluationResult { ModelId = modelId, Message = "Model not found", Results = [] };

        var supportedSyntheticDimensions = new HashSet<string> { "Latency", "Throughput", "CostEfficiency" };
        var results = new List<DimensionEvaluationResult>();

        var logs = await _db.RequestLogs
            .Where(r => r.ModelName == model.Name || r.ResolvedModelName == model.Name)
            .Where(r => r.Status == RequestStatus.Success && r.TotalDurationMs.HasValue)
            .ToListAsync();

        foreach (var dimension in dimensions)
        {
            if (!supportedSyntheticDimensions.Contains(dimension))
            {
                results.Add(new DimensionEvaluationResult
                {
                    Dimension = dimension,
                    Score = null,
                    Message = "Synthetic evaluation not supported for this dimension; manual/benchmark score required."
                });
                continue;
            }

            decimal? score = dimension switch
            {
                "Latency" => CalculateLatencyScore(logs),
                "Throughput" => CalculateThroughputScore(logs),
                "CostEfficiency" => CalculateCostEfficiencyScore(logs, model),
                _ => null
            };

            if (score.HasValue)
            {
                var capability = await _db.ModelCapabilities
                    .FirstOrDefaultAsync(c => c.ModelId == modelId && c.Dimension == dimension);

                if (capability != null)
                {
                    capability.Score = score.Value;
                    capability.Source = "synthetic";
                    capability.Evidence = $"Auto-evaluated from {logs.Count} request logs";
                    capability.EvaluatedAt = DateTime.UtcNow;
                    capability.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    capability = new ModelCapability
                    {
                        ModelId = modelId,
                        Dimension = dimension,
                        Score = score.Value,
                        Source = "synthetic",
                        Evidence = $"Auto-evaluated from {logs.Count} request logs",
                        EvaluatedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _db.ModelCapabilities.Add(capability);
                }

                await _db.SaveChangesAsync();
            }

            results.Add(new DimensionEvaluationResult
            {
                Dimension = dimension,
                Score = score,
                Message = score.HasValue ? "Evaluated successfully" : "Insufficient data for synthetic evaluation"
            });
        }

        return new SyntheticEvaluationResult
        {
            ModelId = modelId,
            ModelName = model.Name,
            Results = results
        };
    }

    private static decimal? CalculateLatencyScore(List<RequestLog> logs)
    {
        if (!logs.Any()) return null;

        var avgLatency = logs.Average(r => r.TotalDurationMs!.Value);
        // Normalize: assume 0ms = 100, 5000ms = 0
        var score = 100m - ((decimal)avgLatency / 5000m * 100m);
        return Math.Clamp(score, 0m, 100m);
    }

    private static decimal? CalculateThroughputScore(List<RequestLog> logs)
    {
        var validLogs = logs.Where(r => r.OutputTokensPerSecond.HasValue && r.OutputTokens > 0).ToList();
        if (!validLogs.Any()) return null;

        var avgThroughput = validLogs.Average(r => r.OutputTokensPerSecond!.Value);
        // Normalize: assume 0 tokens/s = 0, 100 tokens/s = 100
        var score = (decimal)avgThroughput / 100m * 100m;
        return Math.Clamp(score, 0m, 100m);
    }

    private static decimal? CalculateCostEfficiencyScore(List<RequestLog> logs, Model model)
    {
        var pricing = model.Pricings
            .Where(p => p.EffectiveTo == null || p.EffectiveTo > DateTime.UtcNow)
            .OrderByDescending(p => p.EffectiveFrom)
            .FirstOrDefault();

        if (pricing == null) return null;

        var validLogs = logs.Where(r => (r.InputTokens.HasValue || r.OutputTokens.HasValue) &&
                                        (r.InputCost.HasValue || r.OutputCost.HasValue)).ToList();
        if (!validLogs.Any()) return null;

        // Cost per 1M tokens
        var totalTokens = validLogs.Sum(r => (r.InputTokens ?? 0) + (r.OutputTokens ?? 0));
        var totalCost = validLogs.Sum(r => (r.InputCost ?? 0m) + (r.OutputCost ?? 0m));

        if (totalTokens <= 0 || totalCost <= 0) return null;

        var costPerMillion = totalCost / (totalTokens / 1_000_000m);
        // Normalize: assume $0.1/M = 100, $10/M = 0
        var score = 100m - ((costPerMillion - 0.1m) / (10m - 0.1m) * 100m);
        return Math.Clamp(score, 0m, 100m);
    }
}

public class ModelCapabilityMatrix
{
    public List<string> Dimensions { get; set; } = [];
    public List<ModelCapabilityMatrixItem> Models { get; set; } = [];
}

public class ModelCapabilityMatrixItem
{
    public int ModelId { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public Dictionary<string, decimal> Scores { get; set; } = [];
    public decimal OverallScore { get; set; }
}

public class SyntheticEvaluationResult
{
    public int ModelId { get; set; }
    public string ModelName { get; set; } = string.Empty;
    public List<DimensionEvaluationResult> Results { get; set; } = [];
    public string Message { get; set; } = string.Empty;
}

public class DimensionEvaluationResult
{
    public string Dimension { get; set; } = string.Empty;
    public decimal? Score { get; set; }
    public string Message { get; set; } = string.Empty;
}
