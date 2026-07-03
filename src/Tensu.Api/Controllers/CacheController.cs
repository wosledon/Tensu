using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Tensu.Api.Data;
using Tensu.Api.Infrastructure;
using Tensu.Core.Common;
using Tensu.Core.Entities;
using Tensu.Core.Enums;

namespace Tensu.Api.Controllers;

[ApiController]
[Route("api/admin/cache")]
[Authorize]
public class CacheController : AdminBaseController
{
    private readonly TensuDbContext _db;
    private readonly CacheService _cache;
    private readonly AuditChannel _auditChannel;
    private readonly ILogger<CacheController> _logger;

    public CacheController(TensuDbContext db, CacheService cache, AuditChannel auditChannel, ILogger<CacheController> logger)
    {
        _db = db;
        _cache = cache;
        _auditChannel = auditChannel;
        _logger = logger;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        var exactStats = _cache.GetStats();
        var semanticCount = await _cache.GetSemanticEntryCountAsync();

        var from = DateTime.UtcNow.AddDays(-7);
        var allHits = await _db.RequestLogs
            .AsNoTracking()
            .Where(r => r.CacheHit && r.Timestamp >= from)
            .CountAsync();

        var semanticHits = await _db.RequestLogs
            .AsNoTracking()
            .Where(r => r.SemanticCacheHit && r.Timestamp >= from)
            .CountAsync();

        return Ok(ApiResponse<object>.Success(new
        {
            exact = new { entries = exactStats.entries, hits = allHits - semanticHits },
            semantic = new { entries = semanticCount, hits = semanticHits }
        }));
    }

    [HttpDelete("semantic")]
    [Authorize(Policy = "SuperAdmin")]
    public async Task<IActionResult> ClearSemantic()
    {
        await _cache.ClearSemanticAsync();
        await EnqueueSystemAuditAsync("clear:semantic");
        return Ok(ApiResponse.Success());
    }

    [HttpDelete("exact")]
    [Authorize(Policy = "SuperAdmin")]
    public async Task<IActionResult> ClearExact()
    {
        _cache.ClearExact();
        await EnqueueSystemAuditAsync("clear:exact");
        return Ok(ApiResponse.Success());
    }

    private async Task EnqueueSystemAuditAsync(string detail)
    {
        try
        {
            await _auditChannel.EnqueueAsync(new RequestLog
            {
                RequestId = $"system:cache:{Guid.NewGuid():N}",
                Timestamp = DateTime.UtcNow,
                ApiKeyId = null,
                OrganizationId = CurrentOrgId,
                UserId = CurrentUserId,
                ModelName = "system:cache",
                ProviderName = null,
                InputTokens = 0,
                OutputTokens = 0,
                Status = RequestStatus.Success,
                IsStream = false,
                CacheHit = false,
                CompressionApplied = false,
                CompressionStrategy = "none",
                RequestContent = detail
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue cache system audit log");
        }
    }
}
