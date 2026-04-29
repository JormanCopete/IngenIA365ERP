using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Queries.GetPersonByPublicId;
using MediatR;

namespace IngenIA365ERP.Application.Core.People.Queries.GetPeople;

public record GetPeopleQuery : IRequest<Result<PagedList<PersonDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
    public bool? IsAssociate { get; init; }
    public bool? IsEmployee { get; init; }
}
