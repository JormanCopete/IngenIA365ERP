using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.WorkRiskProviders.Commands.DeleteWorkRiskProvider;

public record DeleteWorkRiskProviderCommand(Guid PublicId) : IRequest<Result>;

public class DeleteWorkRiskProviderCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteWorkRiskProviderCommand, Result>
{
    public async Task<Result> Handle(
        DeleteWorkRiskProviderCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.WorkRiskProviders
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
