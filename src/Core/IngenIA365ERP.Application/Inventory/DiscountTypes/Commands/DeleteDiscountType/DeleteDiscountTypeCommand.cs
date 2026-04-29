using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.DiscountTypes.Commands.DeleteDiscountType;

public record DeleteDiscountTypeCommand(Guid PublicId) : IRequest<Result>;

public class DeleteDiscountTypeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteDiscountTypeCommand, Result>
{
    public async Task<Result> Handle(
        DeleteDiscountTypeCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.DiscountTypes
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.IsDeleted = true;
        entity.DeletedAt = dateTime.UtcNow;
        entity.DeletedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
