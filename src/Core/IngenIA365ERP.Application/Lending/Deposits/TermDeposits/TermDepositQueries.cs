using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.TermDeposits;

/// <summary>La forma de respuesta del CDAT (api.md §5, <c>TermDepositDto</c>): la cuenta, la versión vigente con su gracia, la propuesta, el historial y los títulos. (nuevo)</summary>
public static class RespuestasDeCdat
{
    public static async Task<TermDepositDto> DtoAsync(IApplicationDbContext db, LectorDeCdat lector, MemberAccount cuenta, DateOnly hoy, CancellationToken ct)
    {
        var cuentaDto = await LectorDeCuentas.DtoAsync(db, cuenta, hoy, ct);
        var leido = await lector.LeerAsync(cuenta, ct);
        var historia = new List<TermDepositTermsDto>();
        foreach (var v in leido.Versiones) historia.Add(await TerminosAsync(db, lector, v, cuenta.Number, ct));
        var propuesta = leido.Propuesta is { } p
            ? new TermDepositProposalDto(p.Monto, p.PlazoDias, p.Periodicidad, p.Modalidad, p.TasaDeTabla, p.Negociacion?.Margen, p.TasaAplicada, p.Fondeo,
                p.Instruccion, p.FechaPedida)
            : null;
        var solicitud = await db.NegotiatedRateRequests.AsNoTracking()
            .Where(r => r.MemberAccountId == cuenta.Id && r.Status == NegotiatedRateRequestStatus.PendingDecision && !r.IsDeleted).FirstOrDefaultAsync(ct);
        var instruccion = InstruccionDeCdat.Leer(cuenta.MaturityInstructionJson)?.Instruccion ?? leido.Vigente?.MaturityInstruction ?? leido.Propuesta?.Instruccion;
        return new TermDepositDto(cuentaDto, historia.LastOrDefault(), propuesta, historia, await TitulosAsync(db, cuenta.Id, ct), cuenta.Status,
            solicitud is null ? null : (await NegotiatedRates.SolicitudesDeTasaNegociada.DtosAsync(db, [solicitud], ct))[0], instruccion);
    }

