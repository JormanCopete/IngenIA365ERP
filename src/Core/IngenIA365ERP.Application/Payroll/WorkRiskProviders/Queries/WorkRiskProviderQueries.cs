using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.WorkRiskProviders.Queries;

// DTO
public record WorkRiskProviderDto
{
    public Guid PublicId { get; init; }
    public int Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string TaxId { get; init; } = string.Empty;
    public int CheckDigit { get; init; }
    public decimal Factor { get; init; }
}

// List Query
public record ListWorkRiskProvidersQuery : IRequest<Result<PagedList<WorkRiskProviderDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListWorkRiskProvidersQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListWorkRiskProvidersQuery, Result<PagedList<WorkRiskProviderDto>>>
{
    public async Task<Result<PagedList<WorkRiskProviderDto>>> Handle(
        ListWorkRiskProvidersQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.WorkRiskProviders
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.Name.ToLower().Contains(term));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "name" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Name)
                : query.OrderBy(e => e.Name),
            "code" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Code)
                : query.OrderBy(e => e.Code),
            _ => query.OrderBy(e => e.Name)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new WorkRiskProviderDto
            {
                PublicId = e.PublicId,
                Code = e.Code,
                Name = e.Name,
                ShortName = e.ShortName,
                TaxId = e.TaxId,
                CheckDigit = e.CheckDigit,
                Factor = e.Factor
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<WorkRiskProviderDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetWorkRiskProviderByIdQuery(Guid PublicId) : IRequest<Result<WorkRiskProviderDto>>;

public class GetWorkRiskProviderByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetWorkRiskProviderByIdQuery, Result<WorkRiskProviderDto>>
{
    public async Task<Result<WorkRiskProviderDto>> Handle(
        GetWorkRiskProviderByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.WorkRiskProviders
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new WorkRiskProviderDto
            {
                PublicId = e.PublicId,
                Code = e.Code,
                Name = e.Name,
                ShortName = e.ShortName,
                TaxId = e.TaxId,
                CheckDigit = e.CheckDigit,
                Factor = e.Factor
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<WorkRiskProviderDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
