using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Documents.Commands.VoidInventoryDocument;

public record VoidInventoryDocumentCommand(Guid PublicId, string? Reason) : IRequest<Result>;

public class VoidInventoryDocumentCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<VoidInventoryDocumentCommand, Result>
{
    public async Task<Result> Handle(VoidInventoryDocumentCommand request, CancellationToken ct)
    {
        // 1. Find the inventory document
        var document = await context.InventoryDocuments.FirstOrDefaultAsync(
            d => d.PublicId == request.PublicId && !d.IsDeleted, ct);
        if (document is null)
            return Result.Failure(new Error("InvDoc.NotFound",
                "Documento de inventario no encontrado."));

        // 2. Validate not already voided
        if (document.Status == -1)
            return Result.Failure(new Error("InvDoc.AlreadyVoided",
                "El documento ya fue anulado."));

        // 3. Mark document as voided
        document.Status = -1;
        document.Detail = $"[ANULADO] {request.Reason ?? ""} - {document.Detail}";
        document.UpdatedAt = dateTime.UtcNow;
        document.UpdatedBy = currentUser.UserName;

        // 4. Reverse stock movements
        var transactions = await context.InventoryTransactions
            .Where(t => t.TransactionTypeId == document.TransactionTypeId
                     && t.SequenceNumber == document.SequenceNumber
                     && !t.IsDeleted)
            .ToListAsync(ct);

        foreach (var txn in transactions)
        {
            // Reverse the product stock
            var product = await context.Products.FirstOrDefaultAsync(
                p => p.Id == txn.ProductId && !p.IsDeleted, ct);
            if (product is not null)
            {
                product.CurrentStock -= txn.Quantity; // reverse the original movement
                product.UpdatedAt = dateTime.UtcNow;
                product.UpdatedBy = currentUser.UserName;
            }

            // Soft-delete the transaction
            txn.IsDeleted = true;
            txn.DeletedAt = dateTime.UtcNow;
            txn.DeletedBy = currentUser.UserName;
        }

        // 5. Reverse accounting entry if exists
        var txType = await context.InventoryTransactionTypes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == document.TransactionTypeId, ct);

        // E3 (feature 009): contabilización por AccountingPoster pendiente

        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
