using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Attachments.UploadAttachment;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Application.Common.Integration.Receipts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Treasury.Accounting;
using IngenIA365ERP.Application.Treasury.Checkbooks;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Reconciliation;
using IngenIA365ERP.Domain.Entities.Treasury.Statements;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Treasury.Conciliacion;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ConciliacionBancaria = IngenIA365ERP.Domain.Entities.Treasury.Reconciliation.Reconciliation;

namespace IngenIA365ERP.Application.Treasury.Reconciliation;

// Feature 015, US25 (T0278; FR-131, FR-209, FR-216, FR-217; contracts/api.md §12): la conciliación bancaria mínima, la única de la cooperativa (E-04). Por cuenta y mes:
// abrir, cruzar solo y a mano, deshacer un cruce de una abierta, registrar partidas conciliatorias (el abono sin registro pasa a Recaudo como partida por identificar),
// cerrar con diferencia cero y su informe, y reabrir como versión nueva. El conciliador no preparó ni aprobó pagos de la cuenta en el mes (FR-217).

/// <summary>Un cruce de la conciliación (las filas de un mismo grupo se muestran juntas). (nuevo)</summary>
public sealed record ReconciliationMatchDto(Guid PublicId, Guid GroupKey, Guid StatementLinePublicId, string RecordKind, Guid RecordPublicId, string? RecordNumber,
    decimal Amount, string Mode, string Rule, DateTime MatchedAt, string MatchedBy);

/// <summary>Una partida conciliatoria con su antigüedad en días a fin de mes. (nuevo)</summary>
public sealed record ReconcilingItemDto(Guid PublicId, string Kind, decimal Amount, decimal SignedAmount, DateOnly Since, int AgeDays, Guid? SourcePublicId,
    Guid? StatementLinePublicId, Guid? UnidentifiedItemPublicId, Guid? AlertPublicId, string? Note);

/// <summary>Un registro de los libros que falta por cruzar. (nuevo)</summary>
public sealed record AvailableRecordDto(string Kind, Guid PublicId, string Number, DateOnly Date, string Direction, decimal Amount, string? Reference, string Source);

/// <summary>Una conciliación: su cuenta y mes, la versión, los saldos y la diferencia; en el detalle, cruces, partidas, líneas y registros por cruzar. (nuevo)</summary>
public sealed record ReconciliationDto(
    Guid PublicId,
    Guid BankAccountPublicId,
    string BankAccountCode,
    int Year,
    int Month,
    int Version,
    string Status,
    decimal StatementBalance,
    decimal LedgerBalance,
    decimal ReconcilingItemsTotal,
    decimal Difference,
    string Conciliator,
    DateTime? ClosedAt,
    Guid? ReportAttachmentPublicId,
    string? ReopenReason,
    IReadOnlyList<ReconciliationMatchDto>? Matches = null,
    IReadOnlyList<ReconcilingItemDto>? Items = null,
    IReadOnlyList<Statements.BankStatementLineDto>? OpenLines = null,
    IReadOnlyList<AvailableRecordDto>? AvailableRecords = null);

/// <summary>Lo que se imprime en el informe de una conciliación cerrada (T0279). (nuevo)</summary>
public sealed record ConciliacionModel(string Company, string BankName, string AccountCode, string AccountNumber, int Year, int Month, int Version,
    decimal StatementBalance, decimal LedgerBalance, decimal Difference, IReadOnlyList<PartidaDelInforme> Items, int MatchedLines, string Conciliator, DateTime ClosedAt);

public sealed record PartidaDelInforme(string Kind, DateOnly Since, int AgeDays, decimal Amount, decimal SignedAmount, string? Note);

/// <summary>Lo común de los comandos: la conciliación visible con su cuenta, el DTO y lo que un cruce cambia fuera de la conciliación. (nuevo)</summary>
public static class ConciliacionMinima
{
    /// <summary>La conciliación seguida, si es de una cuenta al alcance.</summary>
    public static async Task<(ConciliacionBancaria Conciliacion, OwnBankAccount Cuenta)?> VisibleAsync(IApplicationDbContext db, IAlcanceDeTesoreria alcance, Guid publicId,
        CancellationToken ct)
    {
        var c = await db.TreasuryReconciliations.FirstOrDefaultAsync(x => x.PublicId == publicId, ct);
        if (c is null || !(await alcance.ObtenerAsync(ct)).IncluyeCuenta(c.BankAccountId)) return null;
        var cuenta = await db.OwnBankAccounts.AsNoTracking().FirstAsync(x => x.Id == c.BankAccountId, ct);
        return (c, cuenta);
    }

    public static string Clase(ReconcilingItemKind k) => k switch
    {
        ReconcilingItemKind.IssuedCheckNotCashed => "Cheque girado sin cobrar",
        ReconcilingItemKind.DepositInTransit => "Consignación en tránsito",
        ReconcilingItemKind.RemittanceInTransit => "Remesa en tránsito",
        ReconcilingItemKind.BankChargeNotRecorded => "Cargo del banco sin registrar",
        ReconcilingItemKind.BankCreditNotRecorded => "Abono del banco sin registrar",
        ReconcilingItemKind.UnidentifiedCredit => "Abono no identificado",
        ReconcilingItemKind.UnidentifiedDebit => "Cargo no identificado",
        _ => k.ToString(),
    };

    public static async Task<ReconciliationDto> DtoAsync(IApplicationDbContext db, ConciliacionBancaria c, bool detalle, CancellationToken ct,
        IReadOnlyList<RegistroDisponible>? disponibles = null)
    {
        var cuenta = await db.OwnBankAccounts.AsNoTracking().Where(x => x.Id == c.BankAccountId).Select(x => new { x.PublicId, x.Code }).FirstAsync(ct);
        var quien = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == c.ConciliatorUserId).Select(u => u.Username).FirstOrDefaultAsync(ct) ?? string.Empty;
        if (!detalle)
            return new ReconciliationDto(c.PublicId, cuenta.PublicId, cuenta.Code, c.PeriodYear, c.PeriodMonth, c.Version, c.Status.ToString(), c.StatementBalance,
                c.LedgerBalance, c.ReconcilingItemsTotal, c.Difference, quien, c.ClosedAt, c.ReportAttachmentPublicId, c.ReopenReason);

