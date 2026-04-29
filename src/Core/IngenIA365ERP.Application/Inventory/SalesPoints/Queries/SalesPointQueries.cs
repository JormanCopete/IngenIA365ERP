using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.SalesPoints.Queries;

// DTO
public record SalesPointDto
{
    public Guid PublicId { get; init; }
    public int PointCode { get; init; }
    public string? UserId { get; init; }
    public int? TransactionTypeId { get; init; }
    public string? PrinterName { get; init; }
    public int Status { get; init; }
    public DateTime? DateId { get; init; }
    public int? ShiftId { get; init; }
    public decimal BaseAmount { get; init; }
    public int? WarehouseId { get; init; }
    public int? LocationId { get; init; }
}

// List Query
public record ListSalesPointsQuery : IRequest<Result<PagedList<SalesPointDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListSalesPointsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListSalesPointsQuery, Result<PagedList<SalesPointDto>>>
{
    public async Task<Result<PagedList<SalesPointDto>>> Handle(
        ListSalesPointsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.SalesPoints
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => (e.UserId != null && e.UserId.ToLower().Contains(term)) ||
                                     (e.PrinterName != null && e.PrinterName.ToLower().Contains(term)));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "pointcode" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.PointCode)
                : query.OrderBy(e => e.PointCode),
            _ => query.OrderBy(e => e.PointCode)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new SalesPointDto
            {
                PublicId = e.PublicId,
                PointCode = e.PointCode,
                UserId = e.UserId,
                TransactionTypeId = e.TransactionTypeId,
                PrinterName = e.PrinterName,
                Status = e.Status,
                DateId = e.DateId,
                ShiftId = e.ShiftId,
                BaseAmount = e.BaseAmount,
                WarehouseId = e.WarehouseId,
                LocationId = e.LocationId
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<SalesPointDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetSalesPointByIdQuery(Guid PublicId) : IRequest<Result<SalesPointDto>>;

public class GetSalesPointByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetSalesPointByIdQuery, Result<SalesPointDto>>
{
    public async Task<Result<SalesPointDto>> Handle(
        GetSalesPointByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.SalesPoints
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new SalesPointDto
            {
                PublicId = e.PublicId,
                PointCode = e.PointCode,
                UserId = e.UserId,
                TransactionTypeId = e.TransactionTypeId,
                PrinterName = e.PrinterName,
                Status = e.Status,
                DateId = e.DateId,
                ShiftId = e.ShiftId,
                BaseAmount = e.BaseAmount,
                WarehouseId = e.WarehouseId,
                LocationId = e.LocationId
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<SalesPointDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
