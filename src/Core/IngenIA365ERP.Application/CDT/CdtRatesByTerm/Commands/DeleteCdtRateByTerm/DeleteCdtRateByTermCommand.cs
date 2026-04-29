using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.CDT.CdtRatesByTerm.Commands.DeleteCdtRateByTerm;

public record DeleteCdtRateByTermCommand(Guid PublicId) : IRequest<Result>;

public class DeleteCdtRateByTermCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteCdtRateByTermCommand, Result>
{
    public async Task<Result> Handle(
        DeleteCdtRateByTermCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.CdtRatesByTerm
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
