using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents.Queries;

// --- DTOs ---

public record InventoryDocumentDto(
    Guid PublicId,
    string DocumentType,
    decimal DocumentNumber,
    DateOnly Date,
    string WarehouseName,
    string TransactionTypeName,
    decimal TotalAmount,
    bool IsVoided);

public record InventoryTransactionLineDto(
    Guid PublicId,
    string ProductName,
    int ProductCode,
    int Quantity,
    decimal UnitPrice,
    decimal SubTotal,
    decimal VatAmount,
    decimal DiscountAmount,
    string? WarehouseName);

public record InventoryDocumentDetailDto(
    Guid PublicId,
    string DocumentType,
    decimal DocumentNumber,
    DateOnly Date,
    string WarehouseName,
    string TransactionTypeName,
    decimal TotalAmount,
    decimal DiscountAmount,
    decimal VatAmount,
    string? Detail,
    bool IsVoided,
    List<InventoryTransactionLineDto> Lines);

public record ProductStockDto(
    Guid ProductPublicId,
    string ProductName,
    int ProductCode,
    int TotalStock,
    List<WarehouseStockDto> ByWarehouse);

public record WarehouseStockDto(
    Guid WarehousePublicId,
    string WarehouseName,
    int Stock);

public record StockMovementDto(
    Guid PublicId,
    DateOnly Date,
    decimal DocumentNumber,
    string TransactionTypeName,
    int Quantity,
    decimal UnitPrice,
    decimal SubTotal,
    string? WarehouseName);

// --- List Inventory Documents ---

public record ListInventoryDocumentsQuery : IRequest<Result<PagedList<InventoryDocumentDto>>>
{
    public int PageNumber { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? DocumentType { get; init; }
    public Guid? WarehousePublicId { get; init; }
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
}

public class ListInventoryDocumentsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListInventoryDocumentsQuery, Result<PagedList<InventoryDocumentDto>>>
{
    public async Task<Result<PagedList<InventoryDocumentDto>>> Handle(
        ListInventoryDocumentsQuery request, CancellationToken ct)
    {
        var query = context.InventoryDocuments
            .AsNoTracking()
            .Include(d => d.TransactionType)
            .Where(d => !d.IsDeleted);

        if (request.WarehousePublicId.HasValue)
        {
            var wh = await context.Warehouses.AsNoTracking()
                .FirstOrDefaultAsync(w => w.PublicId == request.WarehousePublicId.Value && !w.IsDeleted, ct);
            if (wh is not null)
            {
                // Filter by warehouse via transactions
                var docIds = await context.InventoryTransactions.AsNoTracking()
                    .Where(t => t.WarehouseId == wh.Id && !t.IsDeleted)
                    .Select(t => new { t.TransactionTypeId, t.SequenceNumber })
                    .Distinct()
                    .ToListAsync(ct);

                var seqNums = docIds.Select(d => d.SequenceNumber).ToHashSet();
                query = query.Where(d => seqNums.Contains(d.SequenceNumber));
            }
        }

        if (request.DateFrom.HasValue)
        {
            var from = request.DateFrom.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(d => d.EntryDate >= from);
        }
        if (request.DateTo.HasValue)
        {
            var to = request.DateTo.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(d => d.EntryDate <= to);
        }

        // Load warehouse names for display
        var warehouseDict = await context.Warehouses.AsNoTracking()
            .Where(w => !w.IsDeleted)
            .ToDictionaryAsync(w => w.Id, w => w.Description, ct);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(d => d.EntryDate)
            .ThenByDescending(d => d.SequenceNumber)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(d => new
            {
                d.PublicId,
                d.SequenceNumber,
                d.EntryDate,
                TransactionTypeName = d.TransactionType != null ? d.TransactionType.Description : "",
                DocClass = d.TransactionType != null ? d.TransactionType.DocumentClass : "",
                d.TotalAmount,
                d.Status
            })
            .ToListAsync(ct);

        var dtos = items.Select(d => new InventoryDocumentDto(
            d.PublicId,
            d.DocClass ?? "",
            d.SequenceNumber,
            DateOnly.FromDateTime(d.EntryDate),
            "", // warehouse resolved at detail level
            d.TransactionTypeName,
            d.TotalAmount,
            d.Status == -1
        )).ToList();

        return Result.Success(new PagedList<InventoryDocumentDto>(
            dtos, totalCount, request.PageNumber, request.PageSize));
    }
}

// --- Get Inventory Document By Id ---

public record GetInventoryDocumentByIdQuery(Guid PublicId) : IRequest<Result<InventoryDocumentDetailDto>>;

public class GetInventoryDocumentByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetInventoryDocumentByIdQuery, Result<InventoryDocumentDetailDto>>
{
    public async Task<Result<InventoryDocumentDetailDto>> Handle(
        GetInventoryDocumentByIdQuery request, CancellationToken ct)
    {
        var document = await context.InventoryDocuments
            .AsNoTracking()
            .Include(d => d.TransactionType)
            .FirstOrDefaultAsync(d => d.PublicId == request.PublicId && !d.IsDeleted, ct);

        if (document is null)
            return Result.Failure<InventoryDocumentDetailDto>(new Error("InvDoc.NotFound",
                "Documento de inventario no encontrado."));

        var transactions = await context.InventoryTransactions
            .AsNoTracking()
            .Include(t => t.Product)
            .Where(t => t.TransactionTypeId == document.TransactionTypeId
                     && t.SequenceNumber == document.SequenceNumber
                     && !t.IsDeleted)
            .ToListAsync(ct);

        var warehouseIds = transactions.Where(t => t.WarehouseId.HasValue)
            .Select(t => t.WarehouseId!.Value).Distinct().ToList();
        var warehouseDict = warehouseIds.Count > 0
            ? await context.Warehouses.AsNoTracking()
                .Where(w => warehouseIds.Contains(w.Id))
                .ToDictionaryAsync(w => w.Id, w => w.Description, ct)
            : new Dictionary<int, string>();

        var lines = transactions.Select(t => new InventoryTransactionLineDto(
            t.PublicId,
            t.Product?.Name ?? "",
            t.Product?.ProductCode ?? 0,
            t.Quantity,
            t.UnitPrice,
            t.SubTotal,
            t.VatAmount,
            t.DiscountAmount,
            t.WarehouseId.HasValue && warehouseDict.TryGetValue(t.WarehouseId.Value, out var wn) ? wn : null
        )).ToList();

        var firstWarehouse = warehouseDict.Values.FirstOrDefault() ?? "";

        var dto = new InventoryDocumentDetailDto(
            document.PublicId,
            document.TransactionType?.DocumentClass ?? "",
            document.SequenceNumber,
            DateOnly.FromDateTime(document.EntryDate),
            firstWarehouse,
            document.TransactionType?.Description ?? "",
            document.TotalAmount,
            document.DiscountAmount,
            document.VatAmount,
            document.Detail,
            document.Status == -1,
            lines);

        return Result.Success(dto);
    }
}

// --- Get Product Stock ---

public record GetProductStockQuery(Guid ProductPublicId) : IRequest<Result<ProductStockDto>>;

public class GetProductStockQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetProductStockQuery, Result<ProductStockDto>>
{
    public async Task<Result<ProductStockDto>> Handle(GetProductStockQuery request, CancellationToken ct)
    {
        var product = await context.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PublicId == request.ProductPublicId && !p.IsDeleted, ct);
        if (product is null)
            return Result.Failure<ProductStockDto>(new Error("Stock.ProductNotFound",
                "Producto no encontrado."));

