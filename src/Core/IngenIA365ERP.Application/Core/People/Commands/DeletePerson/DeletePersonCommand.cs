using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.People.Commands.DeletePerson;

/// <summary>
/// Soft-delete de una persona. Valida que no este referenciada como
/// empleado activo, asociado activo o tenga cartera activa antes de eliminar.
/// </summary>
public record DeletePersonCommand(Guid PublicId) : IRequest<Result>;

public class DeletePersonCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<DeletePersonCommand, Result>
{
    public async Task<Result> Handle(DeletePersonCommand request, CancellationToken ct)
    {
        var person = await context.People.FirstOrDefaultAsync(
            p => p.PublicId == request.PublicId && !p.IsDeleted, ct);

        if (person is null)
            return Result.Failure(new Error("Person.NotFound", "Persona no encontrada."));

        // Validar que no este referenciada como empleado activo.
        var hasActiveEmployee = await context.Employees.AsNoTracking()
            .AnyAsync(e => e.PersonId == person.Id && !e.IsDeleted && e.Status == 1, ct);
        if (hasActiveEmployee)
            return Result.Failure(new Error("Person.HasActiveEmployee",
                "No se puede eliminar: la persona esta registrada como empleado activo. Termine el contrato primero."));

        // Validar que no tenga cartera activa.
        var hasActiveLoans = await context.LoanPortfolios.AsNoTracking()
            .AnyAsync(lp => lp.PersonId == person.Id && !lp.IsDeleted && lp.CurrentBalance > 0, ct);
        if (hasActiveLoans)
            return Result.Failure(new Error("Person.HasActiveLoans",
                "No se puede eliminar: la persona tiene cartera activa."));

        person.IsDeleted = true;
        person.DeletedAt = dateTime.UtcNow;
        person.DeletedBy = currentUser.UserName;
        person.UpdatedAt = dateTime.UtcNow;
        person.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
