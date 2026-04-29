using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.ConceptAccounts.Queries;

// DTO
public record ConceptAccountDto
{
    public Guid PublicId { get; init; }
    public int ConceptId { get; init; }
    public string CostCenterId { get; init; } = string.Empty;
    public string ExpenseAccountCode { get; init; } = string.Empty;
    public string CounterAccountCode { get; init; } = string.Empty;
    public string ProvisionAccountCode { get; init; } = string.Empty;
}

// List Query
public record ListConceptAccountsQuery : IRequest<Result<PagedList<ConceptAccountDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListConceptAccountsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListConceptAccountsQuery, Result<PagedList<ConceptAccountDto>>>
{
    public async Task<Result<PagedList<ConceptAccountDto>>> Handle(
        ListConceptAccountsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.ConceptAccounts
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.ExpenseAccountCode.ToLower().Contains(term)
                || e.CostCenterId.ToLower().Contains(term));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "conceptid" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.ConceptId)
                : query.OrderBy(e => e.ConceptId),
            "costcenterid" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.CostCenterId)
                : query.OrderBy(e => e.CostCenterId),
            _ => query.OrderBy(e => e.ConceptId)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new ConceptAccountDto
            {
                PublicId = e.PublicId,
                ConceptId = e.ConceptId,
                CostCenterId = e.CostCenterId,
                ExpenseAccountCode = e.ExpenseAccountCode,
                CounterAccountCode = e.CounterAccountCode,
                ProvisionAccountCode = e.ProvisionAccountCode
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<ConceptAccountDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetConceptAccountByIdQuery(Guid PublicId) : IRequest<Result<ConceptAccountDto>>;

public class GetConceptAccountByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetConceptAccountByIdQuery, Result<ConceptAccountDto>>
{
    public async Task<Result<ConceptAccountDto>> Handle(
        GetConceptAccountByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.ConceptAccounts
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new ConceptAccountDto
            {
                PublicId = e.PublicId,
                ConceptId = e.ConceptId,
                CostCenterId = e.CostCenterId,
                ExpenseAccountCode = e.ExpenseAccountCode,
                CounterAccountCode = e.CounterAccountCode,
                ProvisionAccountCode = e.ProvisionAccountCode
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<ConceptAccountDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
