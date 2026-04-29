using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.ExternalEntities.Commands.DeleteExternalEntity;

public record DeleteExternalEntityCommand(Guid PublicId) : IRequest<Result>;

public class DeleteExternalEntityCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteExternalEntityCommand, Result>
{
    public async Task<Result> Handle(
        DeleteExternalEntityCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.ExternalEntities
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
