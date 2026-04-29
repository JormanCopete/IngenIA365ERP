using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.CommissionParameters.Commands.DeleteCommissionParameter;

public record DeleteCommissionParameterCommand(Guid PublicId) : IRequest<Result>;

public class DeleteCommissionParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteCommissionParameterCommand, Result>
{
    public async Task<Result> Handle(
        DeleteCommissionParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.CommissionParameters
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
