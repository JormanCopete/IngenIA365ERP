using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.GmfTaxLines.Commands.DeleteGmfTaxLine;

public record DeleteGmfTaxLineCommand(Guid PublicId) : IRequest<Result>;

public class DeleteGmfTaxLineCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteGmfTaxLineCommand, Result>
{
    public async Task<Result> Handle(
        DeleteGmfTaxLineCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.GmfTaxLines
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
