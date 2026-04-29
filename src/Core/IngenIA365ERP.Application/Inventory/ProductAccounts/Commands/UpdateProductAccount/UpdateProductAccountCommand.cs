using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.ProductAccounts.Commands.UpdateProductAccount;

public record UpdateProductAccountCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
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

public class UpdateProductAccountCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateProductAccountCommand, Result>
{
    public async Task<Result> Handle(
        UpdateProductAccountCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.ProductAccounts
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.ProductGroupId = request.ProductGroupId;
        entity.TransactionTypeId = request.TransactionTypeId;
        entity.WarehouseId = request.WarehouseId;
        entity.LocationId = request.LocationId;
        entity.VatAccountCode = request.VatAccountCode;
        entity.DiscountAccountCode = request.DiscountAccountCode;
        entity.TaxableSalesAccountCode = request.TaxableSalesAccountCode;
        entity.NonTaxableSalesAccountCode = request.NonTaxableSalesAccountCode;
        entity.NetAccountCode = request.NetAccountCode;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
