using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.InventoryTransactionTypes.Queries;

// DTO
public record InventoryTransactionTypeDto
{
    public Guid PublicId { get; init; }
    public int TypeCode { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? ShortDescription { get; init; }
    public string? TransactionVoucherCode { get; init; }
    public string? CostVoucherCode { get; init; }
    public decimal SequenceNumber { get; init; }
    public int ControlsStock { get; init; }
    public string? DocumentClass { get; init; }
    public int UpdatesAccounting { get; init; }
    public string? PortfolioVoucherCode { get; init; }
    public int? CreditLineId { get; init; }
    public string? DeductionType { get; init; }
    public string? InvoiceControl { get; init; }
    public bool TotalInPurchase { get; init; }
    public bool CostsProducts { get; init; }
    public bool IsReturn { get; init; }
    public bool TransfersAccounting { get; init; }
    public bool AllowsBonus { get; init; }
    public bool ValidatesCreditLimit { get; init; }
}

// List Query
public record ListInventoryTransactionTypesQuery : IRequest<Result<PagedList<InventoryTransactionTypeDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
}

public class ListInventoryTransactionTypesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListInventoryTransactionTypesQuery, Result<PagedList<InventoryTransactionTypeDto>>>
{
    public async Task<Result<PagedList<InventoryTransactionTypeDto>>> Handle(
        ListInventoryTransactionTypesQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.InventoryTransactionTypes
            .AsNoTracking()
            .Where(e => !e.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(e => e.Description.ToLower().Contains(term));
        }

        query = request.Pagination.SortBy?.ToLower() switch
        {
            "description" => request.Pagination.IsDescending
                ? query.OrderByDescending(e => e.Description)
                : query.OrderBy(e => e.Description),
            _ => query.OrderBy(e => e.Description)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(e => new InventoryTransactionTypeDto
            {
                PublicId = e.PublicId,
                TypeCode = e.TypeCode,
                Description = e.Description,
                ShortDescription = e.ShortDescription,
                TransactionVoucherCode = e.TransactionVoucherCode,
                CostVoucherCode = e.CostVoucherCode,
                SequenceNumber = e.SequenceNumber,
                ControlsStock = e.ControlsStock,
                DocumentClass = e.DocumentClass,
                UpdatesAccounting = e.UpdatesAccounting,
                PortfolioVoucherCode = e.PortfolioVoucherCode,
                CreditLineId = e.CreditLineId,
                DeductionType = e.DeductionType,
                InvoiceControl = e.InvoiceControl,
                TotalInPurchase = e.TotalInPurchase,
                CostsProducts = e.CostsProducts,
                IsReturn = e.IsReturn,
                TransfersAccounting = e.TransfersAccounting,
                AllowsBonus = e.AllowsBonus,
                ValidatesCreditLimit = e.ValidatesCreditLimit
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<InventoryTransactionTypeDto>(items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize);
        return Result.Success(pagedList);
    }
}

// GetById Query
public record GetInventoryTransactionTypeByIdQuery(Guid PublicId) : IRequest<Result<InventoryTransactionTypeDto>>;

public class GetInventoryTransactionTypeByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetInventoryTransactionTypeByIdQuery, Result<InventoryTransactionTypeDto>>
{
    public async Task<Result<InventoryTransactionTypeDto>> Handle(
        GetInventoryTransactionTypeByIdQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await context.InventoryTransactionTypes
            .AsNoTracking()
            .Where(e => e.PublicId == request.PublicId && !e.IsDeleted)
            .Select(e => new InventoryTransactionTypeDto
            {
                PublicId = e.PublicId,
                TypeCode = e.TypeCode,
                Description = e.Description,
                ShortDescription = e.ShortDescription,
                TransactionVoucherCode = e.TransactionVoucherCode,
                CostVoucherCode = e.CostVoucherCode,
                SequenceNumber = e.SequenceNumber,
                ControlsStock = e.ControlsStock,
                DocumentClass = e.DocumentClass,
                UpdatesAccounting = e.UpdatesAccounting,
                PortfolioVoucherCode = e.PortfolioVoucherCode,
                CreditLineId = e.CreditLineId,
                DeductionType = e.DeductionType,
                InvoiceControl = e.InvoiceControl,
                TotalInPurchase = e.TotalInPurchase,
                CostsProducts = e.CostsProducts,
                IsReturn = e.IsReturn,
                TransfersAccounting = e.TransfersAccounting,
                AllowsBonus = e.AllowsBonus,
                ValidatesCreditLimit = e.ValidatesCreditLimit
            })
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<InventoryTransactionTypeDto>(Error.NotFound)
            : Result.Success(dto);
    }
}
