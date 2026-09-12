using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Companies.Queries;

/// <summary>
/// DTO simplificado de Empresa (Company / sys_compania).
/// Expone solo los campos basicos relevantes para la pagina de Maestros.
/// Los ~140 campos de configuracion legacy se administran en otras pantallas.
/// </summary>
public record CompanyDto
{
    public Guid PublicId { get; init; }
    public string? Code { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string TaxId { get; init; } = string.Empty;
    public string? TaxIdCheckDigit { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public string? City { get; init; }
    public string? Department { get; init; }
}

public record ListCompaniesQuery : IRequest<Result<PagedList<CompanyDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListCompaniesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListCompaniesQuery, Result<PagedList<CompanyDto>>>
{
    public async Task<Result<PagedList<CompanyDto>>> Handle(
        ListCompaniesQuery request, CancellationToken ct)
    {
        var query = context.Companies
            .AsNoTracking()
            .Where(c => !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(c =>
                c.Name.Contains(term) ||
                c.TaxId.Contains(term) ||
                (c.ShortName != null && c.ShortName.Contains(term)));
        }

        query = query.OrderBy(c => c.Name);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(c => new CompanyDto
            {
                PublicId = c.PublicId,
                Code = c.LegacyCode,
                Name = c.Name,
                ShortName = c.ShortName,
                TaxId = c.TaxId,
                TaxIdCheckDigit = c.TaxIdCheckDigit,
                Address = c.Address,
                Phone = c.Phone,
                City = c.City,
                Department = c.Department
            })
            .ToListAsync(ct);

        return Result.Success(new PagedList<CompanyDto>(
            items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize));
    }
}

public record GetCompanyByIdQuery(Guid PublicId) : IRequest<Result<CompanyDto>>;

public class GetCompanyByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCompanyByIdQuery, Result<CompanyDto>>
{
    public async Task<Result<CompanyDto>> Handle(GetCompanyByIdQuery request, CancellationToken ct)
    {
        var dto = await context.Companies
            .AsNoTracking()
            .Where(c => c.PublicId == request.PublicId && !c.IsDeleted)
            .Select(c => new CompanyDto
            {
                PublicId = c.PublicId,
                Code = c.LegacyCode,
                Name = c.Name,
                ShortName = c.ShortName,
                TaxId = c.TaxId,
                TaxIdCheckDigit = c.TaxIdCheckDigit,
                Address = c.Address,
                Phone = c.Phone,
                City = c.City,
                Department = c.Department
            })
            .FirstOrDefaultAsync(ct);

        return dto is null
            ? Result.Failure<CompanyDto>(new Error("Company.NotFound", "Empresa no encontrada."))
            : Result.Success(dto);
    }
}
