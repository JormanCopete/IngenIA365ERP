using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.CDT.CdtRatesByTerm.Queries;

// DTO
public record CdtRateByTermDto
{
    public Guid PublicId { get; init; }
    public int CreditLineId { get; init; }
    public decimal AmountRangeStart { get; init; }
    public decimal AmountRangeEnd { get; init; }
    public int TermStart { get; init; }
    public int TermEnd { get; init; }
    public decimal? InterestRate { get; init; }
    public DateTime? LastUpdated { get; init; }
}

// List Query
public record ListCdtRatesByTermQuery : IRequest<Result<PagedList<CdtRateByTermDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListCdtRatesByTermQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListCdtRatesByTermQuery, Result<PagedList<CdtRateByTermDto>>>
{
    public async Task<Result<PagedList<CdtRateByTermDto>>> Handle(
        ListCdtRatesByTermQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.CdtRatesByTerm
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        query = query.OrderBy(e => e.CreditLineId).ThenBy(e => e.TermStart);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new CdtRateByTermDto
            {
                PublicId = e.PublicId,
                CreditLineId = e.CreditLineId,
                AmountRangeStart = e.AmountRangeStart,
                AmountRangeEnd = e.AmountRangeEnd,
                TermStart = e.TermStart,
                TermEnd = e.TermEnd,
                InterestRate = e.InterestRate,
                LastUpdated = e.LastUpdated
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<CdtRateByTermDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetCdtRateByTermByIdQuery(Guid PublicId) : IRequest<Result<CdtRateByTermDto>>;

public class GetCdtRateByTermByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCdtRateByTermByIdQuery, Result<CdtRateByTermDto>>
{
    public async Task<Result<CdtRateByTermDto>> Handle(
        GetCdtRateByTermByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.CdtRatesByTerm
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new CdtRateByTermDto
            {
                PublicId = e.PublicId,
                CreditLineId = e.CreditLineId,
                AmountRangeStart = e.AmountRangeStart,
                AmountRangeEnd = e.AmountRangeEnd,
                TermStart = e.TermStart,
                TermEnd = e.TermEnd,
                InterestRate = e.InterestRate,
                LastUpdated = e.LastUpdated
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<CdtRateByTermDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
