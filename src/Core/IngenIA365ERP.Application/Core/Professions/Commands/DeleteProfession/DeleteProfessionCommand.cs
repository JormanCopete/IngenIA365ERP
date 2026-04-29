using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Professions.Commands.DeleteProfession;

public record DeleteProfessionCommand(Guid PublicId) : IRequest<Result>;

public class DeleteProfessionCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteProfessionCommand, Result>
{
    public async Task<Result> Handle(
        DeleteProfessionCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.Professions
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
