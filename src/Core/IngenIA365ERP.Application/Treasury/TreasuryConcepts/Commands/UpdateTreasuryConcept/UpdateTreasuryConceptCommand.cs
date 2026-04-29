using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.TreasuryConcepts.Commands.UpdateTreasuryConcept;

public record UpdateTreasuryConceptCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string ConceptCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public string? ConceptType { get; init; }
}

public class UpdateTreasuryConceptCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateTreasuryConceptCommand, Result>
{
    public async Task<Result> Handle(
        UpdateTreasuryConceptCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.TreasuryConcepts
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        entity.ConceptCode = request.ConceptCode;
        entity.Name = request.Name;
        entity.ShortName = request.ShortName;
        entity.ConceptType = request.ConceptType;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
