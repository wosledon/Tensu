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
[Route("api/admin/compression")]
[Authorize(Policy = "SuperAdmin")]
public class CompressionController : AdminBaseController
{
    private readonly TensuDbContext _db;
    private readonly AuditChannel _auditChannel;
    private readonly ILogger<CompressionController> _logger;

    public CompressionController(TensuDbContext db, AuditChannel auditChannel, ILogger<CompressionController> logger)
    {
        _db = db;
        _auditChannel = auditChannel;
        _logger = logger;
    }

    [HttpGet("mappings")]
    public async Task<IActionResult> GetMappings(
        [FromQuery] string? requestId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _db.CompressionMappings.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(requestId))
        {
            // requestId is stored in the audit log's CompressionMappingKey; here we
            // allow filtering by the decompression key substring if a request mapping key was passed.
            query = query.Where(m => m.DecompressionKey.Contains(requestId));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new
            {
                m.Id,
                m.DecompressionKey,
                m.Strategy,
                m.CreatedAt,
                HasOriginalBody = m.OriginalBody != null,
                OriginalBodyLength = m.OriginalBody != null ? m.OriginalBody.Length : (int?)null,
                CompressedBodyLength = m.CompressedBody.Length
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Success(new { items, total, page, pageSize }));
    }

    [HttpGet("mappings/{decompressionKey}")]
    public async Task<IActionResult> GetMapping(string decompressionKey)
    {
        var mapping = await _db.CompressionMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.DecompressionKey == decompressionKey);

        if (mapping == null)
            return NotFound(ApiResponse.Error(404, "Compression mapping not found"));

        return Ok(ApiResponse<object>.Success(new
        {
            mapping.Id,
            mapping.DecompressionKey,
            mapping.Strategy,
            mapping.CreatedAt,
            HasOriginalBody = mapping.OriginalBody != null,
            OriginalBody = mapping.OriginalBody,
            CompressedBody = mapping.CompressedBody
        }));
    }

    [HttpPost("restore")]
    public async Task<IActionResult> Restore([FromBody] RestoreRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DecompressionKey))
            return BadRequest(ApiResponse.Error(400, "decompressionKey is required"));

        var mapping = await _db.CompressionMappings
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.DecompressionKey == request.DecompressionKey);

        if (mapping == null)
            return NotFound(ApiResponse.Error(404, "Compression mapping not found"));

        if (string.IsNullOrEmpty(mapping.OriginalBody))
            return BadRequest(ApiResponse.Error(400, "Original body was not stored for this mapping"));

        return Ok(ApiResponse<object>.Success(new { originalBody = mapping.OriginalBody }));
    }

    public record RestoreRequest(string DecompressionKey);

    [HttpDelete("mappings/{decompressionKey}")]
    public async Task<IActionResult> DeleteMapping(string decompressionKey)
    {
        var mapping = await _db.CompressionMappings
            .FirstOrDefaultAsync(m => m.DecompressionKey == decompressionKey);

        if (mapping == null)
            return NotFound(ApiResponse.Error(404, "Compression mapping not found"));

        _db.CompressionMappings.Remove(mapping);
        await _db.SaveChangesAsync();
        await EnqueueSystemAuditAsync($"delete:{decompressionKey}");
        return Ok(ApiResponse.Success());
    }

    private async Task EnqueueSystemAuditAsync(string detail)
    {
        try
        {
            await _auditChannel.EnqueueAsync(new RequestLog
            {
                RequestId = $"system:compression:{Guid.NewGuid():N}",
                Timestamp = DateTime.UtcNow,
                ApiKeyId = null,
                OrganizationId = CurrentOrgId,
                UserId = CurrentUserId,
                ModelName = "system:compression",
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
            _logger.LogError(ex, "Failed to enqueue compression system audit log");
        }
    }
}
