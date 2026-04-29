using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Warehouses.Commands.UpdateWarehouse;

public record UpdateWarehouseCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int WarehouseCode { get; init; }
    public int LocationId { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? ShortDescription { get; init; }
}

public class UpdateWarehouseCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateWarehouseCommand, Result>
{
    public async Task<Result> Handle(
        UpdateWarehouseCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.Warehouses
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.WarehouseCode = request.WarehouseCode;
        entity.LocationId = request.LocationId;
        entity.Description = request.Description;
        entity.ShortDescription = request.ShortDescription;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