        var fin = ConciliacionesDeTesoreria.Fin(c);
        var cruces = await (from m in db.TreasuryReconciliationMatches.AsNoTracking()
                            join l in db.TreasuryBankStatementLines.AsNoTracking() on m.StatementLineId equals l.Id
                            join u in db.Users.AsNoTracking().IgnoreQueryFilters() on m.MatchedByUserId equals u.Id
                            where m.ReconciliationId == c.Id && m.IsLive
                            orderby m.Id
                            select new ReconciliationMatchDto(m.PublicId, m.GroupKey, l.PublicId, m.RecordKind.ToString(), m.RecordPublicId, m.RecordNumber, m.Amount,
                                m.Mode.ToString(), m.Rule, m.MatchedAt, u.Username)).ToListAsync(ct);
        var partidas = await (from i in db.TreasuryReconcilingItems.AsNoTracking()
                              join l in db.TreasuryBankStatementLines.AsNoTracking() on i.StatementLineId equals l.Id into ls
                              from l in ls.DefaultIfEmpty()
                              where i.ReconciliationId == c.Id && !i.IsVoided
                              orderby i.Id
                              select new { i, Linea = l == null ? (Guid?)null : l.PublicId }).ToListAsync(ct);
        var lineas = c.Status == ReconciliationState.Open ? await ConciliacionesDeTesoreria.LineasPorCruzarAsync(db, c, ct) : [];
        return new ReconciliationDto(c.PublicId, cuenta.PublicId, cuenta.Code, c.PeriodYear, c.PeriodMonth, c.Version, c.Status.ToString(), c.StatementBalance,
            c.LedgerBalance, c.ReconcilingItemsTotal, c.Difference, quien, c.ClosedAt, c.ReportAttachmentPublicId, c.ReopenReason, cruces,
            partidas.Select(x => new ReconcilingItemDto(x.i.PublicId, x.i.Kind.ToString(), x.i.Amount,
                ConciliacionesDeTesoreria.Signo(x.i.Kind, x.i.UnidentifiedItemPublicId) * x.i.Amount, x.i.Since, fin.DayNumber - x.i.Since.DayNumber, x.i.SourcePublicId,
                x.Linea, x.i.UnidentifiedItemPublicId, x.i.AlertPublicId, x.i.Note)).ToList(),
            lineas.Select(Statements.ExtractosDeTesoreria.Dto).ToList(),
            disponibles?.Select(r => new AvailableRecordDto(r.Kind.ToString(), r.PublicId, r.Number, r.Date, r.Direction.ToString(), r.Amount, r.Reference, r.Source))
                .ToList());
    }

    /// <summary>
    /// Lo que un cruce cambia fuera de la conciliación (US25-4): el cheque girado cruzado pasa a <c>Cashed</c> con la fecha de la línea; la remesa cruzada pasa a
    /// <c>Confirmed</c> con la línea del extracto y su hecho <c>RemesaConfirmada</c>. Lo demás no cambia. Sin guardar.
    /// </summary>
    public static async Task EfectosAsync(IApplicationDbContext db, EmisionDeTesoreria emision, OwnBankAccount cuenta, ReconciliationRecordKind clase, Guid registro,
        BankStatementLine linea, int usuario, DateTime ahora, CancellationToken ct)
    {
        if (clase == ReconciliationRecordKind.IssuedCheck)
        {
            var cheque = await db.IssuedChecks.FirstOrDefaultAsync(c => c.PublicId == registro, ct);
            if (cheque is null || cheque.Status is not (IssuedCheckState.Issued or IssuedCheckState.Delivered)) return;
            cheque.CashedOn = linea.MovementDate;
            ChequerasDeTesoreria.Transicion(db, cheque, IssuedCheckState.Cashed, usuario, ahora, nota: "Cruzado en la conciliación");
        }
        else if (clase == ReconciliationRecordKind.Remittance)
        {
            var remesa = await db.CashRemittances.FirstOrDefaultAsync(r => r.PublicId == registro, ct);
            if (remesa is null || remesa.Status != RemittanceState.InTransit) return;
            remesa.Status = RemittanceState.Confirmed;
            remesa.ConfirmedByUserId = usuario;
            remesa.ConfirmedAt = ahora;
            remesa.ConfirmedOn = linea.MovementDate;
            remesa.ConfirmedByStatementLinePublicId = linea.PublicId;
            await emision.RemesaConfirmadaAsync(cuenta, remesa, ct);
        }
        else if (clase == ReconciliationRecordKind.Transfer)
        {
            // T1 (fase 33; FR-104): el abono del traslado en la cuenta de destino lo confirma, con la línea y su hecho TrasladoConfirmado; el débito en la de origen no cambia nada.
            var traslado = await db.TreasuryTransfers.FirstOrDefaultAsync(t => t.PublicId == registro, ct);
            if (traslado is null || traslado.Status != TransferState.Sent || traslado.ToBankAccountId != cuenta.Id || !Transfers.TrasladosDeTesoreria.LineaLoConfirma(traslado, linea))
                return;
            await Transfers.TrasladosDeTesoreria.ConfirmarAsync(db, emision, traslado, usuario, ahora, linea.MovementDate, linea.PublicId, null, ct);
        }
    }

    /// <summary>Los tipos de hecho que emitiría cruzar estos registros (para la validación previa): la remesa en tránsito y el traslado recibido.</summary>
    public static IReadOnlyList<string> HechosDelCruce(IEnumerable<ReconciliationRecordKind> clases)
    {
        var lista = clases.ToList();
        var tipos = new List<string>();
        if (lista.Contains(ReconciliationRecordKind.Remittance)) tipos.Add(RemesaConfirmadaV1.Type);
        // T1 (fase 33): el traslado recibido emite TrasladoConfirmado.
        if (lista.Contains(ReconciliationRecordKind.Transfer)) tipos.Add(TrasladoConfirmadoV1.Type);
        return tipos;
    }
}

// ---------------------------------------------------------------------------------------------------------------- abrir --

/// <summary>
/// <c>POST /api/treasury/reconciliations</c> (<c>Treasury.Reconciliation.Work</c>, con clave): abre la conciliación de una cuenta y mes. Si ya hay una abierta la devuelve;
/// una cerrada se reabre por su ruta; una desactualizada (Contabilidad reabrió el período) se vuelve a conciliar con una versión nueva. El saldo del extracto sale de la
/// última línea o carga del mes, o el que diga quien abre. (nuevo)
/// </summary>
public sealed record OpenReconciliationCommand(Guid BankAccountPublicId, int Year, int Month, decimal? StatementBalance = null)
    : IRequest<Result<ReconciliationDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class OpenReconciliationCommandValidator : AbstractValidator<OpenReconciliationCommand>
{
    public OpenReconciliationCommandValidator()
    {
        RuleFor(x => x.BankAccountPublicId).NotEmpty();
        RuleFor(x => x.Year).InclusiveBetween(2000, 2100);
        RuleFor(x => x.Month).InclusiveBetween(1, 12);
    }
}

