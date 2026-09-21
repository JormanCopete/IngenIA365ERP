using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.HealthInsuranceProviders.Queries;

// DTO
public record HealthInsuranceProviderDto
{
    public Guid PublicId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string TaxId { get; init; } = string.Empty;
    public int CheckDigit { get; init; }
    public string? PilaCode { get; init; }
    public Guid? PersonPublicId { get; init; }
    public string? PersonName { get; init; }
}

// List Query
public record ListHealthInsuranceProvidersQuery : IRequest<Result<PagedList<HealthInsuranceProviderDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListHealthInsuranceProvidersQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListHealthInsuranceProvidersQuery, Result<PagedList<HealthInsuranceProviderDto>>>
{
    public async Task<Result<PagedList<HealthInsuranceProviderDto>>> Handle(
        ListHealthInsuranceProvidersQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.HealthInsuranceProviders
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
            .Select(e => new HealthInsuranceProviderDto
            {
                PublicId = e.PublicId,
                Code = e.Code,
                Name = e.Name,
                ShortName = e.ShortName,
                TaxId = e.TaxId,
                PilaCode = e.PilaCode, CheckDigit = e.CheckDigit, PersonPublicId = e.Person != null ? e.Person.PublicId : (Guid?)null, PersonName = e.Person == null ? null : (e.Person.BusinessName ?? (e.Person.FirstName + " " + e.Person.LastName))
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<HealthInsuranceProviderDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetHealthInsuranceProviderByIdQuery(Guid PublicId) : IRequest<Result<HealthInsuranceProviderDto>>;

public class GetHealthInsuranceProviderByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetHealthInsuranceProviderByIdQuery, Result<HealthInsuranceProviderDto>>
{
    public async Task<Result<HealthInsuranceProviderDto>> Handle(
        GetHealthInsuranceProviderByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.HealthInsuranceProviders
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new HealthInsuranceProviderDto
            {
                PublicId = e.PublicId,
                Code = e.Code,
                Name = e.Name,
                ShortName = e.ShortName,
                TaxId = e.TaxId,
                PilaCode = e.PilaCode, CheckDigit = e.CheckDigit, PersonPublicId = e.Person != null ? e.Person.PublicId : (Guid?)null, PersonName = e.Person == null ? null : (e.Person.BusinessName ?? (e.Person.FirstName + " " + e.Person.LastName))
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<HealthInsuranceProviderDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
