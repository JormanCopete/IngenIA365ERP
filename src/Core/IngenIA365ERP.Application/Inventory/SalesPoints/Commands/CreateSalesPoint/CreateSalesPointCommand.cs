using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory;
using MediatR;

namespace IngenIA365ERP.Application.Inventory.SalesPoints.Commands.CreateSalesPoint;

public record CreateSalesPointCommand : IRequest<Result<Guid>>
{
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

public class CreateSalesPointCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<CreateSalesPointCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateSalesPointCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new SalesPoint
        {
            PointCode = request.PointCode,
            UserId = request.UserId,
            TransactionTypeId = request.TransactionTypeId,
            PrinterName = request.PrinterName,
            Status = request.Status,
            DateId = request.DateId,
            ShiftId = request.ShiftId,
            BaseAmount = request.BaseAmount,
            WarehouseId = request.WarehouseId,
            LocationId = request.LocationId,
            CreatedAt = dateTime.UtcNow,
            CreatedBy = currentUser.UserName
        };

        context.SalesPoints.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success(entity.PublicId);
    }
}

public class CreateSalesPointCommandValidator : AbstractValidator<CreateSalesPointCommand>
{
    public CreateSalesPointCommandValidator()
    {
        RuleFor(x => x.UserId)
            .MaximumLength(20).WithMessage("User ID must not exceed 20 characters.");

        RuleFor(x => x.PrinterName)
            .MaximumLength(50).WithMessage("Printer name must not exceed 50 characters.");
    }
}
