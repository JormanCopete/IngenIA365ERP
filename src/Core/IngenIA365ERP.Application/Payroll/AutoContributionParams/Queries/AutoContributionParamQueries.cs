using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.AutoContributionParams.Queries;

// DTO
public record AutoContributionParamDto
{
    public Guid PublicId { get; init; }
    public int Code { get; init; }
    public int IdType { get; init; }
    public int IdNumber { get; init; }
    public int CheckDigit { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public int CityId { get; init; }
    public string? CityName { get; init; }
    public int DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public decimal HealthRate { get; init; }
    public decimal PensionRate { get; init; }
    public decimal WorkRiskRate { get; init; }
    public decimal CcfRate { get; init; }
    public decimal SenaRate { get; init; }
    public decimal IcbfRate { get; init; }
    public decimal SolidarityFundRate { get; init; }
    public decimal MinimumWage { get; init; }
    public string? Email { get; init; }
    public int PresentationMethod { get; init; }
}

// List Query
public record ListAutoContributionParamsQuery : IRequest<Result<PagedList<AutoContributionParamDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListAutoContributionParamsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListAutoContributionParamsQuery, Result<PagedList<AutoContributionParamDto>>>
{
    public async Task<Result<PagedList<AutoContributionParamDto>>> Handle(
        ListAutoContributionParamsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.AutoContributionParams
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
            .Select(e => new AutoContributionParamDto
            {
                PublicId = e.PublicId,
                Code = e.Code,
                IdType = e.IdType,
                IdNumber = e.IdNumber,
                CheckDigit = e.CheckDigit,
                Name = e.Name,
                Address = e.Address,
                Phone = e.Phone,
                CityId = e.CityId,
                CityName = e.CityName,
                DepartmentId = e.DepartmentId,
                DepartmentName = e.DepartmentName,
                HealthRate = e.HealthRate,
                PensionRate = e.PensionRate,
                WorkRiskRate = e.WorkRiskRate,
                CcfRate = e.CcfRate,
                SenaRate = e.SenaRate,
                IcbfRate = e.IcbfRate,
                SolidarityFundRate = e.SolidarityFundRate,
                MinimumWage = e.MinimumWage,
                Email = e.Email,
                PresentationMethod = e.PresentationMethod
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<AutoContributionParamDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetAutoContributionParamByIdQuery(Guid PublicId) : IRequest<Result<AutoContributionParamDto>>;

public class GetAutoContributionParamByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetAutoContributionParamByIdQuery, Result<AutoContributionParamDto>>
{
    public async Task<Result<AutoContributionParamDto>> Handle(
        GetAutoContributionParamByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.AutoContributionParams
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new AutoContributionParamDto
            {
                PublicId = e.PublicId,
                Code = e.Code,
                IdType = e.IdType,
                IdNumber = e.IdNumber,
                CheckDigit = e.CheckDigit,
                Name = e.Name,
                Address = e.Address,
                Phone = e.Phone,
                CityId = e.CityId,
                CityName = e.CityName,
                DepartmentId = e.DepartmentId,
                DepartmentName = e.DepartmentName,
                HealthRate = e.HealthRate,
                PensionRate = e.PensionRate,
                WorkRiskRate = e.WorkRiskRate,
                CcfRate = e.CcfRate,
                SenaRate = e.SenaRate,
                IcbfRate = e.IcbfRate,
                SolidarityFundRate = e.SolidarityFundRate,
                MinimumWage = e.MinimumWage,
                Email = e.Email,
                PresentationMethod = e.PresentationMethod
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<AutoContributionParamDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
