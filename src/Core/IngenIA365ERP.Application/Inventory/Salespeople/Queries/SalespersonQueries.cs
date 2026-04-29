using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Salespeople.Queries;

// DTO
public record SalespersonDto
{
    public Guid PublicId { get; init; }
    public string IdNumber { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? LastName { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public string? Mobile { get; init; }
    public int? CityId { get; init; }
    public int? SalespersonType { get; init; }
    public bool AppliesCommission { get; init; }
}

// List Query
public record ListSalespeopleQuery : IRequest<Result<PagedList<SalespersonDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListSalespeopleQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListSalespeopleQuery, Result<PagedList<SalespersonDto>>>
{
    public async Task<Result<PagedList<SalespersonDto>>> Handle(
        ListSalespeopleQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Salespeople
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.Name.ToLower().Contains(term) ||
                                     (e.LastName != null && e.LastName.ToLower().Contains(term)) ||
                                     e.IdNumber.ToLower().Contains(term));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "name" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Name)
                : query.OrderBy(e => e.Name),
            "idnumber" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.IdNumber)
                : query.OrderBy(e => e.IdNumber),
            _ => query.OrderBy(e => e.Name)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new SalespersonDto
            {
                PublicId = e.PublicId,
                IdNumber = e.IdNumber,
                Name = e.Name,
                LastName = e.LastName,
                Address = e.Address,
                Phone = e.Phone,
                Mobile = e.Mobile,
                CityId = e.CityId,
                SalespersonType = e.SalespersonType,
                AppliesCommission = e.AppliesCommission
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<SalespersonDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetSalespersonByIdQuery(Guid PublicId) : IRequest<Result<SalespersonDto>>;

public class GetSalespersonByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetSalespersonByIdQuery, Result<SalespersonDto>>
{
    public async Task<Result<SalespersonDto>> Handle(
        GetSalespersonByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.Salespeople
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new SalespersonDto
            {
                PublicId = e.PublicId,
                IdNumber = e.IdNumber,
                Name = e.Name,
                LastName = e.LastName,
                Address = e.Address,
                Phone = e.Phone,
                Mobile = e.Mobile,
                CityId = e.CityId,
                SalespersonType = e.SalespersonType,
                AppliesCommission = e.AppliesCommission
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<SalespersonDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
