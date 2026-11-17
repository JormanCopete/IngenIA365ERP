using IngenIA365ERP.Domain.Common.Identidad;
using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Contracts;
using IngenIA365ERP.Application.Core.People.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.People.Commands.UpdatePerson;

/// <summary>
/// Actualiza datos de la persona en la tabla maestra COR_People: identificación, contacto,
/// demografía, estado y las banderas de rol que son sólo una marca.
///
/// <para>
/// <b>No toca</b> <c>IsEmployee</c>, <c>IsAssociate</c> ni <c>IsSalesperson</c>: no están en
/// <see cref="PersonInput"/>. Hasta el 2026-09-13 el handler sobrescribía las ocho banderas con
/// lo que trajera el cliente, y Empleados/Asociados mandaban sólo la suya: registrar como
/// empleado a una persona asociada le apagaba «Asociado» (feature 008, US3).
/// </para>
/// </summary>
public record UpdatePersonCommand : PersonInput, IRequest<Result>
{
    public Guid PublicId { get; init; }
}

public class UpdatePersonCommandHandler(
    IApplicationDbContext context,
    IDateTimeService dateTime,
    ICurrentUserService currentUser,
    PersonFactory personas)
    : IRequestHandler<UpdatePersonCommand, Result>
{
    public async Task<Result> Handle(UpdatePersonCommand request, CancellationToken ct)
    {
        var person = await context.People.FirstOrDefaultAsync(
            p => p.PublicId == request.PublicId && !p.IsDeleted, ct);
        if (person is null)
            return Result.Failure(new Error("Person.NotFound", "Persona no encontrada."));

        // Feature 017: el número se guarda normalizado sólo si cambió (una fila vieja con puntos que
        // se edita sin tocar el documento no se reescribe) y el usuario se recalcula con tipo y número.
        var cambiaDocumento = person.TaxId != request.TaxId || person.IdType != request.IdType;
        var cambiaDigito = !string.Equals(Vacio(person.TaxIdCheckDigit), Vacio(request.TaxIdCheckDigit), StringComparison.Ordinal);
        var taxId = person.TaxId == request.TaxId ? request.TaxId : UsuarioDeDocumento.NormalizarNumero(request.TaxId);

        // 2026-11-16: las reglas del documento (catálogo de tipos, número normalizado, NIT sin DV y DV de la DIAN) se aplican sólo si
        // cambió el tipo o el número —o el DV, y entonces sólo la del DV—: una fila vieja que se edita sin tocar su documento no se bloquea.
        if (cambiaDocumento || cambiaDigito)
        {
            var falla = DocumentoDePersona.Fallas(request.IdType, request.TaxId, request.TaxIdCheckDigit)
                .FirstOrDefault(f => cambiaDocumento || f.Propiedad == nameof(PersonInput.TaxIdCheckDigit));
            if (falla is not null)
                return Result.Failure(new Error(DocumentoDePersona.CodigoInvalido, falla.Mensaje));
        }

        if (cambiaDocumento)
        {
            // Mismo criterio que al crear: eliminadas incluidas, porque el índice único no las distingue.
            var colision = await personas.ColisionDeDocumentoAsync(request.IdType, taxId, excluirId: person.Id, ct);
            if (colision is not null)
                return Result.Failure(colision);
        }

        int? cityId = null;
        if (request.CityPublicId.HasValue)
        {
            cityId = await context.Cities.AsNoTracking()
                .Where(c => c.PublicId == request.CityPublicId.Value && !c.IsDeleted)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync(ct);
            // 2026-11-16: la ciudad que la persona ya tenía se acepta sin cambio aunque se haya dado de baja; antes impedía guardar
            // cualquier otro dato de la persona (Person.CityNotFound). Una ciudad retirada nueva sigue rechazándose.
            if (cityId is null && person.CityId is { } actual)
            {
                var esLaActual = await context.Cities.IgnoreQueryFilters().AsNoTracking()
                    .AnyAsync(c => c.Id == actual && c.PublicId == request.CityPublicId.Value, ct);
                if (esLaActual) cityId = actual;
            }
            if (cityId is null)
                return Result.Failure(new Error("Person.CityNotFound", "Ciudad no encontrada."));
        }

        person.IdType = request.IdType;
        person.TaxId = taxId;
        if (cambiaDocumento)
            person.Username = UsuarioDeDocumento.DerivarParaPersona(request.IdType, taxId);
        person.TaxIdCheckDigit = request.TaxIdCheckDigit;
        person.IdIssuedAt = request.IdIssuedAt;
        person.IdIssueDate = request.IdIssueDate;
        var nombres = PersonFactory.NombresAGuardar(request);
        (person.FirstName, person.LastName) = (nombres.Nombre, nombres.Apellido);
        person.SecondLastName = nombres.SegundoApellido;
        person.OtherNames = nombres.OtrosNombres;
        person.BusinessName = Vacio(request.BusinessName);
        person.PersonType = request.PersonType;
        person.Address = Vacio(request.Address);
        person.Phone1 = request.Phone1;
        person.Phone2 = request.Phone2;
        person.Mobile = request.Mobile;
        person.Email = Vacio(request.Email);
        person.CityId = cityId;
        person.Gender = request.Gender;
        person.MaritalStatus = request.MaritalStatus;
        person.DateOfBirth = request.DateOfBirth;
        person.EducationLevel = request.EducationLevel;
        // Banderas simples: las edita Personas. Las derivadas no se tocan aquí.
        person.IsThirdParty = request.IsThirdParty;
        person.IsAdvisor = request.IsAdvisor;
        person.IsCustomer = request.IsCustomer;
        person.IsSupplier = request.IsSupplier;
        person.ReceivesInvoice = request.ReceivesInvoice;
        // Perfil tributario (feature 012, T173): sólo lo que viene; un PUT sin él no lo borra.
        PersonFactory.AplicarPerfilTributario(request, person);
        if (!string.IsNullOrWhiteSpace(request.Status))
            person.Status = request.Status;
        person.UpdatedAt = dateTime.UtcNow;
        person.UpdatedBy = currentUser.UserName;

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (PersonFactory.EsColisionDeDocumento(ex))
        {
            var colision = await personas.TraducirColisionAsync(ex, request.IdType, taxId, ct);
            return Result.Failure(colision!);
        }
        return Result.Success();
    }

    private static string? Vacio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

public class UpdatePersonCommandValidator : AbstractValidator<UpdatePersonCommand>
{
    public UpdatePersonCommandValidator()
    {
        // Sin las reglas del documento: el handler las aplica sólo si el tipo, el número o el DV cambiaron (2026-11-16).
        Include(PersonInputValidator.SinReglasDelDocumento());
        RuleFor(x => x.PublicId).NotEmpty();
    }
}
