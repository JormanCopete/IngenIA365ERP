using System.Reflection;
using Microsoft.AspNetCore.Http;

namespace IngenIA365ERP.Application.Common.Models;

public record PaginationParams
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public bool IsDescending { get; init; }

    // Allows RequestDelegateFactory to bind this type from query string when it
    // appears as a property of an [AsParameters] target. Without this, RDF infers
    // it as Body — invalid on GET.
    public static ValueTask<PaginationParams?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        var q = context.Request.Query;
        var result = new PaginationParams
        {
            PageNumber = int.TryParse(q["PageNumber"], out var pn) && pn > 0 ? pn : 1,
            PageSize = int.TryParse(q["PageSize"], out var ps) && ps > 0 ? ps : 20,
            SortBy = string.IsNullOrWhiteSpace(q["SortBy"]) ? null : q["SortBy"].ToString(),
            IsDescending = bool.TryParse(q["IsDescending"], out var d) && d,
        };
        return ValueTask.FromResult<PaginationParams?>(result);
    }
}
