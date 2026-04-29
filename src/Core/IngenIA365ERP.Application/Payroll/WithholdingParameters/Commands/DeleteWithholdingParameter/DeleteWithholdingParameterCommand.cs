using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.WithholdingParameters.Commands.DeleteWithholdingParameter;

public record DeleteWithholdingParameterCommand(Guid PublicId) : IRequest<Result>;

public class DeleteWithholdingParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteWithholdingParameterCommand, Result>
{
    public async Task<Result> Handle(
        DeleteWithholdingParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.WithholdingParameters
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
