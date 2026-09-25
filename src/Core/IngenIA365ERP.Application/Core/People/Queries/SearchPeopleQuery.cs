using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.People.Queries;

/// <summary>Una fila del buscador de personas. Desde la feature 008 trae las ocho banderas, para que el picker las muestre y filtre.</summary>
public record PersonSearchDto(
    Guid PublicId,
    string IdentificationNumber,
    string FullName,
    bool IsAssociate,
    bool IsEmployee,
    bool IsSalesperson,
    bool IsCustomer,
    bool IsSupplier,
    bool IsAdvisor,
    bool IsThirdParty,
    bool ReceivesInvoice,
    string? CityName,
    string? Status);

/// <summary>
/// Busca por documento, nombre, razón social o código heredado. <paramref name="Role"/> (feature
/// 008, FR-013) filtra por bandera: <c>associate</c>, <c>employee</c>, <c>salesperson</c>,
/// <c>customer</c>, <c>supplier</c>, <c>advisor</c>, <c>thirdparty</c>; nulo o vacío = todas.
/// Nunca devuelve eliminadas.
/// </summary>
public record SearchPeopleQuery(string SearchTerm, string? Role = null) : IRequest<Result<List<PersonSearchDto>>>;

public class SearchPeopleQueryHandler(IApplicationDbContext context)
    : IRequestHandler<SearchPeopleQuery, Result<List<PersonSearchDto>>>
{
    public static readonly IReadOnlyList<string> RolesAdmitidos =
        ["associate", "employee", "salesperson", "customer", "supplier", "advisor", "thirdparty"];

    public async Task<Result<List<PersonSearchDto>>> Handle(SearchPeopleQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SearchTerm))
            return Result.Success(new List<PersonSearchDto>());

        var term = request.SearchTerm.Trim();

        var query = context.People
            .AsNoTracking()
            .Include(p => p.City)
            .Where(p => !p.IsDeleted &&
                (p.TaxId.Contains(term) ||
                 p.FirstName.Contains(term) ||
                 (p.OtherNames != null && p.OtherNames.Contains(term)) ||
                 p.LastName.Contains(term) ||
                 (p.SecondLastName != null && p.SecondLastName.Contains(term)) ||
                 (p.BusinessName != null && p.BusinessName.Contains(term)) ||
                 (p.LegacyCode != null && p.LegacyCode.Contains(term))));

        query = FiltrarPorRol(query, request.Role);

        var results = await query
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .Take(50)
            .Select(p => new PersonSearchDto(
                p.PublicId,
                p.TaxId,
                PersonFactory.NombreVisible(p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName),
                p.IsAssociate,
                p.IsEmployee,
                p.IsSalesperson,
                p.IsCustomer,
                p.IsSupplier,
                p.IsAdvisor,
                p.IsThirdParty,
                p.ReceivesInvoice,
                p.City != null ? p.City.Name : null,
                p.Status))
            .ToListAsync(ct);

        return Result.Success(results);
    }

    private static IQueryable<Person> FiltrarPorRol(IQueryable<Person> query, string? rol) =>
        rol?.Trim().ToLowerInvariant() switch
        {
            "associate" => query.Where(p => p.IsAssociate),
            "employee" => query.Where(p => p.IsEmployee),
            "salesperson" => query.Where(p => p.IsSalesperson),
            "customer" => query.Where(p => p.IsCustomer),
            "supplier" => query.Where(p => p.IsSupplier),
            "advisor" => query.Where(p => p.IsAdvisor),
            "thirdparty" => query.Where(p => p.IsThirdParty),
            _ => query,
        };
}

public sealed class SearchPeopleQueryValidator : AbstractValidator<SearchPeopleQuery>
{
    public SearchPeopleQueryValidator()
    {
        RuleFor(x => x.SearchTerm).MaximumLength(150);
        RuleFor(x => x.Role)
            .Must(r => string.IsNullOrWhiteSpace(r) || SearchPeopleQueryHandler.RolesAdmitidos.Contains(r.Trim().ToLowerInvariant()))
            .WithMessage("Rol de búsqueda no reconocido.");
    }
}
