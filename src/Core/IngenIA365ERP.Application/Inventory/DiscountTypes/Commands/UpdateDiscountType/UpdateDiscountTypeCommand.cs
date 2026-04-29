using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.DiscountTypes.Commands.UpdateDiscountType;

public record UpdateDiscountTypeCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public int TypeCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public int? DiscountClass { get; init; }
}

public class UpdateDiscountTypeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateDiscountTypeCommand, Result>
{
    public async Task<Result> Handle(
        UpdateDiscountTypeCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.DiscountTypes
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.TypeCode = request.TypeCode;
        entity.Name = request.Name;
        entity.ShortName = request.ShortName;
        entity.DiscountClass = request.DiscountClass;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
