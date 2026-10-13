using System.Text.Json;
using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Integration.Queries;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Risk;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>Las marcas de un cierre (api.md §12.3). (nuevo)</summary>
public sealed record MonthlyCloseMarksDto(bool ObligatoryModelUnavailable, bool UnvalidatedParameters, bool IncompleteHistory);

/// <summary>Los totales de un cierre. (nuevo)</summary>
public sealed record MonthlyCloseTotalsDto(int Loans, decimal GrossPortfolio, decimal IndividualImpairment, decimal GeneralImpairment);

/// <summary>Un cierre en la lista (api.md §12.3). (nuevo)</summary>
public sealed record MonthlyCloseListItemDto(
    Guid PublicId,
    string Kind,
    int Year,
    int Month,
    int Version,
    DateOnly CloseDate,
    string Status,
    DateTime InformationCutoffAt,
    MonthlyCloseMarksDto Marks,
    MonthlyCloseTotalsDto Totals,
    DateTime ClosedAt);

/// <summary>Una reapertura del cierre. (nuevo)</summary>
public sealed record MonthlyCloseReopeningDto(Guid PublicId, string Reason, DateTime ReopenedAt, string? ReopenedBy, int ReversalPartCount);

/// <summary>Una parte emitida por el cierre o su reapertura, con su mensaje. (nuevo)</summary>
public sealed record MonthlyClosePartDto(Guid PublicId, string FactType, int? BatchNumber, string Branch, int LoanCount, decimal TotalAmount, Guid? MessagePublicId,
    DateOnly AccountingDate);

/// <summary>El encabezado de un cierre (api.md §12.3, <c>GET /{id}</c>). (nuevo)</summary>
public sealed record MonthlyCloseDto(
    MonthlyCloseListItemDto Close,
    long NationalValuesWatermark,
    JsonElement DocumentsUsed,
    Guid? RiskPolicyVersionPublicId,
    int? RiskPolicyVersionNumber,
    JsonElement ModelBySegment,
    JsonElement? UnvalidatedParameters,
    JsonElement Totals,
    string LinesSha256,
    Guid? PreviousClosePublicId,
    Guid? SupersedesClosePublicId,
    Guid? ProcessRunPublicId,
    IReadOnlyList<MonthlyCloseReopeningDto> Reopenings,
    IReadOnlyList<MonthlyClosePartDto> Parts);

/// <summary>Una línea del cierre por crédito (api.md §12.3, <c>GET /{id}/loans</c>). (nuevo)</summary>
public sealed record MonthlyCloseLoanDto(
    Guid LoanPublicId,
    string LoanNumber,
    RefDto Debtor,
    string Modality,
    string SarcSegment,
    int DaysPastDue,
    string Category,
    string? Subcategory,
    string AccountingCategory,
    string Cause,
    decimal PrincipalBalance,
    decimal ImpairmentBase,
    decimal CollateralAdmissibleValue,
    decimal ImpairmentPercentage,
    decimal TotalImpairment,
    decimal ImpairmentDelta,
    bool Current,
    IReadOnlyList<string> Explanation);

/// <summary>Un cambio de calificación frente al cierre anterior (api.md §12.3, <c>GET /{id}/changes</c>). (nuevo)</summary>
public sealed record MonthlyCloseChangeDto(Guid LoanPublicId, string LoanNumber, RefDto Debtor, string? Previous, string Current, string Cause);

/// <summary>
/// <c>GET /api/lending/risk/monthly-closes?year=&amp;kind=</c> (feature 013, US9, T0709; api.md §12.3, <c>Lending.Ratings.Read</c>): los cierres
/// con su estado derivado de la reapertura, sus marcas y sus totales. Son de toda la cooperativa y no traen datos de deudores. (nuevo)
/// </summary>
public sealed record ListMonthlyClosesQuery(int? Year = null, MonthlyCloseKind? Kind = null) : IRequest<Result<IReadOnlyList<MonthlyCloseListItemDto>>>;

