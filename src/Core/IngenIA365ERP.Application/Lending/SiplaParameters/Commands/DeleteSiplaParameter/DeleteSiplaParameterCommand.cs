using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.SiplaParameters.Commands.DeleteSiplaParameter;

public record DeleteSiplaParameterCommand(Guid PublicId) : IRequest<Result>;

public class DeleteSiplaParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteSiplaParameterCommand, Result>
{
    public async Task<Result> Handle(
        DeleteSiplaParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.SiplaParameters
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
