using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.VoucherTypes.Commands.DeleteVoucherType;

public record DeleteVoucherTypeCommand(Guid PublicId) : IRequest<Result>;

public class DeleteVoucherTypeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteVoucherTypeCommand, Result>
{
    public async Task<Result> Handle(
        DeleteVoucherTypeCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.VoucherTypes
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