public sealed class ListMonthlyClosesQueryHandler(IApplicationDbContext db) : IRequestHandler<ListMonthlyClosesQuery, Result<IReadOnlyList<MonthlyCloseListItemDto>>>
{
    public async Task<Result<IReadOnlyList<MonthlyCloseListItemDto>>> Handle(ListMonthlyClosesQuery request, CancellationToken ct)
    {
        var consulta = db.MonthlyCloses.AsNoTracking().Where(c => !c.IsDeleted);
        if (request.Year is { } anio) consulta = consulta.Where(c => c.PeriodYear == anio);
        if (request.Kind is { } clase) consulta = consulta.Where(c => c.Kind == clase);
        var cierres = await consulta.OrderByDescending(c => c.CloseDate).ThenByDescending(c => c.Version).ToListAsync(ct);
        var reabiertos = await ConsultasDeCierres.ReabiertosAsync(db, cierres.Select(c => c.Id).ToList(), ct);
        return Result.Success<IReadOnlyList<MonthlyCloseListItemDto>>(cierres.Select(c => ConsultasDeCierres.Item(c, reabiertos.Contains(c.Id))).ToList());
    }
}

/// <summary>
/// <c>GET /api/lending/risk/monthly-closes/{id}</c> (feature 013, US9, T0709; <c>Lending.Ratings.Read</c>): el encabezado con R, la marca de agua,
/// los documentos con su SHA-256, la política y el modelo por segmento, las reaperturas y las partes emitidas (del cierre y de sus reversos). Sin
/// datos de deudores. Inexistente: <c>Lending.MonthlyClose.NotFound</c>. (nuevo)
/// </summary>
public sealed record GetMonthlyCloseQuery(Guid MonthlyClosePublicId) : IRequest<Result<MonthlyCloseDto>>;

public sealed class GetMonthlyCloseQueryHandler(IApplicationDbContext db) : IRequestHandler<GetMonthlyCloseQuery, Result<MonthlyCloseDto>>
{
    public async Task<Result<MonthlyCloseDto>> Handle(GetMonthlyCloseQuery request, CancellationToken ct)
    {
        var c = await db.MonthlyCloses.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.MonthlyClosePublicId && !x.IsDeleted, ct);
        if (c is null) return Result.Failure<MonthlyCloseDto>(ErroresDeRiesgo.CierreInexistente());
        var reaperturas = await (from r in db.MonthlyCloseReopenings.AsNoTracking()
                                 join u in db.Users.AsNoTracking().IgnoreQueryFilters() on r.ReopenedByUserId equals u.Id into usuarios
                                 from u in usuarios.DefaultIfEmpty()
                                 where r.MonthlyCloseId == c.Id && !r.IsDeleted
                                 select new MonthlyCloseReopeningDto(r.PublicId, r.Reason, r.ReopenedAt, u == null ? null : u.Username, r.ReversalPartCount))
            .ToListAsync(ct);
        var idsDeReapertura = await db.MonthlyCloseReopenings.AsNoTracking().Where(r => r.MonthlyCloseId == c.Id).Select(r => r.Id).ToListAsync(ct);
        var partes = await (from p in db.AccountingFactParts.AsNoTracking()
                            join b in db.Branches.AsNoTracking().IgnoreQueryFilters() on p.BranchId equals b.Id
                            where p.MonthlyCloseId == c.Id || (p.MonthlyCloseReopeningId != null && idsDeReapertura.Contains(p.MonthlyCloseReopeningId.Value))
                            orderby p.Id
                            select new MonthlyClosePartDto(p.PublicId, p.FactType, p.BatchNumber, b.Name, p.LoanCount, p.TotalAmount, p.MessagePublicId, p.AccountingDate))
            .ToListAsync(ct);
        var politica = c.RiskPolicyVersionId is { } pid
            ? await db.RiskPolicyVersions.AsNoTracking().Where(v => v.Id == pid).Select(v => new { v.PublicId, v.VersionNumber }).FirstOrDefaultAsync(ct)
            : null;
        var refs = new[] { c.PreviousCloseId ?? 0, c.SupersedesCloseId ?? 0 };
        var otros = await db.MonthlyCloses.AsNoTracking().Where(x => refs.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.PublicId, ct);
        Guid? corrida = c.ProcessRunId is { } run
            ? await db.LendingProcessRuns.AsNoTracking().Where(r => r.Id == run).Select(r => (Guid?)r.PublicId).FirstOrDefaultAsync(ct)
            : null;
        return Result.Success(new MonthlyCloseDto(ConsultasDeCierres.Item(c, reaperturas.Count > 0), c.NationalValuesWatermark,
            ConsultasDeCierres.Json(c.DocumentsUsedJson), politica?.PublicId, politica?.VersionNumber, ConsultasDeCierres.Json(c.ModelBySegmentJson),
            c.UnvalidatedParametersJson is null ? null : ConsultasDeCierres.Json(c.UnvalidatedParametersJson), ConsultasDeCierres.Json(c.TotalsJson),
            c.LinesSha256, c.PreviousCloseId is { } previo ? otros.GetValueOrDefault(previo) : null,
            c.SupersedesCloseId is { } reemplazado ? otros.GetValueOrDefault(reemplazado) : null,
            corrida, reaperturas, partes));
    }
}

