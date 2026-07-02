using System.Text.Json.Serialization;

namespace Tensu.Core.Common;

public class PagedRequest
{
    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; } = 20;

    [JsonPropertyName("sortBy")]
    public string? SortBy { get; set; }

    [JsonPropertyName("sortOrder")]
    public string? SortOrder { get; set; } // "asc" or "desc"

    [JsonPropertyName("keyword")]
    public string? Keyword { get; set; }
}
