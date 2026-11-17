using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Services;
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
    // Feature 008, fase 2: los filtros de Personas para los roles que son una marca y para vendedores.
    public bool? IsSupplier { get; init; }
    public bool? IsCustomer { get; init; }
    public bool? IsSalesperson { get; init; }

    // 2026-11-16 (pedido del dueño): los filtros por columna de /maestros/personas van al servidor —con 12.000 personas, filtrar sólo las
    // cargadas no encontraba a nadie fuera de la primera página—. Todos contienen el texto, sin distinguir mayúsculas.
    public string? IdType { get; init; }
    public string? TaxId { get; init; }
    public string? Name { get; init; }
    public string? Email { get; init; }
    public string? Mobile { get; init; }
    public string? City { get; init; }
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
        if (request.IsSupplier == true) query = query.Where(p => p.IsSupplier);
        if (request.IsCustomer == true) query = query.Where(p => p.IsCustomer);
        if (request.IsSalesperson == true) query = query.Where(p => p.IsSalesperson);

        // 2026-11-16 (pedido del dueño): la búsqueda no distingue mayúsculas y cada palabra puede caer en un campo distinto («juan perez»
        // encuentra a Juan Carlos Pérez… salvo la tilde). Antes era Contains literal: en PostgreSQL «perez» no encontraba «PEREZ».
        foreach (var palabra in Palabras(request.SearchTerm))
        {
            query = query.Where(p =>
                p.TaxId.ToLower().Contains(palabra)
                || p.FirstName.ToLower().Contains(palabra)
                || (p.OtherNames != null && p.OtherNames.ToLower().Contains(palabra))
                || p.LastName.ToLower().Contains(palabra)
                || (p.SecondLastName != null && p.SecondLastName.ToLower().Contains(palabra))
                || (p.BusinessName != null && p.BusinessName.ToLower().Contains(palabra))
                || (p.LegacyCode != null && p.LegacyCode.ToLower().Contains(palabra)));
        }

        if (Texto(request.IdType) is { } tipo) query = query.Where(p => p.IdType.ToLower().Contains(tipo));
        if (Texto(request.TaxId) is { } documento) query = query.Where(p => p.TaxId.ToLower().Contains(documento));
        foreach (var palabra in Palabras(request.Name))
        {
            query = query.Where(p =>
                p.FirstName.ToLower().Contains(palabra)
                || (p.OtherNames != null && p.OtherNames.ToLower().Contains(palabra))
                || p.LastName.ToLower().Contains(palabra)
                || (p.SecondLastName != null && p.SecondLastName.ToLower().Contains(palabra))
                || (p.BusinessName != null && p.BusinessName.ToLower().Contains(palabra)));
        }
        if (Texto(request.Email) is { } correo) query = query.Where(p => p.Email != null && p.Email.ToLower().Contains(correo));
        if (Texto(request.Mobile) is { } movil) query = query.Where(p => p.Mobile != null && p.Mobile.ToLower().Contains(movil));
        if (Texto(request.City) is { } ciudad) query = query.Where(p => p.City != null && p.City.Name.ToLower().Contains(ciudad));

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
                PersonFactory.NombreVisible(p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName),
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

    /// <summary>El texto de un filtro en minúsculas y sin espacios de los extremos; nulo si viene vacío.</summary>
    internal static string? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim().ToLowerInvariant();

    /// <summary>Las palabras de una búsqueda, en minúsculas.</summary>
    internal static IEnumerable<string> Palabras(string? valor) =>
        Texto(valor)?.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct() ?? [];
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
    string? Status)
{
    // Perfil tributario (feature 012, T173): lo que PersonInput escribe tiene que volver aquí.
    public bool IsVatResponsible { get; init; }
    public bool IsSelfWithholder { get; init; }
    public bool IsVatWithholdingAgent { get; init; }
    public bool IsSimpleTaxRegime { get; init; }
    public bool IsIncomeTaxFiler { get; init; }
    public bool IsObligatedToInvoice { get; init; }
    public bool IsObligatedToKeepBooks { get; init; }
    public bool IsLargeContributor { get; init; }
    public bool WithholdingExempt { get; init; }
    public bool IcaWithholdingExempt { get; init; }
    public string? CiiuCode { get; init; }

    /// <summary>
    /// 2026-11-16: la ciudad de la persona está dada de baja. Antes el filtro de bajas del <c>Include</c> la dejaba en nulo: la pantalla
    /// la mostraba vacía y el guardado siguiente la borraba. Ahora vuelve con su nombre y esta marca, y guardarla sin cambio se acepta.
    /// </summary>
    public bool CityRetired { get; init; }
}

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

        // La ciudad dada de baja no llega por el Include (filtro de bajas): se lee aparte y se marca retirada.
        (Guid PublicId, string Name)? ciudad = person.City is { } viva ? (viva.PublicId, viva.Name) : null;
        var ciudadRetirada = false;
        if (ciudad is null && person.CityId is { } ciudadId)
        {
            var retirada = await context.Cities.IgnoreQueryFilters().AsNoTracking()
                .Where(c => c.Id == ciudadId)
                .Select(c => new { c.PublicId, c.Name })
                .FirstOrDefaultAsync(ct);
            if (retirada is not null)
            {
                ciudad = (retirada.PublicId, retirada.Name);
                ciudadRetirada = true;
            }
        }

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
            ciudad?.PublicId,
            ciudad?.Name,
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
            person.Status)
        {
            IsVatResponsible = person.IsVatResponsible,
            IsSelfWithholder = person.IsSelfWithholder,
            IsVatWithholdingAgent = person.IsVatWithholdingAgent,
            IsSimpleTaxRegime = person.IsSimpleTaxRegime,
            IsIncomeTaxFiler = person.IsIncomeTaxFiler,
            IsObligatedToInvoice = person.IsObligatedToInvoice,
            IsObligatedToKeepBooks = person.IsObligatedToKeepBooks,
            IsLargeContributor = person.IsLargeContributor,
            WithholdingExempt = person.WithholdingExempt,
            IcaWithholdingExempt = person.IcaWithholdingExempt,
            CiiuCode = person.CiiuCode,
            CityRetired = ciudadRetirada,
        });
    }
}
