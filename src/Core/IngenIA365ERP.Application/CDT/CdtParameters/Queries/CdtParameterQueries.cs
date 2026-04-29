using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.CDT.CdtParameters.Queries;

// DTO
public record CdtParameterDto
{
    public Guid PublicId { get; init; }
    public int CreditLineId { get; init; }
    public string? Description { get; init; }
    public decimal? MinimumRate { get; init; }
    public decimal? AnnualRate { get; init; }
    public decimal? WithholdingRate { get; init; }
    public decimal? MinWithholdingAmount { get; init; }
    public int? InterestConceptId { get; init; }
    public int? WithholdingConceptId { get; init; }
    public decimal? MonthlyIncrement { get; init; }
    public decimal? InterestRate { get; init; }
    public int? Term { get; init; }
    public decimal? MinAmount { get; init; }
    public decimal? MaxAmount { get; init; }
    public int InterestPaymentType { get; init; }
    public string InterestType { get; init; } = "S";
    public int FormatId { get; init; }
    public int ConceptId { get; init; }
    public int SourceId { get; init; }
    public string? TreasuryAccount { get; init; }
}

// List Query
public record ListCdtParametersQuery : IRequest<Result<PagedList<CdtParameterDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListCdtParametersQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListCdtParametersQuery, Result<PagedList<CdtParameterDto>>>
{
    public async Task<Result<PagedList<CdtParameterDto>>> Handle(
        ListCdtParametersQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.CdtParameters
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.Description != null && e.Description.ToLower().Contains(term));
        }

        query = query.OrderBy(e => e.CreditLineId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new CdtParameterDto
            {
                PublicId = e.PublicId,
                CreditLineId = e.CreditLineId,
                Description = e.Description,
                MinimumRate = e.MinimumRate,
                AnnualRate = e.AnnualRate,
                WithholdingRate = e.WithholdingRate,
                MinWithholdingAmount = e.MinWithholdingAmount,
                InterestConceptId = e.InterestConceptId,
                WithholdingConceptId = e.WithholdingConceptId,
                MonthlyIncrement = e.MonthlyIncrement,
                InterestRate = e.InterestRate,
                Term = e.Term,
                MinAmount = e.MinAmount,
                MaxAmount = e.MaxAmount,
                InterestPaymentType = e.InterestPaymentType,
                InterestType = e.InterestType,
                FormatId = e.FormatId,
                ConceptId = e.ConceptId,
                SourceId = e.SourceId,
                TreasuryAccount = e.TreasuryAccount
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<CdtParameterDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetCdtParameterByIdQuery(Guid PublicId) : IRequest<Result<CdtParameterDto>>;

public class GetCdtParameterByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCdtParameterByIdQuery, Result<CdtParameterDto>>
{
    public async Task<Result<CdtParameterDto>> Handle(
        GetCdtParameterByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.CdtParameters
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new CdtParameterDto
            {
                PublicId = e.PublicId,
                CreditLineId = e.CreditLineId,
                Description = e.Description,
                MinimumRate = e.MinimumRate,
                AnnualRate = e.AnnualRate,
                WithholdingRate = e.WithholdingRate,
                MinWithholdingAmount = e.MinWithholdingAmount,
                InterestConceptId = e.InterestConceptId,
                WithholdingConceptId = e.WithholdingConceptId,
                MonthlyIncrement = e.MonthlyIncrement,
                InterestRate = e.InterestRate,
                Term = e.Term,
                MinAmount = e.MinAmount,
                MaxAmount = e.MaxAmount,
                InterestPaymentType = e.InterestPaymentType,
                InterestType = e.InterestType,
                FormatId = e.FormatId,
                ConceptId = e.ConceptId,
                SourceId = e.SourceId,
                TreasuryAccount = e.TreasuryAccount
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<CdtParameterDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
