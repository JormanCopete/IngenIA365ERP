using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.SavingsParameters.Commands.DeleteSavingsParameter;

public record DeleteSavingsParameterCommand(Guid PublicId) : IRequest<Result>;

public class DeleteSavingsParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteSavingsParameterCommand, Result>
{
    public async Task<Result> Handle(
        DeleteSavingsParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.SavingsParameters
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
