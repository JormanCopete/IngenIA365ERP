using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.WorkRiskRates.Commands.DeleteWorkRiskRate;

public record DeleteWorkRiskRateCommand(Guid PublicId) : IRequest<Result>;

public class DeleteWorkRiskRateCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteWorkRiskRateCommand, Result>
{
    public async Task<Result> Handle(
        DeleteWorkRiskRateCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.WorkRiskRates
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