public sealed class OpenReconciliationCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    SegregacionDeTesoreria segregacion,
    IContabilidadParaTesoreria contabilidad,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<OpenReconciliationCommand, Result<ReconciliationDto>>
{
    public async Task<Result<ReconciliationDto>> Handle(OpenReconciliationCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        var cuenta = await db.OwnBankAccounts.AsNoTracking().FirstOrDefaultAsync(c => c.PublicId == request.BankAccountPublicId, ct);
        if (cuenta is null || !(await alcance.ObtenerAsync(ct)).IncluyeCuenta(cuenta.Id))
            return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.NoEncontrado("La cuenta bancaria propia"));
        var hoy = reloj.HoyLocal;
        if (new DateOnly(request.Year, request.Month, 1) > new DateOnly(hoy.Year, hoy.Month, 1)) return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.ConciliacionPeriodoInvalido());
        var desde = new DateOnly(request.Year, request.Month, 1);
        var participantes = await ConciliacionesDeTesoreria.ParticipantesDePagosAsync(db, cuenta.Id, desde, desde.AddMonths(1).AddDays(-1), ct);
        var segregada = await segregacion.ConciliarAsync(usuario, participantes, ct);
        if (segregada.IsFailure) return Result.Failure<ReconciliationDto>(segregada.Error);

        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, [cuenta.Id], exclusivo: false, ct);
        var ultima = await db.TreasuryReconciliations.Where(c => c.BankAccountId == cuenta.Id && c.PeriodYear == request.Year && c.PeriodMonth == request.Month)
            .OrderByDescending(c => c.Version).FirstOrDefaultAsync(ct);
        if (ultima is { Status: ReconciliationState.Open }) return Result.Success(await ConciliacionMinima.DtoAsync(db, ultima, detalle: false, ct));
        if (ultima is { Status: ReconciliationState.Closed }) return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.ConciliacionCerrada());

        var nueva = new ConciliacionBancaria
        {
            BankAccountId = cuenta.Id, PeriodYear = request.Year, PeriodMonth = request.Month, Version = (ultima?.Version ?? 0) + 1,
            Kind = Domain.Treasury.CatalogoDeTesoreria.EntregaVigente >= Domain.Treasury.EntregaDeTesoreria.T2 ? ReconciliationKind.Complete : ReconciliationKind.Minimal,
            ConciliatorUserId = usuario, Status = ReconciliationState.Open, PreviousReconciliationId = ultima?.Id,
        };
        db.TreasuryReconciliations.Add(nueva);
        await ConciliacionesDeTesoreria.RecalcularAsync(db, contabilidad, cuenta, nueva, request.StatementBalance, ct);
        await auditoria.AgregarAsync("Treasury.Reconciliation.Opened", "Reconciliation", nueva.PublicId, null,
            new { account = cuenta.Code, year = request.Year, month = request.Month, version = nueva.Version, statement = nueva.StatementBalance, ledger = nueva.LedgerBalance,
                by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ConciliacionMinima.DtoAsync(db, nueva, detalle: false, ct));
    }
}

// --------------------------------------------------------------------------------------------------------- consultar --

/// <summary><c>GET /api/treasury/reconciliations?account=&amp;year=&amp;month=</c> (<c>Treasury.Reconciliation.View</c>). (nuevo)</summary>
public sealed record ListReconciliationsQuery(Guid? BankAccountPublicId = null, int? Year = null, int? Month = null) : IRequest<Result<IReadOnlyList<ReconciliationDto>>>;

public sealed class ListReconciliationsQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance)
    : IRequestHandler<ListReconciliationsQuery, Result<IReadOnlyList<ReconciliationDto>>>
{
    public async Task<Result<IReadOnlyList<ReconciliationDto>>> Handle(ListReconciliationsQuery request, CancellationToken ct)
    {
        var mio = await alcance.ObtenerAsync(ct);
        var consulta = db.TreasuryReconciliations.AsNoTracking();
        if (request.BankAccountPublicId is { } c)
        {
            var id = await db.OwnBankAccounts.AsNoTracking().Where(x => x.PublicId == c).Select(x => (int?)x.Id).FirstOrDefaultAsync(ct);
            consulta = consulta.Where(r => r.BankAccountId == id);
        }
        if (request.Year is { } y) consulta = consulta.Where(r => r.PeriodYear == y);
        if (request.Month is { } m) consulta = consulta.Where(r => r.PeriodMonth == m);
        var filas = (await consulta.OrderByDescending(r => r.PeriodYear).ThenByDescending(r => r.PeriodMonth).ThenByDescending(r => r.Version).Take(200).ToListAsync(ct))
            .Where(r => mio.IncluyeCuenta(r.BankAccountId)).ToList();
        var lista = new List<ReconciliationDto>(filas.Count);
        foreach (var r in filas) lista.Add(await ConciliacionMinima.DtoAsync(db, r, detalle: false, ct));
        return Result.Success<IReadOnlyList<ReconciliationDto>>(lista);
    }
}

/// <summary><c>GET /api/treasury/reconciliations/{id}</c> (<c>Treasury.Reconciliation.View</c>): el detalle con cruces, partidas, líneas y registros por cruzar. (nuevo)</summary>
public sealed record GetReconciliationQuery(Guid ReconciliationPublicId) : IRequest<Result<ReconciliationDto>>;

