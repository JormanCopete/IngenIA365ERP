using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.VatTaxLines.Commands.DeleteVatTaxLine;

public record DeleteVatTaxLineCommand(Guid PublicId) : IRequest<Result>;

public class DeleteVatTaxLineCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteVatTaxLineCommand, Result>
{
    public async Task<Result> Handle(
        DeleteVatTaxLineCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.VatTaxLines
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
