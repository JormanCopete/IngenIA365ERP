using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.AutoContributionParams.Commands.DeleteAutoContributionParam;

public record DeleteAutoContributionParamCommand(Guid PublicId) : IRequest<Result>;

public class DeleteAutoContributionParamCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteAutoContributionParamCommand, Result>
{
    public async Task<Result> Handle(
        DeleteAutoContributionParamCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.AutoContributionParams
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
