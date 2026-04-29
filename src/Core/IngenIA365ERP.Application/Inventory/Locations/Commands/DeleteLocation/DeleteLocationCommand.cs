using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.Locations.Commands.DeleteLocation;

public record DeleteLocationCommand(Guid PublicId) : IRequest<Result>;

public class DeleteLocationCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteLocationCommand, Result>
{
    public async Task<Result> Handle(
        DeleteLocationCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.Locations
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
