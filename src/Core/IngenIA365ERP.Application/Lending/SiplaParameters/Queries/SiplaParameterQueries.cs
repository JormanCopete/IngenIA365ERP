using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.SiplaParameters.Queries;

// DTO
public record SiplaParameterDto
{
    public Guid PublicId { get; init; }
    public string ConceptCode { get; init; } = string.Empty;
    public string CompanyCode { get; init; } = string.Empty;
    public decimal MonthlyCreditMoves { get; init; }
    public decimal MaxBalance { get; init; }
    public int AccountCount { get; init; }
    public int AnnualTransactions { get; init; }
}

// List Query
public record ListSiplaParametersQuery : IRequest<Result<PagedList<SiplaParameterDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListSiplaParametersQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListSiplaParametersQuery, Result<PagedList<SiplaParameterDto>>>
{
    public async Task<Result<PagedList<SiplaParameterDto>>> Handle(
        ListSiplaParametersQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.SiplaParameters
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.ConceptCode.ToLower().Contains(term)
                || e.CompanyCode.ToLower().Contains(term));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "conceptcode" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.ConceptCode)
                : query.OrderBy(e => e.ConceptCode),
            "companycode" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.CompanyCode)
                : query.OrderBy(e => e.CompanyCode),
            _ => query.OrderBy(e => e.ConceptCode)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new SiplaParameterDto
            {
                PublicId = e.PublicId,
                ConceptCode = e.ConceptCode,
                CompanyCode = e.CompanyCode,
                MonthlyCreditMoves = e.MonthlyCreditMoves,
                MaxBalance = e.MaxBalance,
                AccountCount = e.AccountCount,
                AnnualTransactions = e.AnnualTransactions
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<SiplaParameterDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetSiplaParameterByIdQuery(Guid PublicId) : IRequest<Result<SiplaParameterDto>>;

public class GetSiplaParameterByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetSiplaParameterByIdQuery, Result<SiplaParameterDto>>
{
    public async Task<Result<SiplaParameterDto>> Handle(
        GetSiplaParameterByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.SiplaParameters
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new SiplaParameterDto
            {
                PublicId = e.PublicId,
                ConceptCode = e.ConceptCode,
                CompanyCode = e.CompanyCode,
                MonthlyCreditMoves = e.MonthlyCreditMoves,
                MaxBalance = e.MaxBalance,
                AccountCount = e.AccountCount,
                AnnualTransactions = e.AnnualTransactions
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<SiplaParameterDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
