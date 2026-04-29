using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Queries.GetPersonByPublicId;
using MapsterMapper;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.People.Queries.GetPeople;

public class GetPeopleQueryHandler(
    IApplicationDbContext context,
    IMapper mapper)
    : IRequestHandler<GetPeopleQuery, Result<PagedList<PersonDto>>>
{
    public async Task<Result<PagedList<PersonDto>>> Handle(
        GetPeopleQuery request,
        CancellationToken cancellationToken)
    {
        var query = context.People
            .AsNoTracking()
            .Include(p => p.City)
            .Where(p => !p.IsDeleted);

        // Filter by search term
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim().ToLower();
            query = query.Where(p =>
                p.FirstName.ToLower().Contains(term) ||
                p.LastName.ToLower().Contains(term) ||
                p.TaxId.Contains(term) ||
                (p.BusinessName != null && p.BusinessName.ToLower().Contains(term)));
        }

        // Filter by role flags
        if (request.IsAssociate.HasValue)
            query = query.Where(p => p.IsAssociate == request.IsAssociate.Value);

        if (request.IsEmployee.HasValue)
            query = query.Where(p => p.IsEmployee == request.IsEmployee.Value);

        // Sorting
        query = request.Pagination.SortBy?.ToLower() switch
        {
            "lastname" => request.Pagination.IsDescending
                ? query.OrderByDescending(p => p.LastName)
                : query.OrderBy(p => p.LastName),
            "taxid" => request.Pagination.IsDescending
                ? query.OrderByDescending(p => p.TaxId)
                : query.OrderBy(p => p.TaxId),
            _ => query.OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .ToListAsync(cancellationToken);

        var dtos = mapper.Map<List<PersonDto>>(items);

        var pagedList = new PagedList<PersonDto>(
            dtos,
            totalCount,
            request.Pagination.PageNumber,
            request.Pagination.PageSize);

        return Result.Success(pagedList);
    }
}