/// <summary>
/// <c>GET /api/lending/risk/monthly-closes/{id}/loans?category=&amp;page=</c> (feature 013, US9, T0709; <c>Lending.Ratings.Read</c>): las líneas del
/// cierre por crédito, filtradas por el alcance de oficina y línea (las de fuera no aparecen), con su vigencia derivada de la reapertura. (nuevo)
/// </summary>
public sealed record ListMonthlyCloseLoansQuery(Guid MonthlyClosePublicId, RatingCategory? Category = null, int Page = 1, int PageSize = 50)
    : IRequest<Result<PagedResult<MonthlyCloseLoanDto>>>;

public sealed class ListMonthlyCloseLoansQueryValidator : AbstractValidator<ListMonthlyCloseLoansQuery>
{
    public ListMonthlyCloseLoansQueryValidator() => RuleFor(x => x.Category).IsInEnum().When(x => x.Category is not null);
}

public sealed class ListMonthlyCloseLoansQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<ListMonthlyCloseLoansQuery, Result<PagedResult<MonthlyCloseLoanDto>>>
{
    public async Task<Result<PagedResult<MonthlyCloseLoanDto>>> Handle(ListMonthlyCloseLoansQuery request, CancellationToken ct)
    {
        var c = await db.MonthlyCloses.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.MonthlyClosePublicId && !x.IsDeleted, ct);
        if (c is null) return Result.Failure<PagedResult<MonthlyCloseLoanDto>>(ErroresDeRiesgo.CierreInexistente());
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var vigente = !await db.MonthlyCloseReopenings.AsNoTracking().AnyAsync(r => r.MonthlyCloseId == c.Id && !r.IsDeleted, ct);
        var consulta = CategoriaContableDeCartera.LineasDe(db, c).PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId);
        if (request.Category is { } categoria) consulta = consulta.Where(l => l.FinalRating == categoria);
        var pagina = new PageRequest(request.Page, request.PageSize);
        var total = await consulta.CountAsync(ct);
        var lineas = await consulta.OrderBy(l => l.LoanId).Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToListAsync(ct);
        var (creditos, personas) = await ConsultasDeCierres.ReferenciasAsync(db, lineas, ct);
        var items = lineas.Select(l => new MonthlyCloseLoanDto(creditos[l.LoanId].PublicId, creditos[l.LoanId].Number, personas[l.PersonId], l.ModalityCode,
            l.SarcSegmentCode, l.DaysPastDueAtClose, l.FinalRating.ToString(), l.FinalSubcategory == RatingSubcategory.None ? null : l.FinalSubcategory.ToString(),
            l.AccountingRating.ToString(), l.RatingCause.ToString(), l.PrincipalBalance, l.ImpairmentBase, l.CollateralAdmissibleValue, l.ImpairmentPercentage,
            l.TotalImpairment, l.ImpairmentDelta, vigente, JsonSerializer.Deserialize<List<string>>(l.ExplanationJson) ?? [])).ToList();
        return Result.Success(new PagedResult<MonthlyCloseLoanDto>(items, pagina.SafePage, pagina.SafePageSize, total));
    }
}

