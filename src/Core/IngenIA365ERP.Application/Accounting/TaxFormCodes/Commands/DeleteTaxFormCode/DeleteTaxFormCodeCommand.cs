using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.TaxFormCodes.Commands.DeleteTaxFormCode;

public record DeleteTaxFormCodeCommand(Guid PublicId) : IRequest<Result>;

public class DeleteTaxFormCodeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteTaxFormCodeCommand, Result>
{
    public async Task<Result> Handle(
        DeleteTaxFormCodeCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.TaxFormCodes
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
