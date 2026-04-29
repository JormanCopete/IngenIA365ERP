using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Debit.PosTerminals.Commands.DeletePosTerminal;

public record DeletePosTerminalCommand(Guid PublicId) : IRequest<Result>;

public class DeletePosTerminalCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeletePosTerminalCommand, Result>
{
    public async Task<Result> Handle(
        DeletePosTerminalCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.PosTerminals
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
