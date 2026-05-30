using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Countries.Commands.DeleteCountry;

/// <summary>
/// Soft-delete de un pais. Bloquea si tiene departamentos asociados.
/// </summary>
public record DeleteCountryCommand(Guid PublicId) : IRequest<Result>;

public class DeleteCountryCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteCountryCommand, Result>
{
    public async Task<Result> Handle(DeleteCountryCommand request, CancellationToken ct)
    {
        var entity = await context.Countries
            .FirstOrDefaultAsync(c => c.PublicId == request.PublicId && !c.IsDeleted, ct);

        if (entity is null)
            return Result.Failure(new Error("Country.NotFound", "Pais no encontrado."));

        // No permitir borrar si tiene departamentos activos
        var hasDepartments = await context.Departments.AsNoTracking()
            .AnyAsync(d => d.CountryId == entity.Id && !d.IsDeleted, ct);
        if (hasDepartments)
            return Result.Failure(new Error("Country.HasDepartments",
                "No se puede eliminar: el pais tiene departamentos asociados."));

        entity.IsDeleted = true;
        entity.DeletedAt = dateTime.UtcNow;
        entity.DeletedBy = currentUser.UserName;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
