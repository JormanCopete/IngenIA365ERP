using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.PayrollConcepts.Queries;

// DTO
public record PayrollConceptDto
{
    public Guid PublicId { get; init; }
    public int ConceptCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public int ConceptClass { get; init; }
    public int Nature { get; init; }
    public decimal Value { get; init; }
    public decimal Factor { get; init; }
    public int Base { get; init; }
    public int AffectsSalary { get; init; }
    public int LiquidationBase { get; init; }
    public decimal TopSalary { get; init; }
    public int AffectsBenefits { get; init; }
    public int AffectsWithholding { get; init; }
    public int IsBenefit { get; init; }
    public decimal ProvisionRate { get; init; }
    public int ProvisionBase { get; init; }
    public int AffectsSeverance { get; init; }
    public int AffectsBonus { get; init; }
    public int AffectsVacation { get; init; }
    public int AffectsIndemnity { get; init; }
    public int ConceptSubClass { get; init; }
}

// List Query
public record ListPayrollConceptsQuery : IRequest<Result<PagedList<PayrollConceptDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListPayrollConceptsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListPayrollConceptsQuery, Result<PagedList<PayrollConceptDto>>>
{
    public async Task<Result<PagedList<PayrollConceptDto>>> Handle(
        ListPayrollConceptsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.PayrollConcepts
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
            "conceptcode" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.ConceptCode)
                : query.OrderBy(e => e.ConceptCode),
            _ => query.OrderBy(e => e.Name)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new PayrollConceptDto
            {
                PublicId = e.PublicId,
                ConceptCode = e.ConceptCode,
                Name = e.Name,
                ShortName = e.ShortName,
                ConceptClass = e.ConceptClass,
                Nature = e.Nature,
                Value = e.Value,
                Factor = e.Factor,
                Base = e.Base,
                AffectsSalary = e.AffectsSalary,
                LiquidationBase = e.LiquidationBase,
                TopSalary = e.TopSalary,
                AffectsBenefits = e.AffectsBenefits,
                AffectsWithholding = e.AffectsWithholding,
                IsBenefit = e.IsBenefit,
                ProvisionRate = e.ProvisionRate,
                ProvisionBase = e.ProvisionBase,
                AffectsSeverance = e.AffectsSeverance,
                AffectsBonus = e.AffectsBonus,
                AffectsVacation = e.AffectsVacation,
                AffectsIndemnity = e.AffectsIndemnity,
                ConceptSubClass = e.ConceptSubClass
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<PayrollConceptDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetPayrollConceptByIdQuery(Guid PublicId) : IRequest<Result<PayrollConceptDto>>;

public class GetPayrollConceptByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPayrollConceptByIdQuery, Result<PayrollConceptDto>>
{
    public async Task<Result<PayrollConceptDto>> Handle(
        GetPayrollConceptByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.PayrollConcepts
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new PayrollConceptDto
            {
                PublicId = e.PublicId,
                ConceptCode = e.ConceptCode,
                Name = e.Name,
                ShortName = e.ShortName,
                ConceptClass = e.ConceptClass,
                Nature = e.Nature,
                Value = e.Value,
                Factor = e.Factor,
                Base = e.Base,
                AffectsSalary = e.AffectsSalary,
                LiquidationBase = e.LiquidationBase,
                TopSalary = e.TopSalary,
                AffectsBenefits = e.AffectsBenefits,
                AffectsWithholding = e.AffectsWithholding,
                IsBenefit = e.IsBenefit,
                ProvisionRate = e.ProvisionRate,
                ProvisionBase = e.ProvisionBase,
                AffectsSeverance = e.AffectsSeverance,
                AffectsBonus = e.AffectsBonus,
                AffectsVacation = e.AffectsVacation,
                AffectsIndemnity = e.AffectsIndemnity,
                ConceptSubClass = e.ConceptSubClass
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<PayrollConceptDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