/// <summary>
/// <c>GET /api/lending/risk/monthly-closes/{id}/changes?page=</c> (feature 013, US9, T0709; FR-107, US9-3; <c>Lending.Ratings.Read</c>): el reporte de
/// cambios, derivado al leer: cada crédito cuya calificación cambió frente a su línea del cierre anterior (el primero, frente al base) con la
/// anterior, la nueva y la causa; los nuevos aparecen sin anterior. Filtrado por el alcance de oficina y línea. (nuevo)
/// </summary>
public sealed record GetMonthlyCloseChangesQuery(Guid MonthlyClosePublicId, int Page = 1, int PageSize = 50) : IRequest<Result<PagedResult<MonthlyCloseChangeDto>>>;

public sealed class GetMonthlyCloseChangesQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<GetMonthlyCloseChangesQuery, Result<PagedResult<MonthlyCloseChangeDto>>>
{
    public async Task<Result<PagedResult<MonthlyCloseChangeDto>>> Handle(GetMonthlyCloseChangesQuery request, CancellationToken ct)
    {
        var c = await db.MonthlyCloses.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.MonthlyClosePublicId && !x.IsDeleted, ct);
        if (c is null) return Result.Failure<PagedResult<MonthlyCloseChangeDto>>(ErroresDeRiesgo.CierreInexistente());
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var lineas = await CategoriaContableDeCartera.LineasDe(db, c).PorOficinaYLinea(alcance, l => l.BranchId, l => l.CreditLineId).ToListAsync(ct);
        var anterior = c.PreviousCloseId is { } pid ? await db.MonthlyCloses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == pid, ct) : null;
        var ids = lineas.Select(l => l.LoanId).ToList();
        var previas = anterior is null
            ? new Dictionary<int, MonthlyCloseLoan>()
            : await CategoriaContableDeCartera.LineasDe(db, anterior).Where(l => ids.Contains(l.LoanId)).ToDictionaryAsync(l => l.LoanId, ct);
        static string Texto(RatingCategory cat, RatingSubcategory sub) => sub == RatingSubcategory.None ? cat.ToString() : sub.ToString();
        var cambios = lineas.Where(l => !previas.TryGetValue(l.LoanId, out var p) || p.FinalRating != l.FinalRating || p.FinalSubcategory != l.FinalSubcategory)
            .OrderBy(l => l.LoanId).ToList();
        var pagina = new PageRequest(request.Page, request.PageSize);
        var vista = cambios.Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToList();
        var (creditos, personas) = await ConsultasDeCierres.ReferenciasAsync(db, vista, ct);
        var items = vista.Select(l => new MonthlyCloseChangeDto(creditos[l.LoanId].PublicId, creditos[l.LoanId].Number, personas[l.PersonId],
            previas.TryGetValue(l.LoanId, out var p) ? Texto(p.FinalRating, p.FinalSubcategory) : null, Texto(l.FinalRating, l.FinalSubcategory),
            l.RatingCause.ToString())).ToList();
        return Result.Success(new PagedResult<MonthlyCloseChangeDto>(items, pagina.SafePage, pagina.SafePageSize, cambios.Count));
    }
}