public sealed class GetReconciliationQueryHandler(IApplicationDbContext db, IAlcanceDeTesoreria alcance, IEnumerable<IRecaudoParaConciliacion> recaudo,
    IContabilidadParaTesoreria contabilidad, ParametrosDeTesoreria parametros) : IRequestHandler<GetReconciliationQuery, Result<ReconciliationDto>>
{
    public async Task<Result<ReconciliationDto>> Handle(GetReconciliationQuery request, CancellationToken ct)
    {
        var c = await db.TreasuryReconciliations.AsNoTracking().FirstOrDefaultAsync(x => x.PublicId == request.ReconciliationPublicId, ct);
        if (c is null || !(await alcance.ObtenerAsync(ct)).IncluyeCuenta(c.BankAccountId)) return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.NoEncontrado("La conciliación"));
        IReadOnlyList<RegistroDisponible>? disponibles = null;
        if (c.Status == ReconciliationState.Open)
        {
            var cuenta = await db.OwnBankAccounts.AsNoTracking().FirstAsync(x => x.Id == c.BankAccountId, ct);
            var holgura = await ConciliacionesDeTesoreria.HolguraAsync(parametros, ConciliacionesDeTesoreria.Fin(c), ct);
            disponibles = await ConciliacionesDeTesoreria.RegistrosAsync(db, recaudo, contabilidad, cuenta, c, holgura, ct);
        }
        return Result.Success(await ConciliacionMinima.DtoAsync(db, c, detalle: true, ct, disponibles));
    }
}

// -------------------------------------------------------------------------------------------------- cruce automático --

/// <summary>
/// <c>POST /api/treasury/reconciliations/{id}/auto-match</c> (<c>Treasury.Reconciliation.Work</c>, con clave): corre <see cref="CruceDeConciliacion"/> sobre las líneas y
/// los registros que faltan por cruzar con <c>Conciliacion.DiasDeHolgura</c> y deja los cruces automáticos, con sus efectos (cheque cobrado, remesa confirmada). (nuevo)
/// </summary>
public sealed record AutoMatchReconciliationCommand(Guid ReconciliationPublicId) : IRequest<Result<ReconciliationDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class AutoMatchReconciliationCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    SegregacionDeTesoreria segregacion,
    IEnumerable<IRecaudoParaConciliacion> recaudo,
    IContabilidadParaTesoreria contabilidad,
    ParametrosDeTesoreria parametros,
    EmisionDeTesoreria emision,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<AutoMatchReconciliationCommand, Result<ReconciliationDto>>
{
    public async Task<Result<ReconciliationDto>> Handle(AutoMatchReconciliationCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        if (await ConciliacionMinima.VisibleAsync(db, alcance, request.ReconciliationPublicId, ct) is not { } v)
            return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.NoEncontrado("La conciliación"));
        var (c, cuenta) = v;
        if (c.Status != ReconciliationState.Open) return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.ConciliacionNoAbierta(c.Status.ToString()));
        var revision = await ReglasDelConciliador.RevisarAsync(db, segregacion, c, usuario, ct);
        if (revision.IsFailure) return Result.Failure<ReconciliationDto>(revision.Error);
        var fin = ConciliacionesDeTesoreria.Fin(c);
        var previa = await emision.ValidacionPreviaAsync(ConciliacionMinima.HechosDelCruce([ReconciliationRecordKind.Remittance, ReconciliationRecordKind.Transfer]),
            cuenta, fin, ct);
        if (previa.IsFailure) return Result.Failure<ReconciliationDto>(previa.Error);

        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, [cuenta.Id], exclusivo: false, ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Conciliacion, [c.Id], ct: ct);
        var holgura = await ConciliacionesDeTesoreria.HolguraAsync(parametros, fin, ct);
        var lineas = await ConciliacionesDeTesoreria.LineasPorCruzarAsync(db, c, ct);
        var registros = await ConciliacionesDeTesoreria.RegistrosAsync(db, recaudo, contabilidad, cuenta, c, holgura, ct);
        var porClave = registros.ToDictionary(r => r.Clave, StringComparer.Ordinal);
        // T2 (fase 41; FR-129): con reglas activas, el cruce las usa en su orden y con su holgura; sin reglas, el de la mínima.
        var reglas = await ConciliacionCompletaDeTesoreria.ReglasAsync(db, cuenta.Id, ct);
        var cruces = CruceDeConciliacion.CruzarConReglas(
            lineas.Select(l => new LineaACruzar(l.Id, l.PublicId, l.MovementDate, l.Direction, l.Amount, l.Reference)).ToList(),
            registros.Select(r => r.ParaElMotor()).ToList(), reglas, holgura);
        var ahora = reloj.UtcNow;
        foreach (var x in cruces)
        {
            var linea = await db.TreasuryBankStatementLines.FirstAsync(l => l.Id == x.LineaId, ct);
            var grupo = Guid.NewGuid();
            foreach (var clave in x.Registros)
            {
                var r = porClave[clave];
                db.TreasuryReconciliationMatches.Add(new ReconciliationMatch
                {
                    ReconciliationId = c.Id, StatementLineId = linea.Id, RecordKind = r.Kind, RecordPublicId = r.PublicId, RecordNumber = Recortar(r.Number, 60),
                    Amount = r.Amount, Mode = ReconciliationMatchMode.Automatic, Rule = x.Regla, GroupKey = grupo, MatchedByUserId = usuario, MatchedAt = ahora,
                });
                await ConciliacionMinima.EfectosAsync(db, emision, cuenta, r.Kind, r.PublicId, linea, usuario, ahora, ct);
            }
            linea.ReconciliationStatus = StatementLineReconciliationStatus.Matched;
        }
        await ConciliacionesDeTesoreria.RecalcularAsync(db, contabilidad, cuenta, c, null, ct);
        await auditoria.AgregarAsync("Treasury.Reconciliation.AutoMatched", "Reconciliation", c.PublicId, null,
            new { lines = lineas.Count, records = registros.Count, matched = cruces.Count, holgura, difference = c.Difference, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ConciliacionMinima.DtoAsync(db, c, detalle: false, ct));
    }

    internal static string Recortar(string t, int largo) => t.Length <= largo ? t : t[..largo];
}

/// <summary>Las reglas que valen para todo lo que hace el conciliador sobre una conciliación: la segregación de FR-217, revisada con los pagos del mes. (nuevo)</summary>
public static class ReglasDelConciliador
{
    public static async Task<Result> RevisarAsync(IApplicationDbContext db, SegregacionDeTesoreria segregacion, ConciliacionBancaria c, int usuario, CancellationToken ct)
    {
        var participantes = await ConciliacionesDeTesoreria.ParticipantesDePagosAsync(db, c.BankAccountId, ConciliacionesDeTesoreria.Inicio(c), ConciliacionesDeTesoreria.Fin(c),
            ct);
        return await segregacion.ConciliarAsync(usuario, participantes, ct);
    }
}

// ------------------------------------------------------------------------------------------------------- cruce manual --

