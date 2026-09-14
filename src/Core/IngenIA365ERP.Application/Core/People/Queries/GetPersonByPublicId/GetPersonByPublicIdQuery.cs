using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Core.People.Queries.GetPersonByPublicId;

public record GetPersonByPublicIdQuery(Guid PublicId) : IRequest<Result<PersonDto>>;

/// <summary>
/// La persona como la lee <c>GET /api/core/people/{id}</c>. Desde la feature 008 trae
/// <b>todo</b> lo que el formulario edita: hasta el 2026-09-13 faltaban el dígito de
/// verificación, lugar y fecha de expedición, segundo teléfono, ciudad, estado civil, nivel
/// educativo y cinco banderas, así que la pantalla los cargaba vacíos y el <c>PUT</c>
/// siguiente los borraba. Mapster completa por nombre; <c>CityPublicId</c> y
/// <c>CityName</c> salen aplanados de <c>City</c>.
/// </summary>
public record PersonDto
{
    public Guid PublicId { get; init; }

    // Identificacion
    public string LastName { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string TaxId { get; init; } = string.Empty;
    public string? TaxIdCheckDigit { get; init; }
    public string? IdIssuedAt { get; init; }
    public DateOnly? IdIssueDate { get; init; }
    public string IdType { get; init; } = string.Empty;
    public string? PersonType { get; init; }
    public string? BusinessName { get; init; }

    // Contacto
    public string? Email { get; init; }
    public string? Phone1 { get; init; }
    public string? Phone2 { get; init; }
    public string? Mobile { get; init; }
    public string? Address { get; init; }
    public Guid? CityPublicId { get; init; }
    public string? CityName { get; init; }

    // Demografia
    public string? Gender { get; init; }
    public string? MaritalStatus { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? EducationLevel { get; init; }

    public string? Status { get; init; }

    // Banderas derivadas (solo lectura: las escribe el modulo que crea la fila hija)
    public bool IsAssociate { get; init; }
    public bool IsEmployee { get; init; }
    public bool IsSalesperson { get; init; }

    // Banderas simples (editables en Personas)
    public bool IsThirdParty { get; init; }
    public bool IsAdvisor { get; init; }
    public bool IsCustomer { get; init; }
    public bool IsSupplier { get; init; }
    public bool ReceivesInvoice { get; init; }
}
