using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.TransactionCodes.Commands.DeleteTransactionCode;

public record DeleteTransactionCodeCommand(Guid PublicId) : IRequest<Result>;

public class DeleteTransactionCodeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteTransactionCodeCommand, Result>
{
    public async Task<Result> Handle(
        DeleteTransactionCodeCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.TransactionCodes
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
