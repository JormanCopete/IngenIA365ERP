using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Core.People.Commands.CreatePerson;

/// <summary>
/// Crea una persona en la tabla maestra COR_People.
/// SOLO datos personales + flags de rol. La especializacion (asociado, empleado,
/// vendedor) se administra desde sus propios modulos.
/// </summary>
public record CreatePersonCommand : IRequest<Result<Guid>>
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

    // Roles
    public bool IsAssociate { get; init; }
    public bool IsEmployee { get; init; }
    public bool IsThirdParty { get; init; }
    public bool IsAdvisor { get; init; }
    public bool IsCustomer { get; init; }
    public bool IsSupplier { get; init; }
    public bool IsSalesperson { get; init; }
    public bool ReceivesInvoice { get; init; }

    public string? Status { get; init; }
}
