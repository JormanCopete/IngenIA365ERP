using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.SeveranceProviders.Queries;

// DTO
public record SeveranceProviderDto
{
    public Guid PublicId { get; init; }
    public int Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string TaxId { get; init; } = string.Empty;
    public int CheckDigit { get; init; }
}

// List Query
public record ListSeveranceProvidersQuery : IRequest<Result<PagedList<SeveranceProviderDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListSeveranceProvidersQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListSeveranceProvidersQuery, Result<PagedList<SeveranceProviderDto>>>
{
    public async Task<Result<PagedList<SeveranceProviderDto>>> Handle(
        ListSeveranceProvidersQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.SeveranceProviders
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
            .Select(e => new SeveranceProviderDto
            {
                PublicId = e.PublicId,
                Code = e.Code,
                Name = e.Name,
                ShortName = e.ShortName,
                TaxId = e.TaxId,
                CheckDigit = e.CheckDigit
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<SeveranceProviderDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetSeveranceProviderByIdQuery(Guid PublicId) : IRequest<Result<SeveranceProviderDto>>;

public class GetSeveranceProviderByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetSeveranceProviderByIdQuery, Result<SeveranceProviderDto>>
{
    public async Task<Result<SeveranceProviderDto>> Handle(
        GetSeveranceProviderByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.SeveranceProviders
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new SeveranceProviderDto
            {
                PublicId = e.PublicId,
                Code = e.Code,
                Name = e.Name,
                ShortName = e.ShortName,
                TaxId = e.TaxId,
                CheckDigit = e.CheckDigit
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<SeveranceProviderDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