    public static async Task<TermDepositTermsDto> TerminosAsync(IApplicationDbContext db, LectorDeCdat lector, MemberAccountTermsVersion v, string numero,
        CancellationToken ct)
    {
        var inicio = v.StartDate ?? v.EffectiveValueDate;
        var vencimiento = v.MaturityDate ?? inicio;
        var calendario = await lector.CalendarioAsync(inicio, vencimiento.AddDays((v.GraceBusinessDays ?? 0) * (int)RepaymentPeriodicity.Weekly), ct);
        var gracia = PlanDeCdat.FinDeLaGracia(vencimiento, v.GraceBusinessDays ?? 0, calendario);
        TermDepositNegotiatedDto? negociada = null;
        if (v.NegotiatedSpread is { } margen)
        {
            var regla = v.NegotiatedRateRuleId is { } r
                ? await db.NegotiatedRateRules.AsNoTracking().Where(x => x.Id == r).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct)
                : null;
            var aprobador = v.NegotiationApprovedByUserId is { } u
                ? await db.Users.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == u).Select(x => x.Email).FirstOrDefaultAsync(ct)
                : null;
            negociada = new TermDepositNegotiatedDto(margen, regla, aprobador, v.NegotiationReason);
        }
        return new TermDepositTermsDto(v.VersionNumber, MemberAccountTermsVersion.NumeroDeTitulo(numero, v.VersionNumber), v.Cause, v.PrincipalAmount, v.TermDays ?? 0,
            inicio, vencimiento, v.PaymentDate ?? vencimiento, v.AppliedAnnualRate, v.TableAnnualRate, negociada, v.InterestPeriodicity ?? RepaymentPeriodicity.SinglePayment,
            v.InterestTiming ?? InterestTiming.Arrears, v.InterestDestination, v.MaturityInstruction, gracia, v.IssueTermBand, v.RolloverCount, v.RulesVersion);
    }

    /// <summary>
    /// Los títulos del CDAT (US13, T196; documentos.md §2.5): los documentos <c>TermDepositTitle</c> que lo nombran, con la versión de condiciones que titulan
    /// y su estado calculado (vigente, reemplazado, liquidado o anulado).
    /// </summary>
    public static async Task<IReadOnlyList<TermDepositTitleDto>> TitulosAsync(IApplicationDbContext db, int cuentaId, CancellationToken ct)
    {
        var titulos = await (from a in db.IssuedDocumentAccounts.AsNoTracking()
                             join d in db.IssuedLendingDocuments.AsNoTracking() on a.IssuedDocumentId equals d.Id
                             join v in db.MemberAccountTermsVersions.AsNoTracking() on a.TermsVersionId equals (int?)v.Id into vs
                             from v in vs.DefaultIfEmpty()
                             where a.MemberAccountId == cuentaId && d.Kind == IssuedDocumentKind.TermDepositTitle && !d.IsDeleted
                             orderby d.Id
                             select new { d.Id, d.PublicId, d.Number, Version = v == null ? (int?)null : v.VersionNumber, d.IssuedAt, d.FinalityUntil })
            .ToListAsync(ct);
        if (titulos.Count == 0) return [];
        var ids = titulos.Select(t => t.Id).ToList();
        var eventos = (await db.IssuedDocumentEvents.AsNoTracking().Where(e => ids.Contains(e.IssuedDocumentId)).Select(e => new { e.IssuedDocumentId, e.Kind })
            .ToListAsync(ct)).ToLookup(e => e.IssuedDocumentId, e => e.Kind);
        return titulos.Select(t => new TermDepositTitleDto(t.PublicId, t.Number, t.Version,
            Domain.Lending.Documents.EstadoDeDocumentoEmitido.Calcular(Domain.Lending.Documents.EventosDeEstado.De(eventos[t.Id]), t.FinalityUntil,
                DateOnly.MaxValue).ToString(), t.IssuedAt)).ToList();
    }

    /// <summary>El CDAT por su PublicId dentro del alcance (404 <c>Lending.TermDeposit.NotFound</c> si no existe, está fuera o no es un CDAT).</summary>
    public static async Task<Result<MemberAccount>> CdatEnAlcanceAsync(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria,
        Guid publicId, bool seguir, CancellationToken ct)
    {
        var cuenta = await LectorDeCuentas.CuentaEnAlcanceAsync(db, alcance, auditoria, publicId, seguir, ct);
        return cuenta.IsSuccess && cuenta.Value.DepositClass == DepositClass.TermDeposit ? cuenta : Result.Failure<MemberAccount>(ErroresDeCdat.NoEncontrado());
    }
}

/// <summary>Los CDAT (api.md §5, <c>GET /api/lending/term-deposits</c>, <c>Lending.Accounts.Read</c>), en el alcance y el vencimiento más próximo primero. (nuevo)</summary>
public sealed record ListTermDepositsQuery(
    MemberAccountStatus? Status = null,
    Guid? LinePublicId = null,
    Guid? OfficePublicId = null,
    DateOnly? MaturityFrom = null,
    DateOnly? MaturityTo = null,
    IssueTermBand? IssueTermBand = null,
    Guid? PersonPublicId = null,
    bool? Negotiated = null,
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<TermDepositListItemDto>>>;

public sealed class ListTermDepositsQueryValidator : AbstractValidator<ListTermDepositsQuery>
{
    public ListTermDepositsQueryValidator()
    {
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status is not null);
        RuleFor(x => x.IssueTermBand).IsInEnum().When(x => x.IssueTermBand is not null);
        RuleFor(x => x).Must(x => x.MaturityFrom is null || x.MaturityTo is null || x.MaturityFrom <= x.MaturityTo).WithMessage("El rango de vencimientos va de menor a mayor.");
    }
}

