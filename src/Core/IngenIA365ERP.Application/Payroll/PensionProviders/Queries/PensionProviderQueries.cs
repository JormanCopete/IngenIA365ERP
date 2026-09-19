using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.PensionProviders.Queries;

// DTO
public record PensionProviderDto
{
    public Guid PublicId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string TaxId { get; init; } = string.Empty;
    public int CheckDigit { get; init; }
    public Guid? PersonPublicId { get; init; }
    public string? PersonName { get; init; }
}

// List Query
public record ListPensionProvidersQuery : IRequest<Result<PagedList<PensionProviderDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListPensionProvidersQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListPensionProvidersQuery, Result<PagedList<PensionProviderDto>>>
{
    public async Task<Result<PagedList<PensionProviderDto>>> Handle(
        ListPensionProvidersQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.PensionProviders
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
            .Select(e => new PensionProviderDto
            {
                PublicId = e.PublicId,
                Code = e.Code,
                Name = e.Name,
                ShortName = e.ShortName,
                TaxId = e.TaxId,
                CheckDigit = e.CheckDigit, PersonPublicId = e.Person != null ? e.Person.PublicId : (Guid?)null, PersonName = e.Person == null ? null : (e.Person.BusinessName ?? (e.Person.FirstName + " " + e.Person.LastName))
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<PensionProviderDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetPensionProviderByIdQuery(Guid PublicId) : IRequest<Result<PensionProviderDto>>;

public class GetPensionProviderByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPensionProviderByIdQuery, Result<PensionProviderDto>>
{
    public async Task<Result<PensionProviderDto>> Handle(
        GetPensionProviderByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.PensionProviders
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new PensionProviderDto
            {
                PublicId = e.PublicId,
                Code = e.Code,
                Name = e.Name,
                ShortName = e.ShortName,
                TaxId = e.TaxId,
                CheckDigit = e.CheckDigit, PersonPublicId = e.Person != null ? e.Person.PublicId : (Guid?)null, PersonName = e.Person == null ? null : (e.Person.BusinessName ?? (e.Person.FirstName + " " + e.Person.LastName))
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<PensionProviderDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
