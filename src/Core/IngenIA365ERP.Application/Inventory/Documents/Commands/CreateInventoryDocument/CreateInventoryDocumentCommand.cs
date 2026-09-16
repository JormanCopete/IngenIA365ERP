using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents.Commands.CreateInventoryDocument;

public record InventoryLineDto(
    Guid ProductPublicId,
    decimal Quantity,
    decimal UnitCost,
    Guid? LocationPublicId);

public record CreateInventoryDocumentCommand : IRequest<Result<Guid>>
{
    /// <summary>E=entry, S=exit, T=transfer, A=adjust</summary>
    public string DocumentType { get; init; } = string.Empty;
    public Guid TransactionTypePublicId { get; init; }
    public Guid WarehousePublicId { get; init; }
    public Guid? DestinationWarehousePublicId { get; init; }
    public DateOnly Date { get; init; }
    public string? Reference { get; init; }
    public string? Description { get; init; }
    public List<InventoryLineDto> Lines { get; init; } = [];
}

public class CreateInventoryDocumentCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateInventoryDocumentCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateInventoryDocumentCommand request, CancellationToken ct)
    {
        // 1. Resolve TransactionType
        var txType = await context.InventoryTransactionTypes.FirstOrDefaultAsync(
            t => t.PublicId == request.TransactionTypePublicId && !t.IsDeleted, ct);
        if (txType is null)
            return Result.Failure<Guid>(new Error("InvDoc.TxTypeNotFound",
                "Tipo de movimiento no encontrado."));

        // 2. Resolve source Warehouse
        var warehouse = await context.Warehouses.FirstOrDefaultAsync(
            w => w.PublicId == request.WarehousePublicId && !w.IsDeleted, ct);
        if (warehouse is null)
            return Result.Failure<Guid>(new Error("InvDoc.WarehouseNotFound",
                "Bodega de origen no encontrada."));

        // 3. Resolve destination warehouse for transfers
        Warehouse? destWarehouse = null;
        if (request.DocumentType == "T")
        {
            if (!request.DestinationWarehousePublicId.HasValue)
                return Result.Failure<Guid>(new Error("InvDoc.DestWarehouseRequired",
                    "Bodega destino requerida para transferencias."));

            destWarehouse = await context.Warehouses.FirstOrDefaultAsync(
                w => w.PublicId == request.DestinationWarehousePublicId.Value && !w.IsDeleted, ct);
            if (destWarehouse is null)
                return Result.Failure<Guid>(new Error("InvDoc.DestWarehouseNotFound",
                    "Bodega destino no encontrada."));
        }

        // 4. Resolve products and validate
        var resolvedLines = new List<(Product Product, int? LocationId, decimal Quantity, decimal UnitCost)>();
        foreach (var line in request.Lines)
        {
            var product = await context.Products.FirstOrDefaultAsync(
                p => p.PublicId == line.ProductPublicId && !p.IsDeleted, ct);
            if (product is null)
                return Result.Failure<Guid>(new Error("InvDoc.ProductNotFound",
                    $"Producto no encontrado: {line.ProductPublicId}"));
            if (!product.IsActive)
                return Result.Failure<Guid>(new Error("InvDoc.ProductInactive",
                    $"Producto inactivo: {product.Name}"));

            int? locationId = null;
            if (line.LocationPublicId.HasValue)
            {
                var loc = await context.Locations.AsNoTracking().FirstOrDefaultAsync(
                    l => l.PublicId == line.LocationPublicId.Value && !l.IsDeleted, ct);
                locationId = loc?.Id;
            }

            resolvedLines.Add((product, locationId, line.Quantity, line.UnitCost));
        }

        // 5. For exits/transfers: validate stock
        if (request.DocumentType is "S" or "T")
        {
            foreach (var (product, _, qty, _) in resolvedLines)
            {
                var currentStock = await context.InventoryTransactions
                    .Where(t => t.ProductId == product.Id
                             && t.WarehouseId == warehouse.Id
                             && !t.IsDeleted)
                    .SumAsync(t => t.Quantity, ct);

                if (currentStock < qty)
                    return Result.Failure<Guid>(new Error("InvDoc.InsufficientStock",
                        $"Stock insuficiente para '{product.Name}': disponible={currentStock}, requerido={qty}"));
            }
        }

        // 6. Generate document number
        var nextSeq = txType.SequenceNumber + 1;
        txType.SequenceNumber = nextSeq;

        // 7. Create InventoryDocument (header)
        var totalAmount = resolvedLines.Sum(l => l.Quantity * l.UnitCost);
        var document = new InventoryDocument
        {
            TransactionTypeId = txType.Id,
            SequenceNumber = nextSeq,
            EntryDate = request.Date.ToDateTime(TimeOnly.MinValue),
            TotalAmount = totalAmount,
            Status = 1,
            Detail = request.Description ?? "",
            ItemCount = resolvedLines.Count,
            UserId = currentUser.UserName,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };
        context.InventoryDocuments.Add(document);

        // 8. Create InventoryTransaction per line
        long consecutiveBase = await context.InventoryTransactions
            .Where(t => !t.IsDeleted)
            .OrderByDescending(t => t.ConsecutiveNumber)
            .Select(t => t.ConsecutiveNumber)
            .FirstOrDefaultAsync(ct);

        foreach (var (product, locationId, qty, unitCost) in resolvedLines)
        {
            consecutiveBase++;

            // Determine signed quantity based on doc type
            int signedQty;
            switch (request.DocumentType)
            {
                case "E": // Entry: positive
                case "A": // Adjust: positive (assumes positive adjust)
                    signedQty = (int)qty;
                    break;
                case "S": // Exit: negative
                    signedQty = -(int)qty;
                    break;
                case "T": // Transfer: negative from origin
                    signedQty = -(int)qty;
                    break;
                default:
                    signedQty = (int)qty;
                    break;
            }

            var txn = new InventoryTransaction
            {
                TransactionTypeId = txType.Id,
                SequenceNumber = nextSeq,
                TransactionDate = request.Date,
                ProductId = product.Id,
                Quantity = signedQty,
                UnitPrice = unitCost,
                SubTotal = Math.Abs(qty) * unitCost,
                NetTotal = Math.Abs(qty) * unitCost,
                WarehouseId = warehouse.Id,
                LocationId = locationId,
                ConsecutiveNumber = consecutiveBase,
                SystemDate = dateTime.UtcNow,
                UserId = currentUser.UserName,
                CreatedAt = dateTime.UtcNow,
                CreatedBy = currentUser.UserName
            };
            context.InventoryTransactions.Add(txn);

            // Update product stock
            product.CurrentStock += signedQty;

            // For transfers: create positive entry in destination warehouse
            if (request.DocumentType == "T" && destWarehouse is not null)
            {
                consecutiveBase++;
                var destTxn = new InventoryTransaction
                {
                    TransactionTypeId = txType.Id,
                    SequenceNumber = nextSeq,
                    TransactionDate = request.Date,
                    ProductId = product.Id,
                    Quantity = (int)qty, // positive in destination
                    UnitPrice = unitCost,
                    SubTotal = qty * unitCost,
                    NetTotal = qty * unitCost,
                    WarehouseId = destWarehouse.Id,
                    LocationId = locationId,
                    ConsecutiveNumber = consecutiveBase,
                    SystemDate = dateTime.UtcNow,
                    UserId = currentUser.UserName,
                    CreatedAt = dateTime.UtcNow,
                    CreatedBy = currentUser.UserName
                };
                context.InventoryTransactions.Add(destTxn);
                // Stock net effect is zero for transfers (product level)
                product.CurrentStock += (int)qty;
            }
        }

        // E3 (feature 009): contabilización por AccountingPoster pendiente

        await context.SaveChangesAsync(ct);
        return Result.Success(document.PublicId);
    }
}