public sealed class ListTermDepositsQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, LendingAuditEmitter auditoria)
    : IRequestHandler<ListTermDepositsQuery, Result<PagedResult<TermDepositListItemDto>>>
{
    public async Task<Result<PagedResult<TermDepositListItemDto>>> Handle(ListTermDepositsQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var consulta = db.MemberAccounts.AsNoTracking().Where(a => !a.IsDeleted && a.DepositClass == DepositClass.TermDeposit)
            .PorOficinaYLinea(alcance, a => a.BranchId, a => a.CreditLineId);
        if (request.Status is { } estado) consulta = consulta.Where(a => a.Status == estado);
        if (request.LinePublicId is { } linea) consulta = consulta.Where(a => db.CreditLines.Any(l => l.Id == a.CreditLineId && l.PublicId == linea));
        if (request.OfficePublicId is { } oficina) consulta = consulta.Where(a => db.Branches.Any(b => b.Id == a.BranchId && b.PublicId == oficina));
        if (request.PersonPublicId is { } persona) consulta = consulta.Where(a => db.People.Any(p => p.Id == a.HolderPersonId && p.PublicId == persona));
        var filas = await consulta.ToListAsync(ct);
        var ids = filas.Select(a => a.Id).ToList();
        var versiones = (await db.MemberAccountTermsVersions.AsNoTracking().Where(v => ids.Contains(v.MemberAccountId) && !v.IsDeleted).ToListAsync(ct))
            .GroupBy(v => v.MemberAccountId).ToDictionary(g => g.Key, g => g.MaxBy(v => v.VersionNumber)!);
        IEnumerable<MemberAccount> lista = filas;
        if (request.MaturityFrom is { } desde) lista = lista.Where(a => versiones.TryGetValue(a.Id, out var v) && v.MaturityDate >= desde);
        if (request.MaturityTo is { } hasta) lista = lista.Where(a => versiones.TryGetValue(a.Id, out var v) && v.MaturityDate <= hasta);
        if (request.IssueTermBand is { } plazo) lista = lista.Where(a => versiones.TryGetValue(a.Id, out var v) && v.IssueTermBand == plazo);
        if (request.Negotiated is { } negociado) lista = lista.Where(a => versiones.TryGetValue(a.Id, out var v) && (v.NegotiatedSpread is not null) == negociado);
        var ordenadas = lista.OrderBy(a => versiones.TryGetValue(a.Id, out var v) ? v.MaturityDate ?? DateOnly.MaxValue : DateOnly.MaxValue).ThenBy(a => a.Id).ToList();
        var pagina = new PageRequest(request.Page, request.PageSize);
        var visibles = ordenadas.Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize).ToList();

        var personas = await LectorDeCuentas.PersonasAsync(db, visibles.Select(a => a.HolderPersonId), ct);
        var idsDeLinea = visibles.Select(a => a.CreditLineId).Distinct().ToList();
        var lineas = await db.CreditLines.AsNoTracking().IgnoreQueryFilters().Where(l => idsDeLinea.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => new Lines.LendingLineRefDto(l.PublicId, l.Code, l.Name), ct);
        var items = visibles.Select(a =>
        {
            versiones.TryGetValue(a.Id, out var v);
            var propuesta = PropuestaDeCdat.Leer(a.PendingTermsJson);
            return new TermDepositListItemDto(a.PublicId, a.Number, v is null ? null : MemberAccountTermsVersion.NumeroDeTitulo(a.Number, v.VersionNumber),
                personas[a.HolderPersonId], lineas[a.CreditLineId], v?.PrincipalAmount ?? propuesta?.Monto ?? 0m, v?.TermDays ?? propuesta?.PlazoDias,
                v?.AppliedAnnualRate ?? propuesta?.TasaAplicada, v?.NegotiatedSpread ?? propuesta?.Negociacion?.Margen, v?.IssueTermBand, a.OpenedOn, v?.MaturityDate,
                a.Status, InstruccionDeCdat.Leer(a.MaturityInstructionJson)?.Instruccion ?? v?.MaturityInstruction ?? propuesta?.Instruccion, v?.RolloverCount ?? 0);
        }).ToList();
        if (request.PersonPublicId is { } titular)
            await auditoria.EmitirSaldosDePersonaVistosAsync(titular, "term-deposits-list", new { request.Status, page = pagina.SafePage, rows = items.Count }, ct);
        return Result.Success(new PagedResult<TermDepositListItemDto>(items, pagina.SafePage, pagina.SafePageSize, ordenadas.Count));
    }
}

/// <summary>Un CDAT (api.md §5, <c>GET /api/lending/term-deposits/{id}</c>, <c>Lending.Accounts.Read</c>); lectura sensible auditada. (nuevo)</summary>
public sealed record GetTermDepositQuery(Guid PublicId) : IRequest<Result<TermDepositDto>>;

