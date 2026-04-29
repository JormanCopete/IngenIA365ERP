using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.DianReportFormats.Queries;

// DTO
public record DianReportFormatDto
{
    public Guid PublicId { get; init; }
    public int FormatId { get; init; }
    public int ConceptId { get; init; }
    public string? FormatCode { get; init; }
    public string? Description { get; init; }
    public decimal Threshold { get; init; }
    public decimal BalanceThreshold { get; init; }
}

// List Query
public record ListDianReportFormatsQuery : IRequest<Result<PagedList<DianReportFormatDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListDianReportFormatsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListDianReportFormatsQuery, Result<PagedList<DianReportFormatDto>>>
{
    public async Task<Result<PagedList<DianReportFormatDto>>> Handle(
        ListDianReportFormatsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.DianReportFormats
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => (e.Description != null && e.Description.ToLower().Contains(term))
                || (e.FormatCode != null && e.FormatCode.ToLower().Contains(term)));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "formatcode" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.FormatCode)
                : query.OrderBy(e => e.FormatCode),
            _ => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Description)
                : query.OrderBy(e => e.Description)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new DianReportFormatDto
            {
                PublicId = e.PublicId,
                FormatId = e.FormatId,
                ConceptId = e.ConceptId,
                FormatCode = e.FormatCode,
                Description = e.Description,
                Threshold = e.Threshold,
                BalanceThreshold = e.BalanceThreshold
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<DianReportFormatDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetDianReportFormatByIdQuery(Guid PublicId) : IRequest<Result<DianReportFormatDto>>;

public class GetDianReportFormatByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetDianReportFormatByIdQuery, Result<DianReportFormatDto>>
{
    public async Task<Result<DianReportFormatDto>> Handle(
        GetDianReportFormatByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.DianReportFormats
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new DianReportFormatDto
            {
                PublicId = e.PublicId,
                FormatId = e.FormatId,
                ConceptId = e.ConceptId,
                FormatCode = e.FormatCode,
                Description = e.Description,
                Threshold = e.Threshold,
                BalanceThreshold = e.BalanceThreshold
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<DianReportFormatDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