/// <summary>Un registro que el conciliador elige para un cruce manual. (nuevo)</summary>
public sealed record RegistroElegido(ReconciliationRecordKind Kind, Guid PublicId);

/// <summary>
/// <c>POST /api/treasury/reconciliations/{id}/matches</c> (<c>Treasury.Reconciliation.Work</c>, con clave): cruza a mano una línea con uno o varios registros que suman
/// exacto su valor. Una línea o un registro ya cruzado responde <c>Treasury.Reconciliation.AlreadyMatched</c>. (nuevo)
/// </summary>
public sealed record AddManualMatchCommand(Guid ReconciliationPublicId, Guid StatementLinePublicId, IReadOnlyList<RegistroElegido> Records, string? Note = null)
    : IRequest<Result<ReconciliationDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class AddManualMatchCommandValidator : AbstractValidator<AddManualMatchCommand>
{
    public AddManualMatchCommandValidator()
    {
        RuleFor(x => x.ReconciliationPublicId).NotEmpty();
        RuleFor(x => x.StatementLinePublicId).NotEmpty();
        RuleFor(x => x.Records).NotEmpty().WithMessage("Elija al menos un registro de los libros para el cruce.");
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class AddManualMatchCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    SegregacionDeTesoreria segregacion,
    IEnumerable<IRecaudoParaConciliacion> recaudo,
    IContabilidadParaTesoreria contabilidad,
    ParametrosDeTesoreria parametros,
    EmisionDeTesoreria emision,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<AddManualMatchCommand, Result<ReconciliationDto>>
{
    public async Task<Result<ReconciliationDto>> Handle(AddManualMatchCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        if (await ConciliacionMinima.VisibleAsync(db, alcance, request.ReconciliationPublicId, ct) is not { } v)
            return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.NoEncontrado("La conciliación"));
        var (c, cuenta) = v;
        if (c.Status != ReconciliationState.Open) return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.ConciliacionNoAbierta(c.Status.ToString()));
        var revision = await ReglasDelConciliador.RevisarAsync(db, segregacion, c, usuario, ct);
        if (revision.IsFailure) return Result.Failure<ReconciliationDto>(revision.Error);
        var fin = ConciliacionesDeTesoreria.Fin(c);
        var previa = await emision.ValidacionPreviaAsync(ConciliacionMinima.HechosDelCruce(request.Records.Select(r => r.Kind)).ToList(), cuenta, fin, ct);
        if (previa.IsFailure) return Result.Failure<ReconciliationDto>(previa.Error);

        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, [cuenta.Id], exclusivo: false, ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Conciliacion, [c.Id], ct: ct);
        var linea = await db.TreasuryBankStatementLines.FirstOrDefaultAsync(l => l.PublicId == request.StatementLinePublicId && l.BankAccountId == c.BankAccountId, ct);
        if (linea is null || linea.MovementDate < ConciliacionesDeTesoreria.Inicio(c) || linea.MovementDate > fin)
            return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.NoEncontrado("La línea del extracto"));
        if ((await ConciliacionesDeTesoreria.LineasUsadasAsync(db, c, ct)).Contains(linea.Id)
            || await db.TreasuryReconciliationMatches.AnyAsync(m => m.StatementLineId == linea.Id && m.IsLive && m.ReconciliationId != c.Id
                                                                    && db.TreasuryReconciliations.Any(r => r.Id == m.ReconciliationId
                                                                        && (r.Status == ReconciliationState.Open || r.Status == ReconciliationState.Closed)), ct))
            return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.ConciliacionYaCruzada($"La línea {linea.LineNumber}"));
        var holgura = await ConciliacionesDeTesoreria.HolguraAsync(parametros, fin, ct);
        var disponibles = (await ConciliacionesDeTesoreria.RegistrosAsync(db, recaudo, contabilidad, cuenta, c, holgura, ct))
            .ToDictionary(r => r.Clave, StringComparer.Ordinal);
        var elegidos = new List<RegistroDisponible>();
        foreach (var e in request.Records.DistinctBy(e => (e.Kind, e.PublicId)))
        {
            var clave = RegistroDisponible.Clave_(e.Kind, e.PublicId);
            if (await db.TreasuryReconciliationMatches.AnyAsync(m => m.ReconciliationId == c.Id && m.IsLive && m.RecordKind == e.Kind && m.RecordPublicId == e.PublicId, ct))
                return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.ConciliacionYaCruzada($"El registro {e.PublicId:N}"));
            if (!disponibles.TryGetValue(clave, out var r)) return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.ConciliacionRegistroNoDisponible());
            if (r.Direction != linea.Direction) return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.ConciliacionRegistroNoDisponible());
            elegidos.Add(r);
        }
        var suma = elegidos.Sum(r => r.Amount);
        if (suma != linea.Amount) return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.ConciliacionValorNoCuadra(linea.Amount, suma));

        var ahora = reloj.UtcNow;
        var grupo = Guid.NewGuid();
        foreach (var r in elegidos)
        {
            db.TreasuryReconciliationMatches.Add(new ReconciliationMatch
            {
                ReconciliationId = c.Id, StatementLineId = linea.Id, RecordKind = r.Kind, RecordPublicId = r.PublicId,
                RecordNumber = AutoMatchReconciliationCommandHandler.Recortar(r.Number, 60), Amount = r.Amount, Mode = ReconciliationMatchMode.Manual, Rule = "Manual",
                GroupKey = grupo, MatchedByUserId = usuario, MatchedAt = ahora,
            });
            await ConciliacionMinima.EfectosAsync(db, emision, cuenta, r.Kind, r.PublicId, linea, usuario, ahora, ct);
        }
        linea.ReconciliationStatus = StatementLineReconciliationStatus.ManuallyMarked;
        await ConciliacionesDeTesoreria.RecalcularAsync(db, contabilidad, cuenta, c, null, ct);
        await auditoria.AgregarAsync("Treasury.Reconciliation.ManualMatch", "Reconciliation", c.PublicId, null,
            new { line = linea.PublicId, records = elegidos.Select(r => new { kind = r.Kind.ToString(), r.PublicId, r.Amount }), note = request.Note, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ConciliacionMinima.DtoAsync(db, c, detalle: false, ct));
    }
}

