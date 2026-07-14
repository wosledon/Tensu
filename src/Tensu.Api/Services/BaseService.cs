using System.Linq.Dynamic.Core;
using Microsoft.EntityFrameworkCore;
using Tensu.Core.Common;

namespace Tensu.Api.Services;

public abstract class BaseService
{
    protected async Task<(IQueryable<T> Query, int Total)> ApplyPagingAsync<T>(IQueryable<T> query, PagedRequest request)
    {
        var total = await query.CountAsync();

        if (!string.IsNullOrEmpty(request.SortBy))
        {
            var sortOrder = request.SortOrder?.ToLower() == "desc" ? "descending" : "ascending";
            query = query.OrderBy($"{request.SortBy} {sortOrder}");
        }

        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        query = query.Skip((page - 1) * pageSize).Take(pageSize);

        return (query, total);
    }

    protected Core.Common.PagedResult<T> ToPagedResult<T>(List<T> items, int total, PagedRequest request)
    {
        return new Core.Common.PagedResult<T>
        {
            Items = items,
            Total = total,
            Page = Math.Max(1, request.Page),
            PageSize = Math.Clamp(request.PageSize, 1, 100)
        };
    }
}
