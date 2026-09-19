using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.People.Commands.RestorePerson;

/// <summary>
/// Reactiva una persona eliminada (feature 008, FR-016): la <b>misma</b> fila, no una nueva.
/// El documento es único incluso frente a eliminadas (<c>UK_COR_People_TaxId</c> no las
/// distingue), así que crear otra es imposible y restaurar conserva todo lo que la referencia:
/// cartera, asientos, corridas. Exige el permiso de eliminar personas (la misma potestad).
/// Al restaurar se recalculan las tres banderas derivadas desde las tablas hijas: una eliminada
/// no puede tener empleado activo, pero sí afiliación o ficha de vendedor históricas.
/// </summary>
public sealed record RestorePersonCommand(Guid PublicId) : IRequest<Result>;

public sealed class RestorePersonCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser)
    : IRequestHandler<RestorePersonCommand, Result>
{
    public async Task<Result> Handle(RestorePersonCommand request, CancellationToken ct)
    {
        var person = await context.People
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.PublicId == request.PublicId, ct);
        if (person is null)
            return Result.Failure(new Error("Person.NotFound", "Persona no encontrada."));
        if (!person.IsDeleted)
            return Result.Failure(new Error("Person.NotDeleted", "La persona no está eliminada."));

        person.IsDeleted = false;
        person.DeletedAt = null;
        person.DeletedBy = null;

        // Banderas derivadas = reflejo de la fila hija viva (Principio V).
        person.IsEmployee = await context.Employees.AsNoTracking()
            .AnyAsync(e => e.PersonId == person.Id && !e.IsDeleted && e.Status != -1, ct);
        person.IsAssociate = await context.Associates.AsNoTracking()
            .AnyAsync(a => a.PersonId == person.Id && !a.IsDeleted, ct);
        person.IsSalesperson = await context.Salespeople.AsNoTracking()
            .AnyAsync(s => s.PersonId == person.Id && !s.IsDeleted, ct);

        person.UpdatedAt = dateTime.UtcNow;
        person.UpdatedBy = currentUser.UserName;

        await context.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public sealed class RestorePersonCommandValidator : AbstractValidator<RestorePersonCommand>
{
    public RestorePersonCommandValidator() => RuleFor(x => x.PublicId).NotEmpty();
}
