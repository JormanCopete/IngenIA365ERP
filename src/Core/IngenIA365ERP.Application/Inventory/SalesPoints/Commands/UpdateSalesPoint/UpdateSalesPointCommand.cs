using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.SalesPoints.Commands.UpdateSalesPoint;

public record UpdateSalesPointCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int PointCode { get; init; }
    public string? UserId { get; init; }
    public int? TransactionTypeId { get; init; }
    public string? PrinterName { get; init; }
    public int Status { get; init; }
    public DateTime? DateId { get; init; }
    public int? ShiftId { get; init; }
    public decimal BaseAmount { get; init; }
    public int? WarehouseId { get; init; }
    public int? LocationId { get; init; }
}

public class UpdateSalesPointCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateSalesPointCommand, Result>
{
    public async Task<Result> Handle(
        UpdateSalesPointCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.SalesPoints
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.PointCode = request.PointCode;
        entity.UserId = request.UserId;
        entity.TransactionTypeId = request.TransactionTypeId;
        entity.PrinterName = request.PrinterName;
        entity.Status = request.Status;
        entity.DateId = request.DateId;
        entity.ShiftId = request.ShiftId;
        entity.BaseAmount = request.BaseAmount;
        entity.WarehouseId = request.WarehouseId;
        entity.LocationId = request.LocationId;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
