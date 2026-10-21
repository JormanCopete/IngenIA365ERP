using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Lines;

// Feature 014, US1 (T066; api.md §3): las consultas del catálogo común de líneas. La lista filtra en silencio por el alcance de líneas y por las
// naturalezas cuyo «View» tiene quien pregunta; el detalle y las versiones de una línea fuera del alcance —o de una naturaleza que no puede ver—
// responden el mismo 404 que una inexistente. La versión a una fecha es bitemporal (VigenciaBitemporal): la vigente en «date» con lo registrado hasta
// «recordedAsOf». (nuevo)

/// <summary>Los permisos de lectura y de administración de cada naturaleza (api.md §2). (nuevo)</summary>
public static class PermisosDeLineas
{
    public const string VerCredito = "Lending.CreditLines.View";
    public const string VerCaptacion = "Lending.DepositLines.View";
    public const string AdministrarCaptacion = "Lending.DepositLines.Manage";
    public const string VerAportes = "Lending.ContributionLines.View";
    public const string AdministrarAportes = "Lending.ContributionLines.Manage";

    public static string VerDe(LineNature naturaleza) => naturaleza switch
    {
        LineNature.Deposit => VerCaptacion,
        LineNature.Contribution => VerAportes,
        _ => VerCredito,
    };

    public static string AdministrarDe(LineNature naturaleza) => naturaleza == LineNature.Contribution ? AdministrarAportes : AdministrarCaptacion;

    /// <summary>Las naturalezas cuyo permiso <c>View</c> tiene quien pregunta (el maestro, todas).</summary>
    public static async Task<HashSet<LineNature>> VisiblesAsync(ICurrentUserPermissions permisos, CancellationToken ct)
    {
        if (permisos.EsMaestroGlobal) return [LineNature.Placement, LineNature.Deposit, LineNature.Contribution];
        var concedidos = new HashSet<string>(await permisos.ListAsync(ct), StringComparer.OrdinalIgnoreCase);
        return [.. new[] { LineNature.Placement, LineNature.Deposit, LineNature.Contribution }.Where(n => concedidos.Contains(VerDe(n)))];
    }

    /// <summary>¿Tiene quien pregunta el permiso <paramref name="codigo"/>?</summary>
    public static async Task<bool> TieneAsync(ICurrentUserPermissions permisos, string codigo, CancellationToken ct) =>
        permisos.EsMaestroGlobal || (await permisos.ListAsync(ct)).Contains(codigo, StringComparer.OrdinalIgnoreCase);
}

// --------------------------------------------------------------------------------------------------------------- lista --

/// <summary>
/// El catálogo común de líneas (api.md §3, <c>GET /api/lending/lines?nature=&amp;status=&amp;depositClass=&amp;includeClosed=</c>): por código, sólo
/// las del alcance y de las naturalezas que quien pregunta puede ver; sin <c>includeClosed</c>, las cerradas no salen. Cada una con el resumen de su
/// versión vigente hoy. (nuevo)
/// </summary>
public sealed record ListLendingLinesQuery(
    LineNature? Nature = null,
    CreditLineStatus? Status = null,
    DepositClass? DepositClass = null,
    bool? IncludeClosed = null) : IRequest<Result<IReadOnlyList<LendingLineListItemDto>>>;

public sealed class ListLendingLinesQueryValidator : AbstractValidator<ListLendingLinesQuery>
{
    public ListLendingLinesQueryValidator()
    {
        RuleFor(x => x.Nature).IsInEnum().When(x => x.Nature is not null);
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.DepositClass).IsInEnum().When(x => x.DepositClass is not null);
    }
}

