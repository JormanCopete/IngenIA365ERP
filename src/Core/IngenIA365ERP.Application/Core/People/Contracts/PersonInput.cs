using FluentValidation;

namespace IngenIA365ERP.Application.Core.People.Contracts;

/// <summary>
/// Lo que un cliente puede escribir de una persona (feature 008). Es la base de
/// <c>CreatePersonCommand</c>, <c>UpdatePersonCommand</c> y de los compuestos
/// <c>RegisterEmployeeWithPersonCommand</c> / <c>RegisterAssociateWithPersonCommand</c>.
///
/// <para>
/// <b>No trae</b> <c>IsEmployee</c>, <c>IsAssociate</c> ni <c>IsSalesperson</c> a propósito:
/// esas tres banderas son el reflejo de una fila en <c>PAY_Employees</c>, <c>COR_Associates</c>
/// e <c>INV_Salespeople</c>, y las escribe únicamente el handler que crea o retira esa fila
/// (constitución, Principio V). Hasta el 2026-09-13 estaban en el contrato y
/// <c>UpdatePersonCommand</c> las sobrescribía las ocho de golpe: registrar como empleado a una
/// persona asociada le apagaba «Asociado». Sacarlas del tipo hace el bug imposible; si un cliente
/// viejo las manda, System.Text.Json las descarta sin error.
/// </para>
/// </summary>
public record PersonInput
{
    // Identificacion
    public string IdType { get; init; } = "C";
    public string TaxId { get; init; } = string.Empty;
    public string? TaxIdCheckDigit { get; init; }
    public string? IdIssuedAt { get; init; }
    public DateOnly? IdIssueDate { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string? BusinessName { get; init; }
    public string? PersonType { get; init; }

    // Contacto
    public string? Address { get; init; }
    public string? Phone1 { get; init; }
    public string? Phone2 { get; init; }
    public string? Mobile { get; init; }
    public string? Email { get; init; }
    public Guid? CityPublicId { get; init; }

    // Demografia
    public string? Gender { get; init; }
    public string? MaritalStatus { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? EducationLevel { get; init; }

    // Roles que son solo una marca (editables en Personas)
    public bool IsThirdParty { get; init; }
    public bool IsAdvisor { get; init; }
    public bool IsCustomer { get; init; }
    public bool IsSupplier { get; init; }
    public bool ReceivesInvoice { get; init; }

    public string? Status { get; init; }
}

/// <summary>
/// Reglas de la persona en un solo sitio. Los comandos que heredan de
/// <see cref="PersonInput"/> las toman con <c>Include(new PersonInputValidator())</c>:
/// el contenedor no aplica contravarianza, así que un <c>IValidator&lt;PersonInput&gt;</c>
/// registrado no llega solo al pipeline de <c>CreatePersonCommand</c>.
/// </summary>
public class PersonInputValidator : AbstractValidator<PersonInput>
{
    public PersonInputValidator()
    {
        RuleFor(x => x.LastName)
            .NotEmpty().WithMessage("Apellidos obligatorios.")
            .MaximumLength(150);

        RuleFor(x => x.FirstName)
            .NotEmpty().WithMessage("Nombres obligatorios.")
            .MaximumLength(150);

        RuleFor(x => x.TaxId)
            .NotEmpty().WithMessage("NIT/Cedula obligatorio.")
            .MaximumLength(20);

        RuleFor(x => x.IdType)
            .NotEmpty().WithMessage("Tipo de identificacion obligatorio.")
            .MaximumLength(2);

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Correo electronico no valido.")
            .MaximumLength(120);

        RuleFor(x => x.Phone1).MaximumLength(40);
        RuleFor(x => x.Phone2).MaximumLength(40);
        RuleFor(x => x.Mobile).MaximumLength(30);
        RuleFor(x => x.Address).MaximumLength(120);
        RuleFor(x => x.BusinessName).MaximumLength(150);
        RuleFor(x => x.Gender).MaximumLength(2);
        RuleFor(x => x.MaritalStatus).MaximumLength(2);
        RuleFor(x => x.EducationLevel).MaximumLength(2);

        RuleFor(x => x.DateOfBirth)
            .LessThan(DateOnly.FromDateTime(DateTime.UtcNow))
            .When(x => x.DateOfBirth.HasValue)
            .WithMessage("La fecha de nacimiento debe ser anterior a hoy.");
    }
}
