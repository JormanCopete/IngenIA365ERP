using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.ProductAccounts.Commands.CreateProductAccount;

public record CreateProductAccountCommand : IRequest<Result<Guid>>
{
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

public class CreateProductAccountCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateProductAccountCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateProductAccountCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new ProductAccount
        {
            ProductGroupId = request.ProductGroupId,
            TransactionTypeId = request.TransactionTypeId,
            WarehouseId = request.WarehouseId,
            LocationId = request.LocationId,
            VatAccountCode = request.VatAccountCode,
            DiscountAccountCode = request.DiscountAccountCode,
            TaxableSalesAccountCode = request.TaxableSalesAccountCode,
            NonTaxableSalesAccountCode = request.NonTaxableSalesAccountCode,
            NetAccountCode = request.NetAccountCode,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.ProductAccounts.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateProductAccountCommandValidator : AbstractValidator<CreateProductAccountCommand>
{
    public CreateProductAccountCommandValidator()
    {
        RuleFor(x => x.VatAccountCode)
            .MaximumLength(15).WithMessage("VAT account code must not exceed 15 characters.");

        RuleFor(x => x.DiscountAccountCode)
            .MaximumLength(15).WithMessage("Discount account code must not exceed 15 characters.");

        RuleFor(x => x.TaxableSalesAccountCode)
            .MaximumLength(15).WithMessage("Taxable sales account code must not exceed 15 characters.");

        RuleFor(x => x.NonTaxableSalesAccountCode)
            .MaximumLength(15).WithMessage("Non-taxable sales account code must not exceed 15 characters.");

        RuleFor(x => x.NetAccountCode)
            .MaximumLength(15).WithMessage("Net account code must not exceed 15 characters.");
    }
}