/// <summary>
/// <c>GET /api/lending/risk/monthly-closes/{id}/reconciliation</c> (feature 013, US9, T0709; T58, SC-006; <c>Lending.Reconciliation.View</c>): el
/// cuadre del cierre con Contabilidad a su fecha de cierre, por <c>ConciliacionContableDeCartera</c> con el alcance de oficinas de quien pregunta
/// (la misma consulta de <c>/cartera/conciliacion-contable</c>). (nuevo)
/// </summary>
public sealed record GetMonthlyCloseReconciliationQuery(Guid MonthlyClosePublicId) : IRequest<Result<ConciliacionContableDto>>;

public sealed class GetMonthlyCloseReconciliationQueryHandler(IApplicationDbContext db, ConciliacionContableDeCartera conciliacion, IAlcanceDeCartera alcanceDeCartera)
    : IRequestHandler<GetMonthlyCloseReconciliationQuery, Result<ConciliacionContableDto>>
{
    public async Task<Result<ConciliacionContableDto>> Handle(GetMonthlyCloseReconciliationQuery request, CancellationToken ct)
    {
        var c = await db.MonthlyCloses.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.MonthlyClosePublicId && !x.IsDeleted, ct);
        if (c is null) return Result.Failure<ConciliacionContableDto>(ErroresDeRiesgo.CierreInexistente());
        return await new GetLendingAccountingReconciliationQueryHandler(db, conciliacion, alcanceDeCartera)
            .Handle(new GetLendingAccountingReconciliationQuery(c.CloseDate), ct);
    }
}

/// <summary>Lo que comparten las consultas de los cierres. (nuevo)</summary>
public static class ConsultasDeCierres
{
    public static async Task<HashSet<int>> ReabiertosAsync(IApplicationDbContext db, IReadOnlyCollection<int> ids, CancellationToken ct) =>
        (await db.MonthlyCloseReopenings.AsNoTracking().Where(r => ids.Contains(r.MonthlyCloseId) && !r.IsDeleted).Select(r => r.MonthlyCloseId).ToListAsync(ct))
        .ToHashSet();

    public static MonthlyCloseListItemDto Item(MonthlyClose c, bool reabierto) => new(c.PublicId, c.Kind.ToString(), c.PeriodYear, c.PeriodMonth, c.Version,
        c.CloseDate, MonthlyClose.EstadoCon(reabierto).ToString(), c.InformationCutoffAt,
        new MonthlyCloseMarksDto(c.ObligatoryModelUnavailable, c.UnvalidatedParameters, c.IncompleteHistory),
        new MonthlyCloseTotalsDto(c.LoanCount, c.TotalPrincipal, c.TotalIndividualImpairment, c.TotalGeneralImpairment), c.ClosedAt);

    public static JsonElement Json(string texto)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(texto) ? "null" : texto);
        return doc.RootElement.Clone();
    }

    public static async Task<(Dictionary<int, (Guid PublicId, string Number)> Creditos, Dictionary<int, RefDto> Personas)> ReferenciasAsync(
        IApplicationDbContext db, IReadOnlyCollection<MonthlyCloseLoan> lineas, CancellationToken ct)
    {
        var ids = lineas.Select(l => l.LoanId).Distinct().ToList();
        var creditos = await db.Loans.AsNoTracking().IgnoreQueryFilters().Where(l => ids.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => (l.PublicId, l.Number), ct);
        var personaIds = lineas.Select(l => l.PersonId).Distinct().ToList();
        var personas = (await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personaIds.Contains(p.Id)).ToListAsync(ct))
            .ToDictionary(p => p.Id, p => new RefDto(p.PublicId, ReferenciasDeGobierno.Nombre(p)));
        foreach (var id in personaIds.Where(id => !personas.ContainsKey(id))) personas[id] = new RefDto(Guid.Empty, string.Empty);
        return (creditos, personas);
    }
}
