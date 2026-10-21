using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Domain.Common.Compliance;
using IngenIA365ERP.Domain.Enums.Compliance;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Compliance.RestrictiveLists;

/// <summary>
/// Una carga de lista restrictiva (contracts/api.md §22.1). <c>Status</c> se deriva a la fecha local: <c>Active</c> la vigente de su lista,
/// <c>Replaced</c> las anteriores y <c>Scheduled</c> **(nuevo)** la que todavía no empieza. (nuevo)
/// </summary>
public sealed record RestrictiveListLoadDto(
    Guid PublicId,
    string ListCode,
    string Source,
    DateOnly? PublishedOn,
    DateOnly ValidFrom,
    int Version,
    string Status,
    int Rows,
    string Sha256,
    string LoadedBy,
    DateTime LoadedAt);

/// <summary>Una entrada de una carga: datos de la lista pública, no una persona del maestro. (nuevo)</summary>
public sealed record RestrictiveListEntryDto(
    Guid PublicId,
    RestrictiveListEntityKind EntityKind,
    string? DocumentType,
    string? DocumentNumber,
    string FullName,
    string? Aliases,
    string? Program,
    string? Notes);

/// <summary>Los estados derivados de una carga. (nuevo)</summary>
public static class EstadosDeCargaDeLista
{
    public const string Active = "Active";
    public const string Replaced = "Replaced";
    public const string Scheduled = "Scheduled";
}

/// <summary>Las cargas de listas restrictivas, la más reciente primero (<c>GET /api/compliance/restrictive-lists</c>). (nuevo)</summary>
public sealed record ListRestrictiveListLoadsQuery(string? ListCode = null) : IRequest<Result<IReadOnlyList<RestrictiveListLoadDto>>>;

public sealed class ListRestrictiveListLoadsQueryValidator : AbstractValidator<ListRestrictiveListLoadsQuery>
{
    public ListRestrictiveListLoadsQueryValidator() => RuleFor(x => x.ListCode).MaximumLength(20);
}

public sealed class ListRestrictiveListLoadsQueryHandler(IApplicationDbContext db, IDateTimeService reloj)
    : IRequestHandler<ListRestrictiveListLoadsQuery, Result<IReadOnlyList<RestrictiveListLoadDto>>>
{
    public async Task<Result<IReadOnlyList<RestrictiveListLoadDto>>> Handle(ListRestrictiveListLoadsQuery request, CancellationToken ct)
    {
        var consulta = db.RestrictiveListLoads.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.ListCode))
        {
            var codigo = request.ListCode.Trim().ToUpperInvariant();
            consulta = consulta.Where(c => c.ListCode == codigo);
        }
        var cargas = await consulta.ToListAsync(ct);
        var hoy = reloj.HoyLocal;
        var vigentes = cargas.Where(c => c.ValidFrom <= hoy)
            .GroupBy(c => c.ListCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(c => c.ValidFrom).ThenByDescending(c => c.Version).First().Id)
            .ToHashSet();

        IReadOnlyList<RestrictiveListLoadDto> lista = cargas
            .OrderBy(c => c.ListCode, StringComparer.Ordinal).ThenByDescending(c => c.Version)
            .Select(c => new RestrictiveListLoadDto(c.PublicId, c.ListCode, c.Source, c.PublishedOn, c.ValidFrom, c.Version,
                c.ValidFrom > hoy ? EstadosDeCargaDeLista.Scheduled : vigentes.Contains(c.Id) ? EstadosDeCargaDeLista.Active : EstadosDeCargaDeLista.Replaced,
                c.EntryCount, c.FileSha256, c.LoadedByName, c.CreatedAt))
            .ToList();
        return Result.Success(lista);
    }
}

/// <summary>
/// Las entradas de una carga, con búsqueda por nombre o documento (<c>GET /api/compliance/restrictive-lists/{id}/entries?search=&amp;page=</c>).
/// Carga inexistente: <c>Compliance.RestrictiveList.NotFound</c>. (nuevo)
/// </summary>
public sealed record ListRestrictiveListEntriesQuery(Guid LoadPublicId, string? Search = null, int Page = 1, int PageSize = 50)
    : IRequest<Result<PagedResult<RestrictiveListEntryDto>>>;

public sealed class ListRestrictiveListEntriesQueryValidator : AbstractValidator<ListRestrictiveListEntriesQuery>
{
    public ListRestrictiveListEntriesQueryValidator()
    {
        RuleFor(x => x.LoadPublicId).NotEmpty();
        RuleFor(x => x.Search).MaximumLength(300);
    }
}

public sealed class ListRestrictiveListEntriesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListRestrictiveListEntriesQuery, Result<PagedResult<RestrictiveListEntryDto>>>
{
    public async Task<Result<PagedResult<RestrictiveListEntryDto>>> Handle(ListRestrictiveListEntriesQuery request, CancellationToken ct)
    {
        var carga = await db.RestrictiveListLoads.AsNoTracking().Where(c => c.PublicId == request.LoadPublicId).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
        if (carga is null) return Result.Failure<PagedResult<RestrictiveListEntryDto>>(ErroresDeListasRestrictivas.NoEncontrada);

        var pagina = new PageRequest(request.Page, request.PageSize);
        var consulta = db.RestrictiveListEntries.AsNoTracking().Where(e => e.LoadId == carga);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var nombre = CoincidenciaDeNombres.Normalizar(request.Search);
            var documento = CoincidenciaDeNombres.NormalizarDocumento(request.Search);
            consulta = consulta.Where(e => (nombre.Length > 0 && e.NameNormalized.Contains(nombre))
                || (documento.Length > 0 && e.DocumentNumberNormalized == documento));
        }

        var total = await consulta.LongCountAsync(ct);
        var items = await consulta.OrderBy(e => e.NameNormalized).ThenBy(e => e.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize)
            .Select(e => new RestrictiveListEntryDto(e.PublicId, e.EntityKind, e.DocumentType, e.DocumentNumberNormalized, e.FullName, e.Aliases, e.Program, e.Notes))
            .ToListAsync(ct);
        return Result.Success(new PagedResult<RestrictiveListEntryDto>(items, pagina.SafePage, pagina.SafePageSize, total));
    }
}
