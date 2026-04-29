using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.TreasuryConcepts.Commands.DeleteTreasuryConcept;

public record DeleteTreasuryConceptCommand(Guid PublicId) : IRequest<Result>;

public class DeleteTreasuryConceptCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteTreasuryConceptCommand, Result>
{
    public async Task<Result> Handle(
        DeleteTreasuryConceptCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.TreasuryConcepts
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