public class CreateInventoryDocumentCommandValidator : AbstractValidator<CreateInventoryDocumentCommand>
{
    private static readonly string[] ValidTypes = ["E", "S", "T", "A"];

    public CreateInventoryDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentType)
            .NotEmpty().WithMessage("Tipo de documento requerido.")
            .Must(t => ValidTypes.Contains(t))
            .WithMessage("Tipo de documento invalido. Use E=entrada, S=salida, T=traslado, A=ajuste.");

        RuleFor(x => x.TransactionTypePublicId)
            .NotEmpty().WithMessage("Tipo de movimiento requerido.");

        RuleFor(x => x.WarehousePublicId)
            .NotEmpty().WithMessage("Bodega requerida.");

        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("Fecha requerida.");

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("Debe incluir al menos 1 linea.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductPublicId)
                .NotEmpty().WithMessage("Producto requerido.");
            line.RuleFor(l => l.Quantity)
                .GreaterThan(0).WithMessage("Cantidad debe ser mayor a 0.");
            line.RuleFor(l => l.UnitCost)
                .GreaterThanOrEqualTo(0).WithMessage("Costo unitario no puede ser negativo.");
        });

        RuleFor(x => x.DestinationWarehousePublicId)
            .NotEmpty()
            .When(x => x.DocumentType == "T")
            .WithMessage("Bodega destino requerida para traslados.");
    }
}
