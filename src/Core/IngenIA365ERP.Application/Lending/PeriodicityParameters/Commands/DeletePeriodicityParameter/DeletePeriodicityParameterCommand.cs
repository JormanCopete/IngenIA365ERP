using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.PeriodicityParameters.Commands.DeletePeriodicityParameter;

public record DeletePeriodicityParameterCommand(Guid PublicId) : IRequest<Result>;

public class DeletePeriodicityParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeletePeriodicityParameterCommand, Result>
{
    public async Task<Result> Handle(
        DeletePeriodicityParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.PeriodicityParameters
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
