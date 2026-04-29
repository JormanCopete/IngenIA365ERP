using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Core.People.Commands.CreatePerson;

public record CreatePersonCommand : IRequest<Result<Guid>>
{
    public string LastName { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string TaxId { get; init; } = string.Empty;
    public string IdType { get; init; } = "C";
    public string? TaxIdCheckDigit { get; init; }
    public string? PersonType { get; init; }
    public string? BusinessName { get; init; }
    public string? Email { get; init; }
    public string? Phone1 { get; init; }
    public string? Phone2 { get; init; }
    public string? Mobile { get; init; }
    public string? Address { get; init; }
    public int? CityId { get; init; }
    public string? Gender { get; init; }
    public string? MaritalStatus { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? EducationLevel { get; init; }
    public bool IsAssociate { get; init; }
    public bool IsEmployee { get; init; }
}
