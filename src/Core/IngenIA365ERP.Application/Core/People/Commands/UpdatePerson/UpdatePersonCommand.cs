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

        if (person.TaxId != request.TaxId)
        {
            // Mismo criterio que al crear: eliminadas incluidas, porque el índice único no las distingue.
            var colision = await personas.ColisionDeDocumentoAsync(request.TaxId, excluirId: person.Id, ct);
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
            if (cityId is null)
                return Result.Failure(new Error("Person.CityNotFound", "Ciudad no encontrada."));
        }

        person.IdType = request.IdType;
        person.TaxId = request.TaxId;
        person.TaxIdCheckDigit = request.TaxIdCheckDigit;
        person.IdIssuedAt = request.IdIssuedAt;
        person.IdIssueDate = request.IdIssueDate;
        person.FirstName = request.FirstName;
        person.LastName = request.LastName;
        person.SecondLastName = string.IsNullOrWhiteSpace(request.SecondLastName) ? null : request.SecondLastName.Trim();
        person.OtherNames = string.IsNullOrWhiteSpace(request.OtherNames) ? null : request.OtherNames.Trim();
        person.BusinessName = request.BusinessName;
        person.PersonType = request.PersonType;
        person.Address = request.Address;
        person.Phone1 = request.Phone1;
        person.Phone2 = request.Phone2;
        person.Mobile = request.Mobile;
        person.Email = request.Email;
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
            var colision = await personas.TraducirColisionAsync(ex, request.TaxId, ct);
            return Result.Failure(colision!);
        }
        return Result.Success();
    }
}

public class UpdatePersonCommandValidator : AbstractValidator<UpdatePersonCommand>
{
    public UpdatePersonCommandValidator()
    {
        Include(new PersonInputValidator());
        RuleFor(x => x.PublicId).NotEmpty();
    }
}