/// <summary>La clase de captación con otra naturaleza → <c>Lending.Report.FilterInvalid</c>.</summary>
public sealed class ListLendingLinesQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcanceDeLaPeticion,
    ICurrentUserPermissions permisos,
    IDateTimeService reloj) : IRequestHandler<ListLendingLinesQuery, Result<IReadOnlyList<LendingLineListItemDto>>>
{
    public async Task<Result<IReadOnlyList<LendingLineListItemDto>>> Handle(ListLendingLinesQuery request, CancellationToken ct)
    {
        if (request.DepositClass is not null && request.Nature is { } n && n != LineNature.Deposit)
            return Result.Failure<IReadOnlyList<LendingLineListItemDto>>(ErroresDeLineasDeCaptacion.FiltroInvalido(
                "La clase de captación sólo filtra líneas de captación: quite la clase o pida la naturaleza Deposit."));

        var visibles = await PermisosDeLineas.VisiblesAsync(permisos, ct);
        if (request.Nature is { } pedida) visibles.IntersectWith([pedida]);
        if (request.DepositClass is not null) visibles.IntersectWith([LineNature.Deposit]);
        if (visibles.Count == 0) return Result.Success<IReadOnlyList<LendingLineListItemDto>>([]);

        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var naturalezas = visibles.ToArray();
        var consulta = db.CreditLines.AsNoTracking().Where(l => !l.IsDeleted && naturalezas.Contains(l.Nature)).PorLinea(alcance, l => l.Id);
        if (request.Status is { } estado) consulta = consulta.Where(l => l.Status == estado);
        else if (request.IncludeClosed != true) consulta = consulta.Where(l => l.Status != CreditLineStatus.Closed);
        var lineas = await consulta.OrderBy(l => l.Code).ToListAsync(ct);

        var hoy = reloj.HoyLocal;
        var ids = lineas.Select(l => l.Id).ToArray();
        var deCaptacion = await db.DepositLineVersions.AsNoTracking().Where(v => ids.Contains(v.CreditLineId) && !v.IsDeleted).ToListAsync(ct);
        var deAportes = await db.ContributionLineVersions.AsNoTracking().Where(v => ids.Contains(v.CreditLineId) && !v.IsDeleted).ToListAsync(ct);
        var deCredito = await db.CreditLineVersions.AsNoTracking().Where(v => ids.Contains(v.CreditLineId) && !v.IsDeleted).ToListAsync(ct);
        var vigentesDeCaptacion = lineas.Select(l => VigenciaBitemporal.VigenteDeCartera(deCaptacion.Where(v => v.CreditLineId == l.Id), hoy, DateTime.MaxValue))
            .Where(v => v is not null).Select(v => v!.Id).ToArray();
        var tramos = (await db.DepositLineRateTiers.AsNoTracking().Where(t => vigentesDeCaptacion.Contains(t.DepositLineVersionId) && !t.IsDeleted).ToListAsync(ct))
            .ToLookup(t => t.DepositLineVersionId);

        var lista = new List<LendingLineListItemDto>(lineas.Count);
        foreach (var linea in lineas)
        {
            LendingLineCurrentVersionDto? actual = null;
            DepositClass? clase = null;
            switch (linea.Nature)
            {
                case LineNature.Deposit:
                {
                    var suyas = deCaptacion.Where(v => v.CreditLineId == linea.Id).ToList();
                    clase = suyas.OrderBy(v => v.VersionNumber).Select(v => (DepositClass?)v.DepositClass).FirstOrDefault();
                    if (request.DepositClass is { } filtro && clase != filtro) continue;
                    var v = VigenciaBitemporal.VigenteDeCartera(suyas, hoy, DateTime.MaxValue);
                    if (v is not null)
                        actual = new LendingLineCurrentVersionDto(v.PublicId, v.VersionNumber, v.ValidFrom, LectorDeLineasDeCaptacion.Resumen(v, tramos[v.Id].ToList()));
                    break;
                }
                case LineNature.Contribution:
                {
                    var v = VigenciaBitemporal.VigenteDeCartera(deAportes.Where(x => x.CreditLineId == linea.Id), hoy, DateTime.MaxValue);
                    if (v is not null) actual = new LendingLineCurrentVersionDto(v.PublicId, v.VersionNumber, v.ValidFrom, LectorDeLineasDeCaptacion.Resumen(v));
                    break;
                }
                default:
                {
                    var v = VigenciaBitemporal.VigenteDeCartera(deCredito.Where(x => x.CreditLineId == linea.Id), hoy, DateTime.MaxValue);
                    if (v is not null)
                        actual = new LendingLineCurrentVersionDto(v.PublicId, v.VersionNumber, v.ValidFrom,
                            v.RateKind == RateKind.Variable ? $"Crédito · {v.ModalityCode} · {v.IndexCode}" : $"Crédito · {v.ModalityCode}");
                    break;
                }
            }
            lista.Add(new LendingLineListItemDto(linea.PublicId, linea.Code, linea.Name, linea.Nature, clase, linea.Status, linea.ClosedReason, actual));
        }
        return Result.Success<IReadOnlyList<LendingLineListItemDto>>(lista);
    }
}

