using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.WithholdingTaxLines.Commands.DeleteWithholdingTaxLine;

public record DeleteWithholdingTaxLineCommand(Guid PublicId) : IRequest<Result>;

public class DeleteWithholdingTaxLineCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteWithholdingTaxLineCommand, Result>
{
    public async Task<Result> Handle(
        DeleteWithholdingTaxLineCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.WithholdingTaxLines
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
