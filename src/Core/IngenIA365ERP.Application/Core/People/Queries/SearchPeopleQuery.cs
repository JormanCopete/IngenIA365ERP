using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.People.Queries;

public record PersonSearchDto(
    Guid PublicId,
    string IdentificationNumber,
    string FullName,
    bool IsAssociate,
    bool IsEmployee,
    string? CityName,
    string? Status);

public record SearchPeopleQuery(string SearchTerm) : IRequest<Result<List<PersonSearchDto>>>;

public class SearchPeopleQueryHandler(IApplicationDbContext context)
    : IRequestHandler<SearchPeopleQuery, Result<List<PersonSearchDto>>>
{
    public async Task<Result<List<PersonSearchDto>>> Handle(SearchPeopleQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SearchTerm))
            return Result.Success(new List<PersonSearchDto>());

        var term = request.SearchTerm.Trim();

        var results = await context.People
            .AsNoTracking()
            .Include(p => p.City)
            .Where(p => !p.IsDeleted &&
                (p.TaxId.Contains(term) ||
                 p.FirstName.Contains(term) ||
                 p.LastName.Contains(term) ||
                 (p.BusinessName != null && p.BusinessName.Contains(term)) ||
                 (p.LegacyCode != null && p.LegacyCode.Contains(term))))
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .Take(50)
            .Select(p => new PersonSearchDto(
                p.PublicId,
                p.TaxId,
                p.BusinessName != null && p.BusinessName.Length > 0
                    ? p.BusinessName
                    : p.FirstName + " " + p.LastName,
                p.IsAssociate,
                p.IsEmployee,
                p.City != null ? p.City.Name : null,
                p.Status))
            .ToListAsync(ct);

        return Result.Success(results);
    }
}
