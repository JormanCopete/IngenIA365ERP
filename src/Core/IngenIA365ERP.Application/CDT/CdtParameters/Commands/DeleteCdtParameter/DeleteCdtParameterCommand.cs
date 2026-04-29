using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.CDT.CdtParameters.Commands.DeleteCdtParameter;

public record DeleteCdtParameterCommand(Guid PublicId) : IRequest<Result>;

public class DeleteCdtParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteCdtParameterCommand, Result>
{
    public async Task<Result> Handle(
        DeleteCdtParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.CdtParameters
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
