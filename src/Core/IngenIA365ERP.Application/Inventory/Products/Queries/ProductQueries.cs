using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Products.Queries;

// DTO
public record ProductDto
{
    public Guid PublicId { get; init; }
    public int ProductCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public int? GroupId { get; init; }
    public int? DiscountTypeId { get; init; }
    public string? UnitOfMeasure { get; init; }
    public decimal CostPrice { get; init; }
    public decimal SalePrice { get; init; }
    public decimal VatRate { get; init; }
    public int MinStock { get; init; }
    public int MaxStock { get; init; }
    public int CurrentStock { get; init; }
    public bool IsActive { get; init; }
    public string? Barcode { get; init; }
    public decimal OtherTax { get; init; }
    public bool ControlsStock { get; init; }
    public bool RestrictsLimit { get; init; }
    public int MaxSalesQuantity { get; init; }
}

// List Query
public record ListProductsQuery : IRequest<Result<PagedList<ProductDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListProductsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListProductsQuery, Result<PagedList<ProductDto>>>
{
    public async Task<Result<PagedList<ProductDto>>> Handle(
        ListProductsQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.Products
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
            "productcode" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.ProductCode)
                : query.OrderBy(e => e.ProductCode),
            _ => query.OrderBy(e => e.Name)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new ProductDto
            {
                PublicId = e.PublicId,
                ProductCode = e.ProductCode,
                Name = e.Name,
                ShortName = e.ShortName,
                GroupId = e.GroupId,
                DiscountTypeId = e.DiscountTypeId,
                UnitOfMeasure = e.UnitOfMeasure,
                CostPrice = e.CostPrice,
                SalePrice = e.SalePrice,
                VatRate = e.VatRate,
                MinStock = e.MinStock,
                MaxStock = e.MaxStock,
                CurrentStock = e.CurrentStock,
                IsActive = e.IsActive,
                Barcode = e.Barcode,
                OtherTax = e.OtherTax,
                ControlsStock = e.ControlsStock,
                RestrictsLimit = e.RestrictsLimit,
                MaxSalesQuantity = e.MaxSalesQuantity
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<ProductDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetProductByIdQuery(Guid PublicId) : IRequest<Result<ProductDto>>;

public class GetProductByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetProductByIdQuery, Result<ProductDto>>
{
    public async Task<Result<ProductDto>> Handle(
        GetProductByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.Products
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new ProductDto
            {
                PublicId = e.PublicId,
                ProductCode = e.ProductCode,
                Name = e.Name,
                ShortName = e.ShortName,
                GroupId = e.GroupId,
                DiscountTypeId = e.DiscountTypeId,
                UnitOfMeasure = e.UnitOfMeasure,
                CostPrice = e.CostPrice,
                SalePrice = e.SalePrice,
                VatRate = e.VatRate,
                MinStock = e.MinStock,
                MaxStock = e.MaxStock,
                CurrentStock = e.CurrentStock,
                IsActive = e.IsActive,
                Barcode = e.Barcode,
                OtherTax = e.OtherTax,
                ControlsStock = e.ControlsStock,
                RestrictsLimit = e.RestrictsLimit,
                MaxSalesQuantity = e.MaxSalesQuantity
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<ProductDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