/// <summary>
/// <c>DELETE /api/treasury/reconciliations/{id}/matches/{mid}</c> (<c>Treasury.Reconciliation.Work</c>, con clave): deshace un cruce (todo su grupo) de una conciliación
/// abierta; la fila no se borra, queda no vigente. Lo que el cruce cambió afuera (el cheque cobrado, la remesa confirmada) no se devuelve: se corrige en su pantalla. (nuevo)
/// </summary>
public sealed record RemoveMatchCommand(Guid ReconciliationPublicId, Guid MatchPublicId) : IRequest<Result<ReconciliationDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class RemoveMatchCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    IContabilidadParaTesoreria contabilidad,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<RemoveMatchCommand, Result<ReconciliationDto>>
{
    public async Task<Result<ReconciliationDto>> Handle(RemoveMatchCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        if (await ConciliacionMinima.VisibleAsync(db, alcance, request.ReconciliationPublicId, ct) is not { } v)
            return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.NoEncontrado("La conciliación"));
        var (c, cuenta) = v;
        if (c.Status == ReconciliationState.Closed) return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.ConciliacionCerrada());
        if (c.Status != ReconciliationState.Open) return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.ConciliacionNoAbierta(c.Status.ToString()));
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Conciliacion, [c.Id], ct: ct);
        var cruce = await db.TreasuryReconciliationMatches.FirstOrDefaultAsync(m => m.PublicId == request.MatchPublicId && m.ReconciliationId == c.Id, ct);
        if (cruce is null) return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.NoEncontrado("El cruce"));
        if (!cruce.IsLive) return Result.Success(await ConciliacionMinima.DtoAsync(db, c, detalle: false, ct));
        var ahora = reloj.UtcNow;
        var grupo = await db.TreasuryReconciliationMatches.Where(m => m.ReconciliationId == c.Id && m.GroupKey == cruce.GroupKey && m.IsLive).ToListAsync(ct);
        foreach (var m in grupo)
        {
            m.IsLive = false;
            m.UndoneByUserId = usuario;
            m.UndoneAt = ahora;
        }
        var linea = await db.TreasuryBankStatementLines.FirstAsync(l => l.Id == cruce.StatementLineId, ct);
        linea.ReconciliationStatus = StatementLineReconciliationStatus.Open;
        await ConciliacionesDeTesoreria.RecalcularAsync(db, contabilidad, cuenta, c, null, ct);
        await auditoria.AgregarAsync("Treasury.Reconciliation.MatchUndone", "Reconciliation", c.PublicId, null,
            new { line = linea.PublicId, records = grupo.Select(m => m.RecordPublicId), by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ConciliacionMinima.DtoAsync(db, c, detalle: false, ct));
    }
}

// ------------------------------------------------------------------------------------------------ partidas conciliatorias --

/// <summary>
/// <c>POST /api/treasury/reconciliations/{id}/items</c> (<c>Treasury.Reconciliation.Work</c>, con clave): registra una partida conciliatoria. Un abono sin registro con su
/// línea pasa a Recaudo como partida por identificar (<see cref="IPartidasDesdeConciliacion"/>, US25-2) y su línea queda cruzada con ella; un cargo no identificado
/// (embargo, débito no autorizado) alerta <c>Tesoreria.CargoNoIdentificado</c>. (nuevo)
/// </summary>
public sealed record AddReconcilingItemCommand(Guid ReconciliationPublicId, ReconcilingItemKind Kind, decimal Amount, DateOnly? Since = null,
    Guid? StatementLinePublicId = null, Guid? SourcePublicId = null, string? Note = null) : IRequest<Result<ReconciliationDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class AddReconcilingItemCommandValidator : AbstractValidator<AddReconcilingItemCommand>
{
    public AddReconcilingItemCommandValidator()
    {
        RuleFor(x => x.ReconciliationPublicId).NotEmpty();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Amount).GreaterThan(0m).WithMessage("El valor de la partida es positivo: su clase dice si suma o resta.");
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.StatementLinePublicId).NotEmpty().When(x => x.Kind is ReconcilingItemKind.UnidentifiedCredit or ReconcilingItemKind.UnidentifiedDebit)
            .WithMessage("Un abono o cargo no identificado sale de una línea del extracto: elíjala.");
    }
}

