using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Inventory.ProductAccounts.Commands.DeleteProductAccount;

public record DeleteProductAccountCommand(Guid PublicId) : IRequest<Result>;

public class DeleteProductAccountCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteProductAccountCommand, Result>
{
    public async Task<Result> Handle(
        DeleteProductAccountCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.ProductAccounts
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
