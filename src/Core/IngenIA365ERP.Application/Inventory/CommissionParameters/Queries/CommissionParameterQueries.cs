using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.CommissionParameters.Queries;

// DTO
public record CommissionParameterDto
{
    public Guid PublicId { get; init; }
    public int InvoiceTypeId { get; init; }
    public int CommissionGroupId { get; init; }
    public int GroupId { get; init; }
    public decimal SalesRangeStart { get; init; }
    public decimal SalesRangeEnd { get; init; }
    public decimal CommissionRate { get; init; }
}

// List Query
public record ListCommissionParametersQuery : IRequest<Result<PagedList<CommissionParameterDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListCommissionParametersQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListCommissionParametersQuery, Result<PagedList<CommissionParameterDto>>>
{
    public async Task<Result<PagedList<CommissionParameterDto>>> Handle(
        ListCommissionParametersQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.CommissionParameters
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        query = query.OrderBy(e => e.InvoiceTypeId).ThenBy(e => e.CommissionGroupId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new CommissionParameterDto
            {
                PublicId = e.PublicId,
                InvoiceTypeId = e.InvoiceTypeId,
                CommissionGroupId = e.CommissionGroupId,
                GroupId = e.GroupId,
                SalesRangeStart = e.SalesRangeStart,
                SalesRangeEnd = e.SalesRangeEnd,
                CommissionRate = e.CommissionRate
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<CommissionParameterDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetCommissionParameterByIdQuery(Guid PublicId) : IRequest<Result<CommissionParameterDto>>;

public class GetCommissionParameterByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetCommissionParameterByIdQuery, Result<CommissionParameterDto>>
{
    public async Task<Result<CommissionParameterDto>> Handle(
        GetCommissionParameterByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.CommissionParameters
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new CommissionParameterDto
            {
                PublicId = e.PublicId,
                InvoiceTypeId = e.InvoiceTypeId,
                CommissionGroupId = e.CommissionGroupId,
                GroupId = e.GroupId,
                SalesRangeStart = e.SalesRangeStart,
                SalesRangeEnd = e.SalesRangeEnd,
                CommissionRate = e.CommissionRate
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<CommissionParameterDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