        var stockByWarehouse = await context.InventoryTransactions
            .AsNoTracking()
            .Where(t => t.ProductId == product.Id && t.WarehouseId.HasValue && !t.IsDeleted)
            .GroupBy(t => t.WarehouseId!.Value)
            .Select(g => new { WarehouseId = g.Key, Stock = g.Sum(t => t.Quantity) })
            .ToListAsync(ct);

        var warehouseIds = stockByWarehouse.Select(s => s.WarehouseId).ToList();
        var warehouses = await context.Warehouses.AsNoTracking()
            .Where(w => warehouseIds.Contains(w.Id))
            .ToDictionaryAsync(w => w.Id, w => new { w.PublicId, w.Description }, ct);

        var byWarehouse = stockByWarehouse.Select(s =>
        {
            var wh = warehouses.GetValueOrDefault(s.WarehouseId);
            return new WarehouseStockDto(
                wh?.PublicId ?? Guid.Empty,
                wh?.Description ?? "",
                s.Stock);
        }).ToList();

        return Result.Success(new ProductStockDto(
            product.PublicId,
            product.Name,
            product.ProductCode,
            product.CurrentStock,
            byWarehouse));
    }
}

// --- Get Stock Movements (Kardex) ---

public record GetStockMovementsQuery : IRequest<Result<List<StockMovementDto>>>
{
    public Guid ProductPublicId { get; init; }
    public Guid? WarehousePublicId { get; init; }
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }
}

public class GetStockMovementsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetStockMovementsQuery, Result<List<StockMovementDto>>>
{
    public async Task<Result<List<StockMovementDto>>> Handle(
        GetStockMovementsQuery request, CancellationToken ct)
    {
        var product = await context.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PublicId == request.ProductPublicId && !p.IsDeleted, ct);
        if (product is null)
            return Result.Failure<List<StockMovementDto>>(new Error("Kardex.ProductNotFound",
                "Producto no encontrado."));

        var query = context.InventoryTransactions
            .AsNoTracking()
            .Include(t => t.TransactionType)
            .Where(t => t.ProductId == product.Id && !t.IsDeleted);

        if (request.WarehousePublicId.HasValue)
        {
            var wh = await context.Warehouses.AsNoTracking()
                .FirstOrDefaultAsync(w => w.PublicId == request.WarehousePublicId.Value, ct);
            if (wh is not null)
                query = query.Where(t => t.WarehouseId == wh.Id);
        }

        if (request.DateFrom.HasValue)
            query = query.Where(t => t.TransactionDate >= request.DateFrom.Value);
        if (request.DateTo.HasValue)
            query = query.Where(t => t.TransactionDate <= request.DateTo.Value);

        var transactions = await query
            .OrderBy(t => t.TransactionDate)
            .ThenBy(t => t.ConsecutiveNumber)
            .ToListAsync(ct);

        var warehouseIds = transactions.Where(t => t.WarehouseId.HasValue)
            .Select(t => t.WarehouseId!.Value).Distinct().ToList();
        var warehouseDict = warehouseIds.Count > 0
            ? await context.Warehouses.AsNoTracking()
                .Where(w => warehouseIds.Contains(w.Id))
                .ToDictionaryAsync(w => w.Id, w => w.Description, ct)
            : new Dictionary<int, string>();

        var movements = transactions.Select(t => new StockMovementDto(
            t.PublicId,
            t.TransactionDate,
            t.SequenceNumber,
            t.TransactionType?.Description ?? "",
            t.Quantity,
            t.UnitPrice,
            t.SubTotal,
            t.WarehouseId.HasValue && warehouseDict.TryGetValue(t.WarehouseId.Value, out var wn) ? wn : null
        )).ToList();

        return Result.Success(movements);
    }
}
