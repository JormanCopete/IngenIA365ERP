using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.People.Queries;

/// <summary>
/// Listado paginado de personas para la pagina /maestros/personas.
/// Soporta busqueda libre (TaxId / nombre / apellido / razon social) y
/// filtros por flag de rol (asociado / empleado / tercero / asesor).
/// </summary>
public record PersonListItemDto(
    Guid PublicId,
    string TaxId,
    string IdType,
    string FirstName,
    string LastName,
    string? BusinessName,
    string FullName,
    string? Email,
    string? Phone1,
    string? Mobile,
    string? CityName,
    bool IsAssociate,
    bool IsEmployee,
    bool IsThirdParty,
    bool IsAdvisor,
    string? Status);

public record ListPeopleQuery : IRequest<Result<PagedList<PersonListItemDto>>>
{
    public PaginationParams Pagination { get; init; } = new();
    public string? SearchTerm { get; init; }
    public bool? IsAssociate { get; init; }
    public bool? IsEmployee { get; init; }
    public bool? IsThirdParty { get; init; }
    public bool? IsAdvisor { get; init; }
}

public class ListPeopleQueryHandler(IApplicationDbContext context)
    : IRequestHandler<ListPeopleQuery, Result<PagedList<PersonListItemDto>>>
{
    public async Task<Result<PagedList<PersonListItemDto>>> Handle(
        ListPeopleQuery request, CancellationToken ct)
    {
        var query = context.People
            .AsNoTracking()
            .Include(p => p.City)
            .Where(p => !p.IsDeleted);

        if (request.IsAssociate == true) query = query.Where(p => p.IsAssociate);
        if (request.IsEmployee == true) query = query.Where(p => p.IsEmployee);
        if (request.IsThirdParty == true) query = query.Where(p => p.IsThirdParty);
        if (request.IsAdvisor == true) query = query.Where(p => p.IsAdvisor);

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.Trim();
            query = query.Where(p =>
                p.TaxId.Contains(term)
                || p.FirstName.Contains(term)
                || p.LastName.Contains(term)
                || (p.BusinessName != null && p.BusinessName.Contains(term))
                || (p.LegacyCode != null && p.LegacyCode.Contains(term)));
        }

        // Sort
        query = request.Pagination.SortBy?.ToLower() switch
        {
            "taxid" => request.Pagination.IsDescending
                ? query.OrderByDescending(p => p.TaxId)
                : query.OrderBy(p => p.TaxId),
            "firstname" => request.Pagination.IsDescending
                ? query.OrderByDescending(p => p.FirstName)
                : query.OrderBy(p => p.FirstName),
            _ => query.OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
        };

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((request.Pagination.PageNumber - 1) * request.Pagination.PageSize)
            .Take(request.Pagination.PageSize)
            .Select(p => new PersonListItemDto(
                p.PublicId,
                p.TaxId,
                p.IdType,
                p.FirstName,
                p.LastName,
                p.BusinessName,
                p.BusinessName != null && p.BusinessName.Length > 0
                    ? p.BusinessName
                    : p.FirstName + " " + p.LastName,
                p.Email,
                p.Phone1,
                p.Mobile,
                p.City != null ? p.City.Name : null,
                p.IsAssociate,
                p.IsEmployee,
                p.IsThirdParty,
                p.IsAdvisor,
                p.Status))
            .ToListAsync(ct);

        return Result.Success(new PagedList<PersonListItemDto>(
            items, totalCount, request.Pagination.PageNumber, request.Pagination.PageSize));
    }
}

// --- Get By Id (lo que carga el formulario de edicion) ---
//
// Es lo que sirve GET /api/core/people/{id} y lo que abre PersonaDialog, que despues manda todo en
// el PUT. Por eso tiene que traer TODO lo que PersonInput escribe: un campo que falte aqui llega
// vacio al formulario y el guardado siguiente lo borra. Hasta el 2026-09-25 faltaban
// SecondLastName y OtherNames (feature 010): se guardaban, no se veian y la edicion siguiente
// los vaciaba. Lo vigila LaEdicionDePersonaDevuelveTodoLoQueSeEscribe.

public record PersonEditDto(
    Guid PublicId,
    string IdType,
    string TaxId,
    string? TaxIdCheckDigit,
    string? IdIssuedAt,
    DateOnly? IdIssueDate,
    string FirstName,
    string LastName,
    string? SecondLastName,
    string? OtherNames,
    string? BusinessName,
    string? PersonType,
    string? Address,
    string? Phone1,
    string? Phone2,
    string? Mobile,
    string? Email,
    Guid? CityPublicId,
    string? CityName,
    string? Gender,
    string? MaritalStatus,
    DateOnly? DateOfBirth,
    string? EducationLevel,
    bool IsAssociate,
    bool IsEmployee,
    bool IsThirdParty,
    bool IsAdvisor,
    bool IsCustomer,
    bool IsSupplier,
    bool IsSalesperson,
    bool ReceivesInvoice,
    string? Status);

public record GetPersonByIdQuery(Guid PublicId) : IRequest<Result<PersonEditDto>>;

public class GetPersonByIdQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetPersonByIdQuery, Result<PersonEditDto>>
{
    public async Task<Result<PersonEditDto>> Handle(GetPersonByIdQuery request, CancellationToken ct)
    {
        var person = await context.People
            .AsNoTracking()
            .Include(p => p.City)
            .FirstOrDefaultAsync(p => p.PublicId == request.PublicId && !p.IsDeleted, ct);

        if (person is null)
            return Result.Failure<PersonEditDto>(new Error("Person.NotFound", "Persona no encontrada."));

        return Result.Success(new PersonEditDto(
            person.PublicId,
            person.IdType,
            person.TaxId,
            person.TaxIdCheckDigit,
            person.IdIssuedAt,
            person.IdIssueDate,
            person.FirstName,
            person.LastName,
            person.SecondLastName,
            person.OtherNames,
            person.BusinessName,
            person.PersonType,
            person.Address,
            person.Phone1,
            person.Phone2,
            person.Mobile,
            person.Email,
            person.City?.PublicId,
            person.City?.Name,
            person.Gender,
            person.MaritalStatus,
            person.DateOfBirth,
            person.EducationLevel,
            person.IsAssociate,
            person.IsEmployee,
            person.IsThirdParty,
            person.IsAdvisor,
            person.IsCustomer,
            person.IsSupplier,
            person.IsSalesperson,
            person.ReceivesInvoice,
            person.Status));
    }
}
