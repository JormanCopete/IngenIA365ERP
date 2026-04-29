using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.CulturalActivities.Commands.UpdateCulturalActivity;

public record UpdateCulturalActivityCommand : IRequest<Result>
{
    public Guid PublicId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ShortName { get; init; }
    public Guid? CommitteePublicId { get; init; }
}

public class UpdateCulturalActivityCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<UpdateCulturalActivityCommand, Result>
{
    public async Task<Result> Handle(
        UpdateCulturalActivityCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.CulturalActivities
            .FirstOrDefaultAsync(e => e.PublicId == request.PublicId && !e.IsDeleted, cancellationToken);

        if (entity is null)
            return Result.Failure(Error.NotFound);

        int? committeeId = null;
        if (request.CommitteePublicId.HasValue)
        {
            var committee = await context.Committees
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.PublicId == request.CommitteePublicId.Value && !c.IsDeleted, cancellationToken);

            if (committee is null)
                return Result.Failure(new Error("CulturalActivity.CommitteeNotFound", "Committee not found."));

            committeeId = committee.Id;
        }

        entity.Name = request.Name;
        entity.ShortName = request.ShortName;
        entity.CommitteeId = committeeId;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
