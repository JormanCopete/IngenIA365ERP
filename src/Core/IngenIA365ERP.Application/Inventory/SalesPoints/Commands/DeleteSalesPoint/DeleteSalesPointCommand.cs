using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.SalesPoints.Commands.DeleteSalesPoint;

public record DeleteSalesPointCommand(Guid PublicId) : IRequest<Result>;

public class DeleteSalesPointCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteSalesPointCommand, Result>
{
    public async Task<Result> Handle(
        DeleteSalesPointCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.SalesPoints
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
