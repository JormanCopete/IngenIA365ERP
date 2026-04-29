using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Payroll.ConceptAccounts.Commands.DeleteConceptAccount;

public record DeleteConceptAccountCommand(Guid PublicId) : IRequest<Result>;

public class DeleteConceptAccountCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteConceptAccountCommand, Result>
{
    public async Task<Result> Handle(
        DeleteConceptAccountCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.ConceptAccounts
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
