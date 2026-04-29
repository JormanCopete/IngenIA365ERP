using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.VatAccounts.Commands.CreateVatAccount;

public record CreateVatAccountCommand : IRequest<Result<Guid>>
{
    public int ProductGroupId { get; init; }
    public int TransactionTypeId { get; init; }
    public int WarehouseId { get; init; }
    public int LocationId { get; init; }
    public decimal VatRate { get; init; }
    public string? AccountType { get; init; }
    public string? AccountCode { get; init; }
}

public class CreateVatAccountCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateVatAccountCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateVatAccountCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new VatAccount
        {
            ProductGroupId = request.ProductGroupId,
            TransactionTypeId = request.TransactionTypeId,
            WarehouseId = request.WarehouseId,
            LocationId = request.LocationId,
            VatRate = request.VatRate,
            AccountType = request.AccountType,
            AccountCode = request.AccountCode,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.VatAccounts.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateVatAccountCommandValidator : AbstractValidator<CreateVatAccountCommand>
{
    public CreateVatAccountCommandValidator()
    {
        RuleFor(x => x.AccountType)
            .MaximumLength(5).WithMessage("Account type must not exceed 5 characters.");

        RuleFor(x => x.AccountCode)
            .MaximumLength(15).WithMessage("Account code must not exceed 15 characters.");
    }
}
