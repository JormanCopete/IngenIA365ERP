using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Saas.NationalValues;

/// <summary>Una fila del registro central para la pantalla del maestro (contracts/api.md §21.5). (nuevo)</summary>
public sealed record PlatformNationalValueDto(
    Guid PublicId,
    string Code,
    string Key,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    DateOnly? ReferenceDate,
    decimal? Value,
    string? Unit,
    string? Label,
    string Source,
    string? Reason,
    long Sequence,
    DateTime RegisteredAt,
    string RegisteredBy,
    Guid? SupersedesPublicId,
    Guid? SupersededByPublicId);

/// <summary>Un documento normativo de la central, sin su contenido. (nuevo)</summary>
public sealed record PlatformRegulatoryDocumentDto(
    Guid PublicId,
    string Kind,
    string Code,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    short SchemaVersion,
    bool Verified,
    string Sha256,
    string Source,
    long Sequence,
    DateTime RegisteredAt,
    string RegisteredBy,
    Guid? SupersedesPublicId);

/// <summary>El contenido de un documento de la central, con su huella. (nuevo)</summary>
public sealed record PlatformRegulatoryDocumentContentDto(Guid PublicId, string Kind, string Code, short SchemaVersion, string Sha256, string Content);

/// <summary>Un valor esperado que la central no tiene para los próximos días (C69). (nuevo)</summary>
public sealed record MissingNationalValueDto(string Code, string Key, DateOnly Date, string Reason);

// ------------------------------------------------------------------------------------------------------ listados --

/// <summary>Los valores del registro central, más nuevos primero, con su numeración y quién los reemplazó. (nuevo)</summary>
public sealed record ListPlatformNationalValuesQuery(string? Code = null, string? Key = null, DateOnly? From = null, DateOnly? To = null, int Page = 1, int PageSize = 50)
    : IRequest<Result<PagedList<PlatformNationalValueDto>>>;

public sealed class ListPlatformNationalValuesQueryHandler(IAdminDbContext db)
    : IRequestHandler<ListPlatformNationalValuesQuery, Result<PagedList<PlatformNationalValueDto>>>
{
    public async Task<Result<PagedList<PlatformNationalValueDto>>> Handle(ListPlatformNationalValuesQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var size = Math.Clamp(request.PageSize, 1, 500);
        var q = db.PlatformNationalValues.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Code)) q = q.Where(v => v.Code == request.Code.Trim().ToUpperInvariant());
        if (!string.IsNullOrWhiteSpace(request.Key)) q = q.Where(v => v.Key == request.Key.Trim());
        if (request.From is { } desde) q = q.Where(v => v.ValidFrom >= desde);
        if (request.To is { } hasta) q = q.Where(v => v.ValidFrom <= hasta);

        var total = await q.CountAsync(ct);
        var filas = await q.OrderByDescending(v => v.Sequence).Skip((page - 1) * size).Take(size).ToListAsync(ct);
        var ids = filas.Select(f => f.PublicId).ToList();
        var reemplazos = await db.PlatformNationalValues.AsNoTracking()
            .Where(v => v.SupersedesPublicId != null && ids.Contains(v.SupersedesPublicId.Value))
            .Select(v => new { Anterior = v.SupersedesPublicId!.Value, v.PublicId })
            .ToListAsync(ct);
        var por = reemplazos.ToDictionary(r => r.Anterior, r => r.PublicId);

        return Result.Success(new PagedList<PlatformNationalValueDto>(
            filas.Select(f => new PlatformNationalValueDto(f.PublicId, f.Code, f.Key, f.ValidFrom, f.ValidTo, f.ReferenceDate, f.Value, f.Unit,
                f.Label, f.Source, f.Reason, f.Sequence, f.RegisteredAt, f.RegisteredBy, f.SupersedesPublicId,
                por.TryGetValue(f.PublicId, out var p) ? p : null)).ToList(),
            total, page, size));
    }
}

/// <summary>Los documentos normativos de la central, de una clase o todos, más nuevos primero. (nuevo)</summary>
public sealed record ListPlatformRegulatoryDocumentsQuery(string? Kind = null) : IRequest<Result<IReadOnlyList<PlatformRegulatoryDocumentDto>>>;

public sealed class ListPlatformRegulatoryDocumentsQueryHandler(IAdminDbContext db)
    : IRequestHandler<ListPlatformRegulatoryDocumentsQuery, Result<IReadOnlyList<PlatformRegulatoryDocumentDto>>>
{
    public async Task<Result<IReadOnlyList<PlatformRegulatoryDocumentDto>>> Handle(ListPlatformRegulatoryDocumentsQuery request, CancellationToken ct)
    {
        var q = db.PlatformRegulatoryDocuments.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Kind)) q = q.Where(d => d.Kind == request.Kind.Trim());
        var filas = await q.OrderByDescending(d => d.Sequence)
            .Select(d => new PlatformRegulatoryDocumentDto(d.PublicId, d.Kind, d.Code, d.ValidFrom, d.ValidTo, d.SchemaVersion, d.Verified,
                d.ContentSha256, d.Source, d.Sequence, d.RegisteredAt, d.RegisteredBy, d.SupersedesPublicId))
            .ToListAsync(ct);
        return Result.Success<IReadOnlyList<PlatformRegulatoryDocumentDto>>(filas);
    }
}

