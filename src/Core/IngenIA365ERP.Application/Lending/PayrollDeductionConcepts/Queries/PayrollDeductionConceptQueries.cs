using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.PayrollDeductionConcepts.Queries;

// DTO
public record PayrollDeductionConceptDto
{
    public Guid PublicId { get; init; }
    public string CompanyCode { get; init; } = string.Empty;
    public string BranchId { get; init; } = string.Empty;
    public string CostCenterId { get; init; } = string.Empty;
    public int CreditLineId { get; init; }
    public string PayrollConceptCode { get; init; } = string.Empty;
    public string InterestConceptCode { get; init; } = string.Empty;
    public string ExtraConceptCode { get; init; } = string.Empty;
}

// List Query
public record ListPayrollDeductionConceptsQuery : IRequest<Result<PagedList<PayrollDeductionConceptDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListPayrollDeductionConceptsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListPayrollDeductionConceptsQuery, Result<PagedList<PayrollDeductionConceptDto>>>
{
    public async Task<Result<PagedList<PayrollDeductionConceptDto>>> Handle(
        ListPayrollDeductionConceptsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.PayrollDeductionConcepts
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.CompanyCode.ToLower().Contains(term));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "companycode" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.CompanyCode)
                : query.OrderBy(e => e.CompanyCode),
            "creditlineid" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.CreditLineId)
                : query.OrderBy(e => e.CreditLineId),
            _ => query.OrderBy(e => e.CompanyCode)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new PayrollDeductionConceptDto
            {
                PublicId = e.PublicId,
                CompanyCode = e.CompanyCode,
                BranchId = e.BranchId,
                CostCenterId = e.CostCenterId,
                CreditLineId = e.CreditLineId,
                PayrollConceptCode = e.PayrollConceptCode,
                InterestConceptCode = e.InterestConceptCode,
                ExtraConceptCode = e.ExtraConceptCode
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<PayrollDeductionConceptDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetPayrollDeductionConceptByIdQuery(Guid PublicId) : IRequest<Result<PayrollDeductionConceptDto>>;

public class GetPayrollDeductionConceptByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPayrollDeductionConceptByIdQuery, Result<PayrollDeductionConceptDto>>
{
    public async Task<Result<PayrollDeductionConceptDto>> Handle(
        GetPayrollDeductionConceptByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.PayrollDeductionConcepts
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new PayrollDeductionConceptDto
            {
                PublicId = e.PublicId,
                CompanyCode = e.CompanyCode,
                BranchId = e.BranchId,
                CostCenterId = e.CostCenterId,
                CreditLineId = e.CreditLineId,
                PayrollConceptCode = e.PayrollConceptCode,
                InterestConceptCode = e.InterestConceptCode,
                ExtraConceptCode = e.ExtraConceptCode
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<PayrollDeductionConceptDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
