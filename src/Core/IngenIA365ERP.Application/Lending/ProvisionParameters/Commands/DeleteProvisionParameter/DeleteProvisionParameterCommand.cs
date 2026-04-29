using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.ProvisionParameters.Commands.DeleteProvisionParameter;

public record DeleteProvisionParameterCommand(Guid PublicId) : IRequest<Result>;

public class DeleteProvisionParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteProvisionParameterCommand, Result>
{
    public async Task<Result> Handle(
        DeleteProvisionParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.ProvisionParameters
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
