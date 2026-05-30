using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Departments.Queries;

// DTO
public record DepartmentDto
{
    public Guid PublicId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public Guid CountryPublicId { get; init; }
    public string CountryName { get; init; } = string.Empty;
}

// List Query
public record ListDepartmentsQuery : IRequest<Result<PagedList<DepartmentDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
    public Guid? CountryPublicId { get; init; }
}

public class ListDepartmentsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListDepartmentsQuery, Result<PagedList<DepartmentDto>>>
{
    public async Task<Result<PagedList<DepartmentDto>>> Handle(
        ListDepartmentsQuery request, CancellationToken ct)
    {
        var query = context.Departments
            .AsNoTracking()
            .Include(d => d.Country)
            .Where(e => !e.IsDeleted);

        if (request.CountryPublicId.HasValue)
            query = query.Where(d => d.Country.PublicId == request.CountryPublicId.Value);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(d =>
                d.Name.Contains(term) ||
                d.Code.Contains(term));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "code" => request.Pagination.IsDescending
                ? query.OrderByDescending(d => d.Code)
                : query.OrderBy(d => d.Code),
            "name" => request.Pagination.IsDescending
                ? query.OrderByDescending(d => d.Name)
                : query.OrderBy(d => d.Name),
            _ => query.OrderBy(d => d.Country.Name).ThenBy(d => d.Name)
        };

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(d => new DepartmentDto
            {
                PublicId = d.PublicId,
                Code = d.Code,
                Name = d.Name,
                CountryPublicId = d.Country.PublicId,
                CountryName = d.Country.Name
            })
            .ToListAsync(ct);

        return Result.Success(new PagedList<DepartmentDto>(
            items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize));
    }
}

// GetById
public record GetDepartmentByIdQuery(Guid PublicId) : IRequest<Result<DepartmentDto>>;

public class GetDepartmentByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetDepartmentByIdQuery, Result<DepartmentDto>>
{
    public async Task<Result<DepartmentDto>> Handle(
        GetDepartmentByIdQuery request, CancellationToken ct)
    {
        var dto = await context.Departments
            .AsNoTracking()
            .Include(d => d.Country)
            .Where(d => d.PublicId == request.PublicId && !d.IsDeleted)
            .Select(d => new DepartmentDto
            {
                PublicId = d.PublicId,
                Code = d.Code,
                Name = d.Name,
                CountryPublicId = d.Country.PublicId,
                CountryName = d.Country.Name
            })
            .FirstOrDefaultAsync(ct);

        return dto is null
            ? Result.Failure<DepartmentDto>(new Error("Department.NotFound", "Departamento no encontrado."))
            : Result.Success(dto);
    }
}
