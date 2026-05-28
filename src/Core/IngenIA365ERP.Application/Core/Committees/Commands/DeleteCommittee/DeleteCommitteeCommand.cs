using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Committees.Commands.DeleteCommittee;

/// <summary>
/// Soft-delete de un comite. Bloquea si tiene deportes o actividades culturales asociadas.
/// </summary>
public record DeleteCommitteeCommand(Guid PublicId) : IRequest<Result>;

public class DeleteCommitteeCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteCommitteeCommand, Result>
{
    public async Task<Result> Handle(DeleteCommitteeCommand request, CancellationToken ct)
    {
        var entity = await context.Committees
            .FirstOrDefaultAsync(c => c.PublicId == request.PublicId && !c.IsDeleted, ct);

        if (entity is null)
            return Result.Failure(new Error("Committee.NotFound", "Comite no encontrado."));

        var hasSports = await context.Sports.AsNoTracking()
            .AnyAsync(s => s.CommitteeId == entity.Id && !s.IsDeleted, ct);
        if (hasSports)
            return Result.Failure(new Error("Committee.HasSports",
                "No se puede eliminar: el comite tiene deportes asociados."));

        var hasCultural = await context.CulturalActivities.AsNoTracking()
            .AnyAsync(a => a.CommitteeId == entity.Id && !a.IsDeleted, ct);
        if (hasCultural)
            return Result.Failure(new Error("Committee.HasCulturalActivities",
                "No se puede eliminar: el comite tiene actividades culturales asociadas."));

        entity.IsDeleted = true;
        entity.DeletedAt = dateTime.UtcNow;
        entity.DeletedBy = currentUser.UserName;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