public sealed class AddReconcilingItemCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    SegregacionDeTesoreria segregacion,
    IEnumerable<IPartidasDesdeConciliacion> partidas,
    IContabilidadParaTesoreria contabilidad,
    IAlertas alertas,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<AddReconcilingItemCommand, Result<ReconciliationDto>>
{
    public const string ReglaPartidaDeRecaudo = "UnidentifiedItemFromReconciliation";

    public async Task<Result<ReconciliationDto>> Handle(AddReconcilingItemCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        if (await ConciliacionMinima.VisibleAsync(db, alcance, request.ReconciliationPublicId, ct) is not { } v)
            return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.NoEncontrado("La conciliación"));
        var (c, cuenta) = v;
        if (c.Status != ReconciliationState.Open) return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.ConciliacionNoAbierta(c.Status.ToString()));
        var revision = await ReglasDelConciliador.RevisarAsync(db, segregacion, c, usuario, ct);
        if (revision.IsFailure) return Result.Failure<ReconciliationDto>(revision.Error);

        BankStatementLine? linea = null;
        if (request.StatementLinePublicId is { } lp)
        {
            linea = await db.TreasuryBankStatementLines.FirstOrDefaultAsync(l => l.PublicId == lp && l.BankAccountId == c.BankAccountId, ct);
            if (linea is null) return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.NoEncontrado("La línea del extracto"));
            if ((await ConciliacionesDeTesoreria.LineasUsadasAsync(db, c, ct)).Contains(linea.Id))
                return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.ConciliacionYaCruzada($"La línea {linea.LineNumber}"));
            if (linea.Amount != request.Amount) return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.ConciliacionValorNoCuadra(linea.Amount, request.Amount));
            var esperado = request.Kind is ReconcilingItemKind.UnidentifiedCredit or ReconcilingItemKind.BankCreditNotRecorded
                ? StatementLineDirection.Credit
                : request.Kind is ReconcilingItemKind.UnidentifiedDebit or ReconcilingItemKind.BankChargeNotRecorded ? StatementLineDirection.Debit : linea.Direction;
            if (linea.Direction != esperado)
                return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.ConciliacionPartidaInvalida("La clase de la partida no corresponde al sentido de la línea del extracto."));
        }
        var aRecaudo = request.Kind == ReconcilingItemKind.UnidentifiedCredit && linea is not null;
        if (aRecaudo && linea!.RecaudoStatus is not (StatementLineRecaudoStatus.Pending or StatementLineRecaudoStatus.PartiallyIdentified))
            return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.ConciliacionPartidaInvalida(
                "El abono ya está identificado en Recaudo o no es de recaudo: crúcelo con su registro en vez de llevarlo a partidas."));

        // CH16: lo de Recaudo primero, después los cerrojos de Tesorería.
        if (aRecaudo) foreach (var p in partidas) await p.TomarCerrojosAsync(cuenta.PublicId, ct);
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.CuentaDeOrigen, [cuenta.Id], exclusivo: false, ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Conciliacion, [c.Id], ct: ct);

        var ahora = reloj.UtcNow;
        var partida = new ReconcilingItem
        {
            ReconciliationId = c.Id, Kind = request.Kind, Amount = request.Amount, Since = request.Since ?? linea?.MovementDate ?? ConciliacionesDeTesoreria.Fin(c),
            SourcePublicId = request.SourcePublicId ?? linea?.PublicId, StatementLineId = linea?.Id, Note = request.Note?.Trim(), CreatedByUserId = usuario,
        };
        if (aRecaudo)
        {
            var puerto = partidas.FirstOrDefault();
            if (puerto is null)
                return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.ConciliacionPartidaInvalida("Recaudo no está disponible para recibir la partida por identificar."));
            var creada = await puerto.CrearAsync(linea!.PublicId, request.Note ?? $"Abono sin registro en la conciliación {c.PeriodYear}-{c.PeriodMonth:00}", ct);
            partida.UnidentifiedItemPublicId = creada.UnidentifiedItemPublicId;
            db.TreasuryReconciliationMatches.Add(new ReconciliationMatch
            {
                ReconciliationId = c.Id, StatementLineId = linea.Id, RecordKind = ReconciliationRecordKind.ReceiptsBankReceipt, RecordPublicId = creada.UnidentifiedItemPublicId,
                RecordNumber = creada.Number, Amount = linea.Amount, Mode = ReconciliationMatchMode.Manual, Rule = ReglaPartidaDeRecaudo, GroupKey = Guid.NewGuid(),
                MatchedByUserId = usuario, MatchedAt = ahora,
            });
        }
        if (request.Kind == ReconcilingItemKind.UnidentifiedDebit)
        {
            var alerta = await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.TesoreriaCargoNoIdentificado, "Cargo bancario no identificado",
                string.Create(CultureInfo.InvariantCulture,
                    $"La cuenta {cuenta.Code} tiene en el extracto un cargo de {request.Amount:N0} del {partida.Since:dd/MM/yyyy} sin registro en el sistema (posible embargo o débito no autorizado): revíselo con el banco."),
                "Reconciliation", c.PublicId, DedupKey: $"Tesoreria.CargoNoIdentificado:{linea?.PublicId ?? partida.PublicId:N}", ModuloDeOrigen: "Treasury"), ct);
            partida.AlertPublicId = alerta.IsSuccess ? alerta.Value.AlertPublicId : null;
        }
        if (linea is not null) linea.ReconciliationStatus = StatementLineReconciliationStatus.ManuallyMarked;
        db.TreasuryReconcilingItems.Add(partida);
        await ConciliacionesDeTesoreria.RecalcularAsync(db, contabilidad, cuenta, c, null, ct);
        await auditoria.AgregarAsync("Treasury.Reconciliation.ItemAdded", "Reconciliation", c.PublicId, null,
            new { kind = request.Kind.ToString(), amount = request.Amount, since = partida.Since, unidentified = partida.UnidentifiedItemPublicId, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ConciliacionMinima.DtoAsync(db, c, detalle: false, ct));
    }
}

// ---------------------------------------------------------------------------------------------------------- cerrar --

/// <summary>
/// <c>POST /api/treasury/reconciliations/{id}/close</c> (<c>Treasury.Reconciliation.Close</c>, con clave): cierra con diferencia cero (<c>.NotSquared</c> si no) y guarda su
/// informe en PDF como adjunto no borrable <c>TreasuryReconciliationReport</c> antes de cerrar la fila. Quien cierra tampoco preparó ni aprobó pagos del mes. (nuevo)
/// </summary>
public sealed record CloseReconciliationCommand(Guid ReconciliationPublicId) : IRequest<Result<ReconciliationDto>>, IOperacionIdempotente, IEscrituraDeTesoreria
{
    public Guid OperationKey { get; init; }
}

public sealed class CloseReconciliationCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    SegregacionDeTesoreria segregacion,
    IContabilidadParaTesoreria contabilidad,
    IDocumentosDeTesoreriaEnPdf documentos,
    ISender sender,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<CloseReconciliationCommand, Result<ReconciliationDto>>
{
    public const string TipoDeAdjunto = AdjuntosDeModulo.InformeDeConciliacion;

    public async Task<Result<ReconciliationDto>> Handle(CloseReconciliationCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        if (await ConciliacionMinima.VisibleAsync(db, alcance, request.ReconciliationPublicId, ct) is not { } v)
            return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.NoEncontrado("La conciliación"));
        var (c, cuenta) = v;
        if (c.Status == ReconciliationState.Closed) return Result.Success(await ConciliacionMinima.DtoAsync(db, c, detalle: false, ct));
        if (c.Status != ReconciliationState.Open) return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.ConciliacionNoAbierta(c.Status.ToString()));
        var revision = await ReglasDelConciliador.RevisarAsync(db, segregacion, c, usuario, ct);
        if (revision.IsFailure) return Result.Failure<ReconciliationDto>(revision.Error);

        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Conciliacion, [c.Id], ct: ct);
        await ConciliacionesDeTesoreria.RecalcularAsync(db, contabilidad, cuenta, c, null, ct);
        if (c.Difference != 0m) return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.ConciliacionNoCuadra(c.Difference));

        var ahora = reloj.UtcNow;
        var detalle = await ConciliacionMinima.DtoAsync(db, c, detalle: true, ct);
        var empresa = await db.Companies.AsNoTracking().Select(e => e.Name).FirstOrDefaultAsync(ct) ?? string.Empty;
        var banco = await db.Banks.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == cuenta.BankId).Select(b => b.Name).FirstOrDefaultAsync(ct) ?? string.Empty;
        var modelo = new ConciliacionModel(empresa, banco, cuenta.Code, cuenta.AccountNumber, c.PeriodYear, c.PeriodMonth, c.Version, c.StatementBalance, c.LedgerBalance,
            c.Difference,
            (detalle.Items ?? []).Select(i => new PartidaDelInforme(ConciliacionMinima.Clase(Enum.Parse<ReconcilingItemKind>(i.Kind)), i.Since, i.AgeDays, i.Amount,
                i.SignedAmount, i.Note)).ToList(),
            (detalle.Matches ?? []).Select(m => m.StatementLinePublicId).Distinct().Count(), actor.Name, ahora);
        var pdf = documentos.Conciliacion(modelo);
        var nombre = string.Create(CultureInfo.InvariantCulture, $"conciliacion-{cuenta.Code}-{c.PeriodYear}{c.PeriodMonth:00}-v{c.Version}.pdf");
        var subida = await sender.Send(new UploadAttachmentCommand(TipoDeAdjunto, c.PublicId, nombre, "application/pdf", pdf), ct);
        if (subida.IsFailure) return Result.Failure<ReconciliationDto>(subida.Error);
        c.ReportAttachmentPublicId = subida.Value;
        c.Status = ReconciliationState.Closed;
        c.ClosedAt = ahora;
        c.ClosedByUserId = usuario;
        await auditoria.AgregarAsync("Treasury.Reconciliation.Closed", "Reconciliation", c.PublicId, new { status = "Open" },
            new { status = "Closed", statement = c.StatementBalance, ledger = c.LedgerBalance, items = c.ReconcilingItemsTotal, report = c.ReportAttachmentPublicId, by = actor.Name },
            ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ConciliacionMinima.DtoAsync(db, c, detalle: false, ct));
    }
}

