using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Reports;

// --- DTOs ---

public record InventoryValuationLineDto(
    Guid ProductPublicId,
    string ProductCode,
    string ProductName,
    string GroupName,
    string WarehouseName,
    decimal Stock,
    decimal UnitCost,
    decimal TotalCost);

public record InventoryValuationDto(
    DateTime AsOfDate,
    string? WarehouseName,
    List<InventoryValuationLineDto> Lines,
    decimal GrandTotal,
    int TotalProducts);

// --- Query ---

public record GetInventoryValuationQuery(
    Guid? WarehousePublicId,
    int? GroupId) : IRequest<Result<InventoryValuationDto>>;

public class GetInventoryValuationQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetInventoryValuationQuery, Result<InventoryValuationDto>>
{
    public async Task<Result<InventoryValuationDto>> Handle(GetInventoryValuationQuery request, CancellationToken ct)
    {
        int? warehouseId = null;
        string? warehouseName = null;

        if (request.WarehousePublicId.HasValue)
        {
            var wh = await context.Warehouses.AsNoTracking()
                .FirstOrDefaultAsync(w => w.PublicId == request.WarehousePublicId.Value && !w.IsDeleted, ct);
            if (wh is not null)
            {
                warehouseId = wh.Id;
                warehouseName = wh.Description;
            }
        }

        var productsQuery = context.Products.AsNoTracking()
            .Where(p => !p.IsDeleted);

        if (request.GroupId.HasValue)
            productsQuery = productsQuery.Where(p => p.GroupId == request.GroupId.Value);

        var products = await productsQuery.OrderBy(p => p.ProductCode).ToListAsync(ct);

        // Load group names for display
        var groupIds = products.Where(p => p.GroupId.HasValue).Select(p => p.GroupId!.Value).Distinct().ToList();
        var groupMap = groupIds.Count > 0
            ? await context.ProductGroups.AsNoTracking()
                .Where(g => groupIds.Contains(g.Id))
                .ToDictionaryAsync(g => g.Id, g => g.Name ?? g.Id.ToString(), ct)
            : new Dictionary<int, string>();

        var lines = products.Select(p => new InventoryValuationLineDto(
            p.PublicId,
            p.ProductCode.ToString(),
            p.Name,
            p.GroupId.HasValue && groupMap.TryGetValue(p.GroupId.Value, out var gn) ? gn : "",
            warehouseName ?? "Todas",
            p.CurrentStock,
            p.CostPrice,
            p.CurrentStock * p.CostPrice)).ToList();

        return Result.Success(new InventoryValuationDto(
            DateTime.UtcNow,
            warehouseName,
            lines,
            lines.Sum(l => l.TotalCost),
            lines.Count));
    }
}