public sealed class GetTermDepositQueryHandler(IApplicationDbContext db, LectorDeCdat lector, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria,
    IDateTimeService reloj) : IRequestHandler<GetTermDepositQuery, Result<TermDepositDto>>
{
    public async Task<Result<TermDepositDto>> Handle(GetTermDepositQuery request, CancellationToken ct)
    {
        var cuenta = await RespuestasDeCdat.CdatEnAlcanceAsync(db, alcance, auditoria, request.PublicId, seguir: false, ct);
        if (cuenta.IsFailure) return Result.Failure<TermDepositDto>(cuenta.Error);
        var dto = await RespuestasDeCdat.DtoAsync(db, lector, cuenta.Value, reloj.HoyLocal, ct);
        await auditoria.EmitirSaldoDeCuentaVistoAsync(cuenta.Value.PublicId, "term-deposit-detail", new { version = dto.CurrentTerms?.Version }, ct);
        return Result.Success(dto);
    }
}

/// <summary>
/// El plan de una versión del CDAT (api.md §5, <c>GET /{id}/schedule?version=</c>, <c>Lending.Accounts.Read</c>): cada período con su interés y, si ya se
/// pagó, la retención y el neto reales, el día y la operación. Sin versión, la vigente. Lectura sensible auditada. (nuevo)
/// </summary>
public sealed record GetTermDepositScheduleQuery(Guid PublicId, int? Version = null) : IRequest<Result<IReadOnlyList<TermDepositScheduleLineDto>>>;

public sealed class GetTermDepositScheduleQueryHandler(IApplicationDbContext db, LectorDeCdat lector, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria)
    : IRequestHandler<GetTermDepositScheduleQuery, Result<IReadOnlyList<TermDepositScheduleLineDto>>>
{
    public async Task<Result<IReadOnlyList<TermDepositScheduleLineDto>>> Handle(GetTermDepositScheduleQuery request, CancellationToken ct)
    {
        var cuenta = await RespuestasDeCdat.CdatEnAlcanceAsync(db, alcance, auditoria, request.PublicId, seguir: false, ct);
        if (cuenta.IsFailure) return Result.Failure<IReadOnlyList<TermDepositScheduleLineDto>>(cuenta.Error);
        var leido = await lector.LeerAsync(cuenta.Value, ct);
        var version = request.Version is { } n ? leido.Versiones.FirstOrDefault(v => v.VersionNumber == n) : leido.Vigente;
        if (version is null) return Result.Success<IReadOnlyList<TermDepositScheduleLineDto>>([]);
        // Lo pagado: la fila InterestPaid de cada período (con su operación viva) y, de esa operación, lo que fue a la memoria de la retención.
        var pagos = await (from t in db.MemberAccountTransactions.AsNoTracking()
                           join o in db.MemberAccountOperations.AsNoTracking() on t.OperationId equals o.Id
                           where t.MemberAccountId == cuenta.Value.Id && t.TermsVersionId == version.Id && t.SchedulePeriodNumber != null && !t.IsDeleted
                                 && o.ReversedByOperationId == null && t.Kind == MemberAccountTransactionKind.InterestPaid
                           select new { Periodo = t.SchedulePeriodNumber!.Value, t.Amount, OperacionId = o.Id, o.ValueDate, o.PublicId }).ToListAsync(ct);
        var operaciones = pagos.Select(p => (long?)p.OperacionId).Distinct().ToList();
        var retenido = (await db.MemberAccountTransactions.AsNoTracking()
                .Where(t => operaciones.Contains(t.OperationId) && t.MemberAccountId == cuenta.Value.Id && t.Concept == MemberAccountConcept.Withholding && !t.IsDeleted)
                .Select(t => new { t.OperationId, t.Amount }).ToListAsync(ct))
            .GroupBy(t => t.OperationId!.Value).ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
        var lineas = leido.PlanDe(version).Select(p =>
        {
            var pago = pagos.FirstOrDefault(x => x.Periodo == p.PeriodNumber);
            var retencion = pago is null ? 0m : retenido.GetValueOrDefault(pago.OperacionId);
            return new TermDepositScheduleLineDto(p.PeriodNumber, p.PeriodStart, p.PeriodEnd, p.PaymentDate, p.Days, p.PeriodRate, p.Interest, retencion,
                pago is null ? p.Interest : -pagos.Where(x => x.Periodo == p.PeriodNumber).Sum(x => x.Amount), pago?.ValueDate, pago?.PublicId);
        }).ToList();
        await auditoria.EmitirSaldoDeCuentaVistoAsync(cuenta.Value.PublicId, "term-deposit-schedule", new { version = version.VersionNumber }, ct);
        return Result.Success<IReadOnlyList<TermDepositScheduleLineDto>>(lineas);
    }
}

