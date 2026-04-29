using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.InventoryTransactionTypes.Commands.CreateInventoryTransactionType;

public record CreateInventoryTransactionTypeCommand : IRequest<Result<Guid>>
{
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

public class CreateInventoryTransactionTypeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateInventoryTransactionTypeCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateInventoryTransactionTypeCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new InventoryTransactionType
        {
            TypeCode = request.TypeCode,
            Description = request.Description,
            ShortDescription = request.ShortDescription,
            TransactionVoucherCode = request.TransactionVoucherCode,
            CostVoucherCode = request.CostVoucherCode,
            SequenceNumber = request.SequenceNumber,
            ControlsStock = request.ControlsStock,
            DocumentClass = request.DocumentClass,
            UpdatesAccounting = request.UpdatesAccounting,
            PortfolioVoucherCode = request.PortfolioVoucherCode,
            CreditLineId = request.CreditLineId,
            DeductionType = request.DeductionType,
            InvoiceControl = request.InvoiceControl,
            TotalInPurchase = request.TotalInPurchase,
            CostsProducts = request.CostsProducts,
            IsReturn = request.IsReturn,
            TransfersAccounting = request.TransfersAccounting,
            AllowsBonus = request.AllowsBonus,
            ValidatesCreditLimit = request.ValidatesCreditLimit,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.InventoryTransactionTypes.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateInventoryTransactionTypeCommandValidator : AbstractValidator<CreateInventoryTransactionTypeCommand>
{
    public CreateInventoryTransactionTypeCommandValidator()
    {
        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(150).WithMessage("Description must not exceed 150 characters.");

        RuleFor(x => x.ShortDescription)
            .MaximumLength(80).WithMessage("Short description must not exceed 80 characters.");

        RuleFor(x => x.TransactionVoucherCode)
            .MaximumLength(5).WithMessage("Transaction voucher code must not exceed 5 characters.");

        RuleFor(x => x.CostVoucherCode)
            .MaximumLength(5).WithMessage("Cost voucher code must not exceed 5 characters.");
    }
}