// ------------------------------------------------------------------------------------------------------------- detalle --

/// <summary>
/// Una línea del catálogo con la versión vigente en <c>date</c> (por defecto hoy) según su naturaleza, con lo registrado hasta <c>recordedAsOf</c>
/// (por defecto todo), y su historial (api.md §3, <c>GET /{id}?date=</c>; US1, escenario independiente). (nuevo)
/// </summary>
public sealed record GetLendingLineQuery(Guid LinePublicId, DateOnly? Date = null, DateTime? RecordedAsOf = null) : IRequest<Result<LendingLineDto>>;

public sealed class GetLendingLineQueryValidator : AbstractValidator<GetLendingLineQuery>
{
    public GetLendingLineQueryValidator() => RuleFor(x => x.LinePublicId).NotEmpty();
}

public sealed class GetLendingLineQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcanceDeLaPeticion,
    ICurrentUserPermissions permisos,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<GetLendingLineQuery, Result<LendingLineDto>>
{
    public async Task<Result<LendingLineDto>> Handle(GetLendingLineQuery request, CancellationToken ct)
    {
        var linea = await ConsultasDelCatalogo.VisibleAsync(db, alcanceDeLaPeticion, permisos, auditoria, request.LinePublicId, ct);
        if (linea.IsFailure) return Result.Failure<LendingLineDto>(linea.Error);
        var hasta = request.RecordedAsOf is { } r ? DateTime.SpecifyKind(r, DateTimeKind.Utc) : (DateTime?)null;
        return Result.Success(await LectorDeLineasDeCaptacion.DtoAsync(db, linea.Value, request.Date ?? reloj.HoyLocal, hasta, ct));
    }
}

// ----------------------------------------------------------------------------------------------------------- versiones --

/// <summary>
/// El historial de versiones de una línea de captación o de aportes (api.md §3, <c>GET /{id}/deposit-versions</c> y <c>/contribution-versions</c>):
/// la ruta dice la naturaleza que espera; una línea de otra naturaleza responde <c>Lending.CreditLine.NatureMismatch</c>. (nuevo)
/// </summary>
public sealed record ListLendingLineVersionsQuery(Guid LinePublicId, LineNature Nature) : IRequest<Result<IReadOnlyList<LendingLineVersionSummaryDto>>>;

public sealed class ListLendingLineVersionsQueryValidator : AbstractValidator<ListLendingLineVersionsQuery>
{
    public ListLendingLineVersionsQueryValidator()
    {
        RuleFor(x => x.LinePublicId).NotEmpty();
        RuleFor(x => x.Nature).Must(n => n is LineNature.Deposit or LineNature.Contribution).WithMessage("Sólo líneas de captación o de aportes.");
    }
}

public sealed class ListLendingLineVersionsQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcanceDeLaPeticion,
    ICurrentUserPermissions permisos,
    LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<ListLendingLineVersionsQuery, Result<IReadOnlyList<LendingLineVersionSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<LendingLineVersionSummaryDto>>> Handle(ListLendingLineVersionsQuery request, CancellationToken ct)
    {
        var linea = await ConsultasDelCatalogo.VisibleAsync(db, alcanceDeLaPeticion, permisos, auditoria, request.LinePublicId, ct);
        if (linea.IsFailure) return Result.Failure<IReadOnlyList<LendingLineVersionSummaryDto>>(linea.Error);
        if (linea.Value.ValidarNaturaleza(request.Nature) is { } distinta)
            return Result.Failure<IReadOnlyList<LendingLineVersionSummaryDto>>(ErroresDeLineasDeCaptacion.De(distinta));
        var dto = await LectorDeLineasDeCaptacion.DtoAsync(db, linea.Value, reloj.HoyLocal, null, ct);
        return Result.Success(dto.Versions);
    }
}

/// <summary>Una versión de captación completa con sus tramos (api.md §3, <c>GET /{id}/deposit-versions/{versionId}</c>). (nuevo)</summary>
public sealed record GetDepositLineVersionQuery(Guid LinePublicId, Guid VersionPublicId) : IRequest<Result<DepositLineVersionDto>>;

