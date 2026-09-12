using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.FamilyCompensationFunds.Commands.DeleteFamilyCompensationFund;

public record DeleteFamilyCompensationFundCommand(Guid PublicId) : IRequest<Result>;

public class DeleteFamilyCompensationFundCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteFamilyCompensationFundCommand, Result>
{
    public async Task<Result> Handle(
        DeleteFamilyCompensationFundCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.FamilyCompensationFunds
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