// --------------------------------------------------------------------------------------------------------- reabrir --

/// <summary>
/// <c>POST /api/treasury/reconciliations/{id}/reopen</c> (<c>Treasury.Reconciliation.Reopen</c>, con clave y motivo): la cerrada queda <c>Reopened</c> —no se cambia— y
/// nace una versión nueva abierta con sus cruces y partidas vigentes copiados, para corregir sobre ella. (nuevo)
/// </summary>
public sealed record ReopenReconciliationCommand(Guid ReconciliationPublicId, string Reason) : IRequest<Result<ReconciliationDto>>, IOperacionIdempotente,
    IEscrituraDeTesoreria, IConMotivo
{
    public Guid OperationKey { get; init; }
}

public sealed class ReopenReconciliationCommandValidator : ValidadorConMotivo<ReopenReconciliationCommand>
{
    public ReopenReconciliationCommandValidator() => RuleFor(x => x.ReconciliationPublicId).NotEmpty();
}

public sealed class ReopenReconciliationCommandHandler(
    IApplicationDbContext db,
    IAlcanceDeTesoreria alcance,
    IActorActual actorActual,
    ICerrojoDeTesoreria cerrojo,
    IContabilidadParaTesoreria contabilidad,
    AuditoriaDeTesoreria auditoria,
    IDateTimeService reloj) : IRequestHandler<ReopenReconciliationCommand, Result<ReconciliationDto>>
{
    public async Task<Result<ReconciliationDto>> Handle(ReopenReconciliationCommand request, CancellationToken ct)
    {
        var actor = await actorActual.ObtenerAsync(ct);
        if (actor.UserId is not { } usuario) return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.NoEncontrado("El usuario"));
        if (await ConciliacionMinima.VisibleAsync(db, alcance, request.ReconciliationPublicId, ct) is not { } v)
            return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.NoEncontrado("La conciliación"));
        var (c, cuenta) = v;
        if (c.Status != ReconciliationState.Closed) return Result.Failure<ReconciliationDto>(ErroresDeTesoreria.ConciliacionNoAbierta(c.Status.ToString()));
        await cerrojo.TomarSetupAsync(ct: ct);
        await cerrojo.BloquearAsync(NivelDeTesoreria.Conciliacion, [c.Id], ct: ct);
        var ahora = reloj.UtcNow;
        c.Status = ReconciliationState.Reopened;
        c.ReopenReason = request.Reason.Trim();
        c.ReopenedByUserId = usuario;
        c.ReopenedAt = ahora;
        var nueva = new ConciliacionBancaria
        {
            BankAccountId = c.BankAccountId, PeriodYear = c.PeriodYear, PeriodMonth = c.PeriodMonth, Version = c.Version + 1, Kind = c.Kind, ConciliatorUserId = usuario,
            Status = ReconciliationState.Open, PreviousReconciliationId = c.Id, StatementBalance = c.StatementBalance,
        };
        db.TreasuryReconciliations.Add(nueva);
        await db.SaveChangesAsync(ct);
        foreach (var m in await db.TreasuryReconciliationMatches.AsNoTracking().Where(m => m.ReconciliationId == c.Id && m.IsLive).ToListAsync(ct))
            db.TreasuryReconciliationMatches.Add(new ReconciliationMatch
            {
                ReconciliationId = nueva.Id, StatementLineId = m.StatementLineId, RecordKind = m.RecordKind, RecordPublicId = m.RecordPublicId, RecordNumber = m.RecordNumber,
                Amount = m.Amount, Mode = m.Mode, Rule = m.Rule, GroupKey = m.GroupKey, MatchedByUserId = m.MatchedByUserId, MatchedAt = m.MatchedAt,
            });
        foreach (var i in await db.TreasuryReconcilingItems.AsNoTracking().Where(i => i.ReconciliationId == c.Id && !i.IsVoided).ToListAsync(ct))
            db.TreasuryReconcilingItems.Add(new ReconcilingItem
            {
                ReconciliationId = nueva.Id, Kind = i.Kind, Amount = i.Amount, Since = i.Since, SourcePublicId = i.SourcePublicId, StatementLineId = i.StatementLineId,
                UnidentifiedItemPublicId = i.UnidentifiedItemPublicId, Note = i.Note, AlertPublicId = i.AlertPublicId, CreatedByUserId = i.CreatedByUserId,
            });
        await ConciliacionesDeTesoreria.RecalcularAsync(db, contabilidad, cuenta, nueva, c.StatementBalance, ct);
        await auditoria.AgregarAsync("Treasury.Reconciliation.Reopened", "Reconciliation", c.PublicId, new { status = "Closed" },
            new { status = "Reopened", reason = c.ReopenReason, newVersion = nueva.Version, newPublicId = nueva.PublicId, by = actor.Name }, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success(await ConciliacionMinima.DtoAsync(db, nueva, detalle: false, ct));
    }
}
