using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.ProductAccounts.Queries;

// DTO
public record ProductAccountDto
{
    public Guid PublicId { get; init; }
    public int ProductGroupId { get; init; }
    public int TransactionTypeId { get; init; }
    public int WarehouseId { get; init; }
    public int LocationId { get; init; }
    public string? VatAccountCode { get; init; }
    public string? DiscountAccountCode { get; init; }
    public string? TaxableSalesAccountCode { get; init; }
    public string? NonTaxableSalesAccountCode { get; init; }
    public string? NetAccountCode { get; init; }
}

// List Query
public record ListProductAccountsQuery : IRequest<Result<PagedList<ProductAccountDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListProductAccountsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListProductAccountsQuery, Result<PagedList<ProductAccountDto>>>
{
    public async Task<Result<PagedList<ProductAccountDto>>> Handle(
        ListProductAccountsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.ProductAccounts
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e =>
                (e.VatAccountCode != null && e.VatAccountCode.ToLower().Contains(term)) ||
                (e.NetAccountCode != null && e.NetAccountCode.ToLower().Contains(term)));
        }

        query = query.OrderBy(e => e.ProductGroupId).ThenBy(e => e.TransactionTypeId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new ProductAccountDto
            {
                PublicId = e.PublicId,
                ProductGroupId = e.ProductGroupId,
                TransactionTypeId = e.TransactionTypeId,
                WarehouseId = e.WarehouseId,
                LocationId = e.LocationId,
                VatAccountCode = e.VatAccountCode,
                DiscountAccountCode = e.DiscountAccountCode,
                TaxableSalesAccountCode = e.TaxableSalesAccountCode,
                NonTaxableSalesAccountCode = e.NonTaxableSalesAccountCode,
                NetAccountCode = e.NetAccountCode
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<ProductAccountDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetProductAccountByIdQuery(Guid PublicId) : IRequest<Result<ProductAccountDto>>;

public class GetProductAccountByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetProductAccountByIdQuery, Result<ProductAccountDto>>
{
    public async Task<Result<ProductAccountDto>> Handle(
        GetProductAccountByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.ProductAccounts
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new ProductAccountDto
            {
                PublicId = e.PublicId,
                ProductGroupId = e.ProductGroupId,
                TransactionTypeId = e.TransactionTypeId,
                WarehouseId = e.WarehouseId,
                LocationId = e.LocationId,
                VatAccountCode = e.VatAccountCode,
                DiscountAccountCode = e.DiscountAccountCode,
                TaxableSalesAccountCode = e.TaxableSalesAccountCode,
                NonTaxableSalesAccountCode = e.NonTaxableSalesAccountCode,
                NetAccountCode = e.NetAccountCode
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<ProductAccountDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