/// <summary>Los títulos del CDAT (api.md §5, <c>GET /{id}/titles</c>): los documentos <c>TermDepositTitle</c> de la cadena; la emisión llega con US13. (nuevo)</summary>
public sealed record ListTermDepositTitlesQuery(Guid PublicId) : IRequest<Result<IReadOnlyList<TermDepositTitleDto>>>;

public sealed class ListTermDepositTitlesQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcance, LendingAuditEmitter auditoria)
    : IRequestHandler<ListTermDepositTitlesQuery, Result<IReadOnlyList<TermDepositTitleDto>>>
{
    public async Task<Result<IReadOnlyList<TermDepositTitleDto>>> Handle(ListTermDepositTitlesQuery request, CancellationToken ct)
    {
        var cuenta = await RespuestasDeCdat.CdatEnAlcanceAsync(db, alcance, auditoria, request.PublicId, seguir: false, ct);
        return cuenta.IsFailure
            ? Result.Failure<IReadOnlyList<TermDepositTitleDto>>(cuenta.Error)
            : Result.Success(await RespuestasDeCdat.TitulosAsync(db, cuenta.Value.Id, ct));
    }
}

/// <summary>
/// El calendario de vencimientos (api.md §5, <c>GET /maturity-calendar?from=&amp;to=&amp;office=</c>, <c>Lending.Accounts.View</c>): por día y plazo de emisión,
/// cuántos CDAT vivos vencen y su capital, sin titulares, en el alcance. (nuevo)
/// </summary>
public sealed record GetTermDepositMaturityCalendarQuery(DateOnly From, DateOnly To, Guid? OfficePublicId = null) : IRequest<Result<IReadOnlyList<TermDepositMaturityDayDto>>>;

public sealed class GetTermDepositMaturityCalendarQueryValidator : AbstractValidator<GetTermDepositMaturityCalendarQuery>
{
    public GetTermDepositMaturityCalendarQueryValidator() => RuleFor(x => x).Must(x => x.From <= x.To).WithMessage("El rango va de menor a mayor.");
}

public sealed class GetTermDepositMaturityCalendarQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion)
    : IRequestHandler<GetTermDepositMaturityCalendarQuery, Result<IReadOnlyList<TermDepositMaturityDayDto>>>
{
    public async Task<Result<IReadOnlyList<TermDepositMaturityDayDto>>> Handle(GetTermDepositMaturityCalendarQuery request, CancellationToken ct)
    {
        var alcance = await alcanceDeLaPeticion.ObtenerAsync(ct);
        var cuentas = db.MemberAccounts.AsNoTracking()
            .Where(a => !a.IsDeleted && a.DepositClass == DepositClass.TermDeposit && a.Status != MemberAccountStatus.Closed && a.Status != MemberAccountStatus.Opening)
            .PorOficinaYLinea(alcance, a => a.BranchId, a => a.CreditLineId);
        if (request.OfficePublicId is { } oficina) cuentas = cuentas.Where(a => db.Branches.Any(b => b.Id == a.BranchId && b.PublicId == oficina));
        var ids = await cuentas.Select(a => a.Id).ToListAsync(ct);
        var vigentes = (await db.MemberAccountTermsVersions.AsNoTracking().Where(v => ids.Contains(v.MemberAccountId) && !v.IsDeleted).ToListAsync(ct))
            .GroupBy(v => v.MemberAccountId).Select(g => g.MaxBy(v => v.VersionNumber)!)
            .Where(v => v.MaturityDate >= request.From && v.MaturityDate <= request.To).ToList();
        return Result.Success<IReadOnlyList<TermDepositMaturityDayDto>>(vigentes
            .GroupBy(v => (v.MaturityDate!.Value, v.IssueTermBand ?? PlanDeCdat.PlazoDeEmision(Math.Max(1, v.TermDays ?? 1))))
            .OrderBy(g => g.Key.Value).ThenBy(g => g.Key.Item2)
            .Select(g => new TermDepositMaturityDayDto(g.Key.Value, g.Key.Item2, g.Count(), g.Sum(v => v.PrincipalAmount))).ToList());
    }
}
