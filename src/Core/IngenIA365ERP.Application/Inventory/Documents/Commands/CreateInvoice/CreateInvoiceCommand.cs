using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Inventory;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents.Commands.CreateInvoice;

public record InvoiceLineDto(
    Guid ProductPublicId,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPercent);

public record CreateInvoiceCommand : IRequest<Result<Guid>>
{
    public Guid CustomerPublicId { get; init; }
    public Guid WarehousePublicId { get; init; }
    public DateOnly Date { get; init; }
    public string? Description { get; init; }
    public List<InvoiceLineDto> Lines { get; init; } = [];
}

public class CreateInvoiceCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateInvoiceCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateInvoiceCommand request, CancellationToken ct)
    {
        // 1. Resolve customer
        var customer = await context.People.FirstOrDefaultAsync(
            p => p.PublicId == request.CustomerPublicId && !p.IsDeleted, ct);
        if (customer is null)
            return Result.Failure<Guid>(new Error("Invoice.CustomerNotFound",
                "Cliente no encontrado."));

        // 2. Resolve warehouse
        var warehouse = await context.Warehouses.FirstOrDefaultAsync(
            w => w.PublicId == request.WarehousePublicId && !w.IsDeleted, ct);
        if (warehouse is null)
            return Result.Failure<Guid>(new Error("Invoice.WarehouseNotFound",
                "Bodega no encontrada."));

        // 3. Get invoice config
        var invoiceConfig = await context.InventoryInvoices
            .Where(i => !i.IsDeleted)
            .OrderByDescending(i => i.Id)
            .FirstOrDefaultAsync(ct);

        // 4. Resolve products, validate stock, calc amounts
        decimal totalSubtotal = 0, totalDiscount = 0, totalVat = 0, totalNet = 0;
        var resolvedLines = new List<(Product Product, decimal Qty, decimal UnitPrice, decimal DiscPct,
            decimal Subtotal, decimal Discount, decimal Vat, decimal Net)>();

        foreach (var line in request.Lines)
        {
            var product = await context.Products.FirstOrDefaultAsync(
                p => p.PublicId == line.ProductPublicId && !p.IsDeleted, ct);
            if (product is null)
                return Result.Failure<Guid>(new Error("Invoice.ProductNotFound",
                    $"Producto no encontrado: {line.ProductPublicId}"));
            if (!product.IsActive)
                return Result.Failure<Guid>(new Error("Invoice.ProductInactive",
                    $"Producto inactivo: {product.Name}"));

            // Validate stock
            if (product.ControlsStock && product.CurrentStock < (int)line.Quantity)
                return Result.Failure<Guid>(new Error("Invoice.InsufficientStock",
                    $"Stock insuficiente para '{product.Name}': disponible={product.CurrentStock}, requerido={line.Quantity}"));

            var subtotal = line.Quantity * line.UnitPrice;
            var discount = subtotal * (line.DiscountPercent / 100m);
            var taxableBase = subtotal - discount;
            var vat = taxableBase * (product.VatRate / 100m);
            var net = taxableBase + vat;

            resolvedLines.Add((product, line.Quantity, line.UnitPrice, line.DiscountPercent,
                subtotal, discount, vat, net));

            totalSubtotal += subtotal;
            totalDiscount += discount;
            totalVat += vat;
            totalNet += net;
        }

        // 5. Find exit transaction type (first one with DocumentClass = "FV" or first exit type)
        var exitTxType = await context.InventoryTransactionTypes
            .Where(t => !t.IsDeleted && (t.DocumentClass == "FV" || t.InvoiceControl == "1"))
            .FirstOrDefaultAsync(ct);
        exitTxType ??= await context.InventoryTransactionTypes
            .Where(t => !t.IsDeleted)
            .FirstOrDefaultAsync(ct);

        if (exitTxType is null)
            return Result.Failure<Guid>(new Error("Invoice.NoTxType",
                "No se encontro tipo de movimiento para facturacion."));

        // 6. Generate invoice number
        var nextInvoiceNum = (invoiceConfig?.InvoiceConsecutive ?? 0) + 1;
        if (invoiceConfig is not null)
            invoiceConfig.InvoiceConsecutive = nextInvoiceNum;

        // 7. Generate document sequence
        var nextSeq = exitTxType.SequenceNumber + 1;
        exitTxType.SequenceNumber = nextSeq;

        // 8. Create InventoryDocument header (exit)
        var document = new InventoryDocument
        {
            TransactionTypeId = exitTxType.Id,
            SequenceNumber = nextSeq,
            CustomerId = customer.Id,
            EntryDate = request.Date.ToDateTime(TimeOnly.MinValue),
            TotalAmount = totalNet,
            DiscountAmount = totalDiscount,
            VatAmount = totalVat,
            Status = 1,
            Detail = request.Description ?? $"Factura #{nextInvoiceNum}",
            ItemCount = resolvedLines.Count,
            InvoiceNumber = nextInvoiceNum,
            UserId = currentUser.UserName,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };
        context.InventoryDocuments.Add(document);

        // 9. Create transactions (exit from stock) and update stock
        long consecutiveBase = await context.InventoryTransactions
            .Where(t => !t.IsDeleted)
            .OrderByDescending(t => t.ConsecutiveNumber)
            .Select(t => t.ConsecutiveNumber)
            .FirstOrDefaultAsync(ct);

        foreach (var (product, qty, unitPrice, discPct, subtotal, discount, vat, net) in resolvedLines)
        {
            consecutiveBase++;
            var txn = new InventoryTransaction
            {
                TransactionTypeId = exitTxType.Id,
                SequenceNumber = nextSeq,
                TransactionDate = request.Date,
                InvoiceNumber = nextInvoiceNum.ToString(),
                ProductId = product.Id,
                Quantity = -(int)qty, // Exit: negative
                UnitPrice = unitPrice,
                VatRate = product.VatRate,
                DiscountRate = discPct,
                VatAmount = vat,
                DiscountAmount = discount,
                SubTotal = subtotal,
                NetTotal = net,
                CustomerId = customer.Id,
                WarehouseId = warehouse.Id,
                ConsecutiveNumber = consecutiveBase,
                SystemDate = dateTime.UtcNow,
                UserId = currentUser.UserName,
                CreatedAt = dateTime.UtcNow,
                CreatedBy = currentUser.UserName
            };
            context.InventoryTransactions.Add(txn);

            // Decrease stock
            product.CurrentStock -= (int)qty;
        }

        // 10. Create AccountingDocument (Debit: CxC, Credit: Revenue + VAT)
        if (exitTxType.TransfersAccounting && !string.IsNullOrEmpty(exitTxType.TransactionVoucherCode))
        {
            var voucherType = await context.VoucherTypes.FirstOrDefaultAsync(
                v => v.Code == exitTxType.TransactionVoucherCode && !v.IsDeleted, ct);

            if (voucherType is not null)
            {
                var nextAccNum = voucherType.NextSequenceNumber + 1;
                voucherType.NextSequenceNumber = nextAccNum;
                var periodCode = request.Date.Year * 100 + request.Date.Month;

                var accDoc = new AccountingDocument
                {
                    VoucherTypeCode = voucherType.Code,
                    DocumentNumber = nextAccNum,
                    Detail = $"Factura #{nextInvoiceNum} - {customer.FirstName} {customer.LastName}",
                    TotalDebit = totalNet,
                    TotalCredit = totalNet,
                    DocumentDate = request.Date,
                    IsClosed = false,
                    IsVoided = false,
                    PeriodCode = periodCode,
                    ModuleCode = "INV",
                    CreatedAt = dateTime.UtcNow,
                    CreatedBy = currentUser.UserName
                };
                context.AccountingDocuments.Add(accDoc);
            }
        }

        await context.SaveChangesAsync(ct);
        return Result.Success(document.PublicId);
    }
}

public class CreateInvoiceCommandValidator : AbstractValidator<CreateInvoiceCommand>
{
    public CreateInvoiceCommandValidator()
    {
        RuleFor(x => x.CustomerPublicId)
            .NotEmpty().WithMessage("Cliente requerido.");

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
            line.RuleFor(l => l.UnitPrice)
                .GreaterThan(0).WithMessage("Precio unitario debe ser mayor a 0.");
            line.RuleFor(l => l.DiscountPercent)
                .InclusiveBetween(0, 100).WithMessage("Descuento debe estar entre 0 y 100.");
        });
    }
}
