using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.TermRates.Queries;

// DTO
public record TermRateDto
{
    public Guid PublicId { get; init; }
    public int CreditLineId { get; init; }
    public decimal AmountStart { get; init; }
    public decimal AmountEnd { get; init; }
    public int TermStart { get; init; }
    public int TermEnd { get; init; }
    public int SeniorityStart { get; init; }
    public int SeniorityEnd { get; init; }
    public decimal Rate { get; init; }
    public DateTime UpdateDate { get; init; }
    public int MaxTerm { get; init; }
    public decimal MaxAmount { get; init; }
    public string GuaranteeType { get; init; } = string.Empty;
}

// List Query
public record ListTermRatesQuery : IRequest<Result<PagedList<TermRateDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListTermRatesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListTermRatesQuery, Result<PagedList<TermRateDto>>>
{
    public async Task<Result<PagedList<TermRateDto>>> Handle(
        ListTermRatesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.TermRates
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            if (int.TryParse(term, out var creditLineId))
            {
                query = query.Where(e => e.CreditLineId == creditLineId);
            }
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "creditlineid" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.CreditLineId)
                : query.OrderBy(e => e.CreditLineId),
            "rate" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Rate)
                : query.OrderBy(e => e.Rate),
            _ => query.OrderBy(e => e.CreditLineId)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new TermRateDto
            {
                PublicId = e.PublicId,
                CreditLineId = e.CreditLineId,
                AmountStart = e.AmountStart,
                AmountEnd = e.AmountEnd,
                TermStart = e.TermStart,
                TermEnd = e.TermEnd,
                SeniorityStart = e.SeniorityStart,
                SeniorityEnd = e.SeniorityEnd,
                Rate = e.Rate,
                UpdateDate = e.UpdateDate,
                MaxTerm = e.MaxTerm,
                MaxAmount = e.MaxAmount,
                GuaranteeType = e.GuaranteeType
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<TermRateDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetTermRateByIdQuery(Guid PublicId) : IRequest<Result<TermRateDto>>;

public class GetTermRateByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetTermRateByIdQuery, Result<TermRateDto>>
{
    public async Task<Result<TermRateDto>> Handle(
        GetTermRateByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.TermRates
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new TermRateDto
            {
                PublicId = e.PublicId,
                CreditLineId = e.CreditLineId,
                AmountStart = e.AmountStart,
                AmountEnd = e.AmountEnd,
                TermStart = e.TermStart,
                TermEnd = e.TermEnd,
                SeniorityStart = e.SeniorityStart,
                SeniorityEnd = e.SeniorityEnd,
                Rate = e.Rate,
                UpdateDate = e.UpdateDate,
                MaxTerm = e.MaxTerm,
                MaxAmount = e.MaxAmount,
                GuaranteeType = e.GuaranteeType
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<TermRateDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
