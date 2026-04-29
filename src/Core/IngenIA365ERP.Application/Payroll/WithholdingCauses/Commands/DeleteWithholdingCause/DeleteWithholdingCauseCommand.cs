using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.WithholdingCauses.Commands.DeleteWithholdingCause;

public record DeleteWithholdingCauseCommand(Guid PublicId) : IRequest<Result>;

public class DeleteWithholdingCauseCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteWithholdingCauseCommand, Result>
{
    public async Task<Result> Handle(
        DeleteWithholdingCauseCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.WithholdingCauses
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
