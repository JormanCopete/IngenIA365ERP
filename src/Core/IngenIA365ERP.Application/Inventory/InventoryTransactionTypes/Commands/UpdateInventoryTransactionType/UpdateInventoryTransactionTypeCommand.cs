using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.InventoryTransactionTypes.Commands.UpdateInventoryTransactionType;

public record UpdateInventoryTransactionTypeCommand : IRequest<Result>
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

public class UpdateInventoryTransactionTypeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateInventoryTransactionTypeCommand, Result>
{
    public async Task<Result> Handle(
        UpdateInventoryTransactionTypeCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.InventoryTransactionTypes
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.TypeCode = request.TypeCode;
        entity.Description = request.Description;
        entity.ShortDescription = request.ShortDescription;
        entity.TransactionVoucherCode = request.TransactionVoucherCode;
        entity.CostVoucherCode = request.CostVoucherCode;
        entity.SequenceNumber = request.SequenceNumber;
        entity.ControlsStock = request.ControlsStock;
        entity.DocumentClass = request.DocumentClass;
        entity.UpdatesAccounting = request.UpdatesAccounting;
        entity.PortfolioVoucherCode = request.PortfolioVoucherCode;
        entity.CreditLineId = request.CreditLineId;
        entity.DeductionType = request.DeductionType;
        entity.InvoiceControl = request.InvoiceControl;
        entity.TotalInPurchase = request.TotalInPurchase;
        entity.CostsProducts = request.CostsProducts;
        entity.IsReturn = request.IsReturn;
        entity.TransfersAccounting = request.TransfersAccounting;
        entity.AllowsBonus = request.AllowsBonus;
        entity.ValidatesCreditLimit = request.ValidatesCreditLimit;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