public sealed class GetDepositLineVersionQueryValidator : AbstractValidator<GetDepositLineVersionQuery>
{
    public GetDepositLineVersionQueryValidator()
    {
        RuleFor(x => x.LinePublicId).NotEmpty();
        RuleFor(x => x.VersionPublicId).NotEmpty();
    }
}

public sealed class GetDepositLineVersionQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcanceDeLaPeticion,
    ICurrentUserPermissions permisos,
    LendingAuditEmitter auditoria) : IRequestHandler<GetDepositLineVersionQuery, Result<DepositLineVersionDto>>
{
    public async Task<Result<DepositLineVersionDto>> Handle(GetDepositLineVersionQuery request, CancellationToken ct)
    {
        var linea = await ConsultasDelCatalogo.VisibleAsync(db, alcanceDeLaPeticion, permisos, auditoria, request.LinePublicId, ct);
        if (linea.IsFailure) return Result.Failure<DepositLineVersionDto>(linea.Error);
        var version = await db.DepositLineVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.PublicId == request.VersionPublicId && v.CreditLineId == linea.Value.Id && !v.IsDeleted, ct);
        return version is null
            ? Result.Failure<DepositLineVersionDto>(ErroresDeLineasDeCaptacion.VersionInexistente(LineNature.Deposit))
            : Result.Success(await LectorDeLineasDeCaptacion.DtoDeCaptacionAsync(db, version, ct));
    }
}

/// <summary>Una versión de aportes completa (api.md §3, <c>GET /{id}/contribution-versions/{versionId}</c>). (nuevo)</summary>
public sealed record GetContributionLineVersionQuery(Guid LinePublicId, Guid VersionPublicId) : IRequest<Result<ContributionLineVersionDto>>;

public sealed class GetContributionLineVersionQueryValidator : AbstractValidator<GetContributionLineVersionQuery>
{
    public GetContributionLineVersionQueryValidator()
    {
        RuleFor(x => x.LinePublicId).NotEmpty();
        RuleFor(x => x.VersionPublicId).NotEmpty();
    }
}

public sealed class GetContributionLineVersionQueryHandler(
    IApplicationDbContext db,
    IAlcanceDeCartera alcanceDeLaPeticion,
    ICurrentUserPermissions permisos,
    LendingAuditEmitter auditoria) : IRequestHandler<GetContributionLineVersionQuery, Result<ContributionLineVersionDto>>
{
    public async Task<Result<ContributionLineVersionDto>> Handle(GetContributionLineVersionQuery request, CancellationToken ct)
    {
        var linea = await ConsultasDelCatalogo.VisibleAsync(db, alcanceDeLaPeticion, permisos, auditoria, request.LinePublicId, ct);
        if (linea.IsFailure) return Result.Failure<ContributionLineVersionDto>(linea.Error);
        var version = await db.ContributionLineVersions.AsNoTracking()
            .FirstOrDefaultAsync(v => v.PublicId == request.VersionPublicId && v.CreditLineId == linea.Value.Id && !v.IsDeleted, ct);
        return version is null
            ? Result.Failure<ContributionLineVersionDto>(ErroresDeLineasDeCaptacion.VersionInexistente(LineNature.Contribution))
            : Result.Success(await LectorDeLineasDeCaptacion.DtoDeAportesAsync(db, version, ct));
    }
}

/// <summary>La línea que quien pregunta puede ver: en su alcance y de una naturaleza cuyo <c>View</c> tiene; si no, el 404 de la inexistente. (nuevo)</summary>
internal static class ConsultasDelCatalogo
{
    public static async Task<Result<CreditLine>> VisibleAsync(IApplicationDbContext db, IAlcanceDeCartera alcance, ICurrentUserPermissions permisos,
        LendingAuditEmitter auditoria, Guid publicId, CancellationToken ct)
    {
        var linea = await LectorDeLineasDeCaptacion.LineaEnAlcanceAsync(db, alcance, auditoria, publicId, seguir: false, ct);
        if (linea.IsFailure) return linea;
        return (await PermisosDeLineas.VisiblesAsync(permisos, ct)).Contains(linea.Value.Nature)
            ? linea
            : Result.Failure<CreditLine>(ErroresDeLineasDeCaptacion.LineaInexistente());
    }
}
