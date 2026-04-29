using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Debit.DebitAgreementParameters.Commands.DeleteDebitAgreementParameter;

public record DeleteDebitAgreementParameterCommand(Guid PublicId) : IRequest<Result>;

public class DeleteDebitAgreementParameterCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteDebitAgreementParameterCommand, Result>
{
    public async Task<Result> Handle(
        DeleteDebitAgreementParameterCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.DebitAgreementParameters
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
