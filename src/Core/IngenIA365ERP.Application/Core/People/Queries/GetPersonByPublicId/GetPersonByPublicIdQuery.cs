using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Core.People.Queries.GetPersonByPublicId;

public record GetPersonByPublicIdQuery(Guid PublicId) : IRequest<Result<PersonDto>>;

public record PersonDto
{
    public Guid PublicId { get; init; }
    public string LastName { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string TaxId { get; init; } = string.Empty;
    public string IdType { get; init; } = string.Empty;
    public string? PersonType { get; init; }
    public string? BusinessName { get; init; }
    public string? Email { get; init; }
    public string? Phone1 { get; init; }
    public string? Mobile { get; init; }
    public string? Address { get; init; }
    public string? Gender { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? Status { get; init; }
    public bool IsAssociate { get; init; }
    public bool IsEmployee { get; init; }
    public string? CityName { get; init; }
}