/// <summary>El JSON de un documento de la central. (nuevo)</summary>
public sealed record GetPlatformRegulatoryDocumentContentQuery(Guid PublicId) : IRequest<Result<PlatformRegulatoryDocumentContentDto>>;

public sealed class GetPlatformRegulatoryDocumentContentQueryHandler(IAdminDbContext db)
    : IRequestHandler<GetPlatformRegulatoryDocumentContentQuery, Result<PlatformRegulatoryDocumentContentDto>>
{
    public async Task<Result<PlatformRegulatoryDocumentContentDto>> Handle(GetPlatformRegulatoryDocumentContentQuery request, CancellationToken ct)
    {
        var d = await db.PlatformRegulatoryDocuments.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.PublicId, ct);
        return d is null
            ? Result.Failure<PlatformRegulatoryDocumentContentDto>(ErroresDeValoresNacionales.DocumentoNoEncontrado)
            : Result.Success(new PlatformRegulatoryDocumentContentDto(d.PublicId, d.Kind, d.Code, d.SchemaVersion, d.ContentSha256, d.Content));
    }
}

// ------------------------------------------------------------------------------------------------------ faltantes --

/// <summary>
/// Qué valores esperados no tienen vigencia en la central para los próximos <see cref="Days"/> días (feature 013, FR-015, C69;
/// data-model parte 3 §3): calculado <b>sin leer cooperativas</b>. Se esperan el SMMLV y la UVT, la usura de cada modalidad
/// certificada del catálogo vigente, cada límite legal del catálogo y al menos un festivo del año del horizonte. Los índices
/// (IBR, IPC) sólo los necesitan las líneas variables y la central no lo sabe: los avisa cada cooperativa
/// (<c>Cartera.IndiceDesactualizado</c>). No hay tabla ni alerta central: la pantalla <c>/admin/valores-nacionales</c> lo muestra. (nuevo)
/// </summary>
public sealed record ListMissingPlatformNationalValuesQuery(int Days = 31) : IRequest<Result<IReadOnlyList<MissingNationalValueDto>>>;

public sealed class ListMissingPlatformNationalValuesQueryHandler(IAdminDbContext db, IDateTimeService reloj)
    : IRequestHandler<ListMissingPlatformNationalValuesQuery, Result<IReadOnlyList<MissingNationalValueDto>>>
{
    public async Task<Result<IReadOnlyList<MissingNationalValueDto>>> Handle(ListMissingPlatformNationalValuesQuery request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var horizonte = hoy.AddDays(Math.Clamp(request.Days, 0, 400));
        var faltan = new List<MissingNationalValueDto>();

        var valores = await db.PlatformNationalValues.AsNoTracking()
            .Where(v => v.ValidFrom <= horizonte && v.Code != CodigosDeValorNacional.Festivo)
            .ToListAsync(ct);
        var porSerie = valores.ToLookup(v => (v.Code, v.Key));

        void Exigir(string codigo, string clave, string motivo)
        {
            foreach (var fecha in new[] { hoy, horizonte }.Distinct())
            {
                if (VigenciaBitemporal.Vigente(porSerie[(codigo, clave)], fecha, DateTime.MaxValue) is not null) continue;
                faltan.Add(new MissingNationalValueDto(codigo, clave, fecha, motivo));
                break;
            }
        }

        Exigir(CodigosDeValorNacional.Smmlv, string.Empty, "Nómina y los límites en veces el salario mínimo lo leen.");
        Exigir(CodigosDeValorNacional.Uvt, string.Empty, "Nómina y los impuestos del comercio la leen.");
        foreach (var limite in CatalogoDeLimitesLegales.Todos)
            Exigir(CodigosDeValorNacional.Limite, limite.Codigo, $"Acota {limite.Acota}.");
        foreach (var modalidad in await ReglasDelRegistroCentral.ClavesDeUsuraAsync(db, horizonte, ct))
            Exigir(CodigosDeValorNacional.Usura, modalidad, "La usura del período: sin ella no se causa ni se origina en esa modalidad.");

        var año = (short)horizonte.Year;
        var primero = new DateOnly(año, 1, 1);
        var ultimo = new DateOnly(año, 12, 31);
        if (!await db.PlatformNationalValues.AsNoTracking().AnyAsync(v => v.Code == CodigosDeValorNacional.Festivo && v.ValidFrom >= primero && v.ValidFrom <= ultimo, ct))
            faltan.Add(new MissingNationalValueDto(CodigosDeValorNacional.Festivo, $"{año}", primero,
                $"No hay festivos de {año}: los días hábiles de ese año los contarían como laborables."));

        return Result.Success<IReadOnlyList<MissingNationalValueDto>>(faltan);
    }
}
