using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.VatAccounts.Commands.UpdateVatAccount;

public record UpdateVatAccountCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int ProductGroupId { get; init; }
    public int TransactionTypeId { get; init; }
    public int WarehouseId { get; init; }
    public int LocationId { get; init; }
    public decimal VatRate { get; init; }
    public string? AccountType { get; init; }
    public string? AccountCode { get; init; }
}

public class UpdateVatAccountCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateVatAccountCommand, Result>
{
    public async Task<Result> Handle(
        UpdateVatAccountCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.VatAccounts
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.ProductGroupId = request.ProductGroupId;
        entity.TransactionTypeId = request.TransactionTypeId;
        entity.WarehouseId = request.WarehouseId;
        entity.LocationId = request.LocationId;
        entity.VatRate = request.VatRate;
        entity.AccountType = request.AccountType;
        entity.AccountCode = request.AccountCode;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
