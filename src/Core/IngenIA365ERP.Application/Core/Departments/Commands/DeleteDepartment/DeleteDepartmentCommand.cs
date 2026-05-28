using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.Departments.Commands.DeleteDepartment;

/// <summary>
/// Soft-delete de un departamento. Bloquea si tiene ciudades asociadas.
/// </summary>
public record DeleteDepartmentCommand(Guid PublicId) : IRequest<Result>;

public class DeleteDepartmentCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeleteDepartmentCommand, Result>
{
    public async Task<Result> Handle(DeleteDepartmentCommand request, CancellationToken ct)
    {
        var entity = await context.Departments
            .FirstOrDefaultAsync(d => d.PublicId == request.PublicId && !d.IsDeleted, ct);

        if (entity is null)
            return Result.Failure(new Error("Department.NotFound", "Departamento no encontrado."));

        // No permitir borrar si tiene ciudades activas
        var hasCities = await context.Cities.AsNoTracking()
            .AnyAsync(c => c.DepartmentId == entity.Id && !c.IsDeleted, ct);
        if (hasCities)
            return Result.Failure(new Error("Department.HasCities",
                "No se puede eliminar: el departamento tiene ciudades asociadas."));

        entity.IsDeleted = true;
        entity.DeletedAt = dateTime.UtcNow;
        entity.DeletedBy = currentUser.UserName;
        entity.UpdatedAt = dateTime.UtcNow;
        entity.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
