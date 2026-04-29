using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.IcaTaxLines.Commands.DeleteIcaTaxLine;

public record DeleteIcaTaxLineCommand(Guid PublicId) : IRequest<Result>;

public class DeleteIcaTaxLineCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteIcaTaxLineCommand, Result>
{
    public async Task<Result> Handle(
        DeleteIcaTaxLineCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.IcaTaxLines
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
