using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.People.Commands.DeletePerson;

/// <summary>
/// Soft-delete de una persona. Valida que no este referenciada como
/// empleado activo ni tenga obligaciones vivas en Cartera antes de eliminar. Feature 013 (T0169, T94, E-04; interfaces.md §7):
/// Cartera se consulta por <see cref="IConsultaDeObligaciones"/> con codeudas; bloquea si hay alguna obligación viva como deudor o
/// codeudor, o si la persona está en una carga de Cartera aplicada y sin activar.
/// </summary>
public record DeletePersonCommand(Guid PublicId) : IRequest<Result>;

public class DeletePersonCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser,
    IConsultaDeObligaciones obligaciones)
    : IRequestHandler<DeletePersonCommand, Result>
{
    public const string HasActiveLoansCode = "Person.HasActiveLoans";

    public const string HasActiveLoansMessage =
        "No se puede eliminar: la persona tiene obligaciones vivas como deudor o codeudor, o está en una carga de Cartera por activar.";

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

        // Validar que no tenga obligaciones vivas en Cartera (deudor o codeudor) ni este en una carga por activar.
        var deCartera = await obligaciones.ObligacionesAsync(
            new ConsultaDeObligaciones([person.PublicId], dateTime.HoyLocal, IncludeAsCoDebtor: true), ct);
        if (deCartera.People.Any(p => p.MigratedNotActivated || p.Obligations.Any(o => o.IsLive)))
            return Result.Failure(new Error(HasActiveLoansCode, HasActiveLoansMessage));

        person.IsDeleted = true;
        person.DeletedAt = dateTime.UtcNow;
        person.DeletedBy = currentUser.UserName;
        person.UpdatedAt = dateTime.UtcNow;
        person.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}
