using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Personas;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Domain.Common.Identidad;
using IngenIA365ERP.Domain.Common.Text;
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
///
/// <para>
/// Feature 022 (T014; FR-020 a FR-024, plan §8.1): cada término normalizado (sin tildes ni mayúsculas, en cualquier
/// orden) tiene que estar en <c>COR_People.SearchText</c> (<c>LIKE '%término%'</c> escapado, como la búsqueda de
/// productos); además entra la persona cuyo documento es exactamente lo escrito aunque venga con puntos o guiones (por
/// <see cref="ResolutorDePersonaPorDocumento"/>, el único sitio que busca por documento). Orden: documento exacto,
/// documento que empieza por lo escrito, primer término al comienzo de una palabra, el resto; dentro de cada grupo, por
/// nombre visible. Hasta <see cref="SearchPeopleQueryHandler.Toma"/> filas. <paramref name="Ids"/> (los recientes del
/// equipo, hasta 20) devuelve esas personas en ese orden e ignora el término.
/// </para>
/// </summary>
public record SearchPeopleQuery(string SearchTerm, string? Role = null, IReadOnlyList<Guid>? Ids = null)
    : IRequest<Result<List<PersonSearchDto>>>;

public class SearchPeopleQueryHandler(IApplicationDbContext context)
    : IRequestHandler<SearchPeopleQuery, Result<List<PersonSearchDto>>>
{
    public static readonly IReadOnlyList<string> RolesAdmitidos =
        ["associate", "employee", "salesperson", "customer", "supplier", "advisor", "thirdparty"];

    /// <summary>Feature 022: filas de una búsqueda y máximo de recientes por pedido.</summary>
    public const int Toma = 20;

    public async Task<Result<List<PersonSearchDto>>> Handle(SearchPeopleQuery request, CancellationToken ct)
    {
        var vivas = FiltrarPorRol(context.People.AsNoTracking().Where(p => !p.IsDeleted), request.Role);

        if (request.Ids is { Count: > 0 } pedidos)
        {
            var lista = pedidos.Distinct().Take(Toma).ToList();
            var porPublico = (await ProyectarAsync(vivas.Where(p => lista.Contains(p.PublicId)), ct))
                .ToDictionary(f => f.Dto.PublicId, f => f.Dto);
            return Result.Success(lista.Where(porPublico.ContainsKey).Select(id => porPublico[id]).ToList());
        }

        var terminos = NormalizadorDeBusqueda.Terminos(request.SearchTerm);
        if (terminos.Count == 0)
            return Result.Success(new List<PersonSearchDto>());

        var escape = NormalizadorDeBusqueda.CaracterDeEscape.ToString();
        var documento = UsuarioDeDocumento.NormalizarNumero(request.SearchTerm);
        var buscaDocumento = documento.Length >= NormalizadorDeBusqueda.MinimoDeCaracteres;

        // 1) El documento exacto, aunque se haya escrito con puntos («1.023.456.789»): siempre primero.
        List<int> exactos = buscaDocumento
            ? await ResolutorDePersonaPorDocumento.Filtrar(vivas, null, request.SearchTerm)
                .OrderBy(p => p.IdType).Select(p => p.Id).Take(Toma).ToListAsync(ct)
            : [];

        // 2) Todos los términos en el texto de búsqueda, en el orden del contrato.
        var coinciden = vivas;
        foreach (var patron in NormalizadorDeBusqueda.PatronesContiene(request.SearchTerm))
            coinciden = coinciden.Where(p => EF.Functions.Like(p.SearchText, patron, escape));

        var primero = NormalizadorDeBusqueda.EscaparParaLike(terminos[0]);
        var alInicio = primero + "%";
        var enPalabra = "% " + primero + "%";
        var ordenadas = buscaDocumento
            ? coinciden.OrderBy(p => p.TaxId.StartsWith(documento) ? 0
                : EF.Functions.Like(p.SearchText, alInicio, escape) || EF.Functions.Like(p.SearchText, enPalabra, escape) ? 1
                : 2)
            : coinciden.OrderBy(p =>
                EF.Functions.Like(p.SearchText, alInicio, escape) || EF.Functions.Like(p.SearchText, enPalabra, escape) ? 1 : 2);
        var resto = await ordenadas
            .ThenBy(p => p.BusinessName != null && p.BusinessName != ""
                ? p.BusinessName
                : p.FirstName + " " + (p.OtherNames ?? "") + " " + p.LastName + " " + (p.SecondLastName ?? ""))
            .ThenBy(p => p.TaxId)
            .Select(p => p.Id)
            .Take(Toma)
            .ToListAsync(ct);

        var ids = exactos.Concat(resto).Distinct().Take(Toma).ToList();
        var porInterno = (await ProyectarAsync(vivas.Where(p => ids.Contains(p.Id)), ct)).ToDictionary(f => f.Id, f => f.Dto);
        return Result.Success(ids.Where(porInterno.ContainsKey).Select(id => porInterno[id]).ToList());
    }

    private static async Task<List<(int Id, PersonSearchDto Dto)>> ProyectarAsync(IQueryable<Person> query, CancellationToken ct)
    {
        var filas = await query
            .Select(p => new
            {
                p.Id, p.PublicId, p.TaxId, p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName,
                p.IsAssociate, p.IsEmployee, p.IsSalesperson, p.IsCustomer, p.IsSupplier, p.IsAdvisor, p.IsThirdParty,
                p.ReceivesInvoice,
                Ciudad = p.City != null ? p.City.Name : null,
                p.Status,
            })
            .ToListAsync(ct);
        return filas.Select(p => (p.Id, new PersonSearchDto(
                p.PublicId,
                p.TaxId,
                PersonFactory.NombreVisible(p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName),
                p.IsAssociate, p.IsEmployee, p.IsSalesperson, p.IsCustomer, p.IsSupplier, p.IsAdvisor, p.IsThirdParty,
                p.ReceivesInvoice, p.Ciudad, p.Status)))
            .ToList();
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
        // Feature 022 (T014): los recientes, hasta 20 identificadores válidos.
        RuleFor(x => x.Ids)
            .Must(ids => ids is null || ids.Count <= SearchPeopleQueryHandler.Toma)
            .WithMessage($"Se pueden pedir hasta {SearchPeopleQueryHandler.Toma} personas recientes.");
        RuleForEach(x => x.Ids).NotEmpty().WithMessage("Identificador de persona no válido.");
    }
}
