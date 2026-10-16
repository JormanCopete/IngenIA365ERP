using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Collections;
using IngenIA365ERP.Application.Lending.Collections.Agreements;
using IngenIA365ERP.Application.Lending.Collections.PriorNotices;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Collections;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Reports.Vistas;

// Feature 013, K4 (US12, T0953; contracts/api.md §17): las cuatro vistas de la cobranza en el centro de informes de Cartera —la cartera por etapa, las
// promesas, los acuerdos de pago y los avisos previos—. Todas traen datos del deudor (PD: Lending.Loans.Read para consultarlas,
// Lending.Reports.ExportPersonalData para exportarlas), filtran por el alcance en la consulta de créditos (AmbitoDeInformeDeCartera) y llevan la columna
// oculta _credito. (nuevo)

/// <summary>Lo común de las vistas de la cobranza: los créditos del alcance y los nombres. (nuevo)</summary>
internal static class VistasDeCobranza
{
    public static async Task<(IReadOnlyDictionary<int, Domain.Entities.Lending.Loans.Loan> Creditos, IReadOnlyDictionary<int, PersonaRefDto> Personas)> CreditosAsync(
        IApplicationDbContext db, AmbitoDeInformeDeCartera ambito, IEnumerable<int> ids, CancellationToken ct)
    {
        var lista = ids.Distinct().ToList();
        var creditos = await ambito.Creditos(db).Where(l => lista.Contains(l.Id)).ToDictionaryAsync(l => l.Id, ct);
        var personas = await ReferenciasDeGobierno.PersonasAsync(db, creditos.Values.Select(c => c.DebtorPersonId), ct);
        return (creditos, personas);
    }

    public static ColumnaExportable Credito { get; } = new("_credito", Clave: "_credito");
}

// --------------------------------------------------------------------------------------------------- collection-stages --

/// <summary>
/// La vista <c>collection-stages</c> (K4, T0953): la cartera en cobranza al corte (<c>asOf</c>) por su etapa vigente —la última entrada hasta el corte—,
/// con los días de mora y el saldo de capital de la posición al corte, el gestor vigente y la última gestión. Filtros <c>stage</c> y <c>collector</c>
/// (el <c>PublicId</c> del usuario). (nuevo)
/// </summary>
public sealed record CollectionStagesReportQuery(FiltrosDeInformeDeCartera Filtros) : IRequest<Result<TablaExportable>>;

public sealed class CollectionStagesReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<CollectionStagesReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeCartera Vista = VistaDeInformeDeCartera.ConDatosDelDeudor(
        "collection-stages", "Cartera por etapa de cobranza",
        "Los créditos en cobranza al corte por su etapa: días de mora, saldo, gestor y última gestión.", "cartera-por-etapa",
        ["asOf", "branch", "line", "stage", "collector"], [], EntregaDeCartera.K4);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Etapa"), new("Crédito"), new("Deudor"), new("Días", TipoDeColumna.Entero), new("Saldo", TipoDeColumna.Moneda), new("Gestor"),
        new("Última gestión", TipoDeColumna.Fecha), VistasDeCobranza.Credito,
    ];

    public async Task<Result<TablaExportable>> Handle(CollectionStagesReportQuery request, CancellationToken ct)
    {
        var f = request.Filtros;
        var corte = f.ALaFecha(reloj.HoyLocal);
        var ambito = await AmbitoDeInformeDeCartera.LeerAsync(db, alcanceDeLaPeticion, f, ct);
        var delAlcance = ambito.Creditos(db).Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.WrittenOff).Select(l => l.Id);
        var entradas = (await db.CollectionStageHistory.AsNoTracking().Where(h => !h.IsDeleted && h.EnteredOn <= corte && delAlcance.Contains(h.LoanId)).ToListAsync(ct))
            .GroupBy(h => h.LoanId).ToDictionary(g => g.Key, g => g.OrderBy(h => h.EnteredOn).ThenBy(h => h.Id).Last())
            // K4, bloque D: el crédito cuya última entrada es la salida está fuera de cobranza al corte.
            .Where(e => !e.Value.EsSalida).ToDictionary(e => e.Key, e => e.Value);
        if (!string.IsNullOrWhiteSpace(f.Stage))
        {
            var etapa = f.Stage.Trim().ToUpperInvariant();
            entradas = entradas.Where(e => e.Value.StageCode == etapa).ToDictionary(e => e.Key, e => e.Value);
        }
        var (creditos, personas) = await VistasDeCobranza.CreditosAsync(db, ambito, entradas.Keys, ct);
        var ids = creditos.Keys.ToList();
        var posiciones = await AmbitoDeInformeDeCartera.PosicionesAlCorteAsync(db, ids, corte, ct);
        var gestores = await CobranzaDeCartera.GestorVigenteAsync(db, creditos.Values.Select(c => (c.Id, c.DebtorPersonId)).ToList(), ct);
        if (!string.IsNullOrWhiteSpace(f.Collector))
        {
            if (!Guid.TryParse(f.Collector, out var gestor)) return Result.Failure<TablaExportable>(FiltrosDeInformeDeCartera.FiltroInvalido("collector", f.Collector, []));
            var id = await db.Users.AsNoTracking().Where(u => u.PublicId == gestor).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct) ?? -1;
            ids = ids.Where(i => gestores.TryGetValue(i, out var a) && a.CollectorUserId == id).ToList();
        }
        var usuarios = await ReferenciasDeGobierno.UsuariosAsync(db, gestores.Values.Select(g => g.CollectorUserId), ct);
        var desfase = reloj.AhoraLocal.Offset;
        var ultimas = (await db.CollectionActions.AsNoTracking().Where(a => a.LoanId != null && ids.Contains(a.LoanId.Value) && !a.IsDeleted)
                .Select(a => new { LoanId = a.LoanId!.Value, a.OccurredAt }).ToListAsync(ct))
            .GroupBy(a => a.LoanId).ToDictionary(g => g.Key, g => DateOnly.FromDateTime(CobranzaDeCartera.ALocal(g.Max(a => a.OccurredAt), desfase)));
        var filas = ids.Select(i => (Entrada: entradas[i], Credito: creditos[i]))
            .OrderBy(x => x.Entrada.StageCode, StringComparer.Ordinal).ThenBy(x => x.Credito.Number, StringComparer.Ordinal)
            .Select(x => new FilaExportable(
            [
                x.Entrada.StageCode, x.Credito.Number, personas[x.Credito.DebtorPersonId].FullName, posiciones.GetValueOrDefault(x.Credito.Id)?.DaysPastDue ?? 0,
                posiciones.GetValueOrDefault(x.Credito.Id)?.Principal ?? 0m,
                gestores.TryGetValue(x.Credito.Id, out var g) ? ReferenciasDeGobierno.Usuario(usuarios, g.CollectorUserId)?.Name ?? string.Empty : string.Empty,
                ultimas.TryGetValue(x.Credito.Id, out var ultima) ? ultima : null, x.Credito.PublicId.ToString(),
            ])).ToList();
        return Result.Success(new TablaExportable("Cartera por etapa de cobranza", $"Al {corte:yyyy-MM-dd} · {ambito.Descripcion()}", Columnas, filas, null,
            ["Etapa: la última entrada del crédito hasta el corte. Días y saldo de capital: la posición diaria al corte. Gestor: el asignado vigente."]));
    }
}

// ------------------------------------------------------------------------------------------------------------- promises --

/// <summary>La vista <c>promises</c> (K4, T0953): las promesas de pago con su estado <b>derivado</b> a hoy; filtro propio <c>status</c>. (nuevo)</summary>
public sealed record PromisesReportQuery(FiltrosDeInformeDeCartera Filtros, string? Status = null) : IRequest<Result<TablaExportable>>;

public sealed class PromisesReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<PromisesReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeCartera Vista = VistaDeInformeDeCartera.ConDatosDelDeudor(
        "promises", "Promesas de pago", "Las promesas de pago con su estado a hoy: pendiente, cumplida o incumplida.", "promesas-de-pago",
        ["branch", "line"], ["status"], EntregaDeCartera.K4);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Crédito"), new("Deudor"), new("Fecha", TipoDeColumna.Fecha), new("Valor", TipoDeColumna.Moneda), new("Estado"), new("Gestor"), VistasDeCobranza.Credito,
    ];

    public async Task<Result<TablaExportable>> Handle(PromisesReportQuery request, CancellationToken ct)
    {
        PaymentPromiseStatus? filtro = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<PaymentPromiseStatus>(request.Status.Trim(), true, out var e) || !Enum.IsDefined(e))
                return Result.Failure<TablaExportable>(FiltrosDeInformeDeCartera.FiltroInvalido("status", request.Status, Enum.GetNames<PaymentPromiseStatus>()));
            filtro = e;
        }
        var hoy = reloj.HoyLocal;
        var ambito = await AmbitoDeInformeDeCartera.LeerAsync(db, alcanceDeLaPeticion, request.Filtros, ct);
        var delAlcance = ambito.Creditos(db).Select(l => l.Id);
        var promesas = await (from p in db.PaymentPromises.AsNoTracking()
                              join a in db.CollectionActions.AsNoTracking() on p.CollectionActionId equals a.Id
                              where !p.IsDeleted && delAlcance.Contains(p.LoanId)
                              select new { Promesa = p, a.Id, a.OccurredAt, a.CollectorUserId }).ToListAsync(ct);
        var gestionIds = promesas.Select(p => p.Id).ToList();
        var anuladas = (await db.CollectionActions.AsNoTracking().Where(a => a.VoidsActionId != null && gestionIds.Contains(a.VoidsActionId.Value))
            .Select(a => a.VoidsActionId!.Value).ToListAsync(ct)).ToHashSet();
        var (creditos, personas) = await VistasDeCobranza.CreditosAsync(db, ambito, promesas.Select(p => p.Promesa.LoanId), ct);
        var pagos = await CobranzaDeCartera.PagosVigentesAsync(db, creditos.Keys.ToList(), ct);
        var usuarios = await ReferenciasDeGobierno.UsuariosAsync(db, promesas.Select(p => p.CollectorUserId), ct);
        var filas = new List<FilaExportable>();
        foreach (var p in promesas.Where(p => !anuladas.Contains(p.Id)).OrderBy(p => p.Promesa.PromisedDate))
        {
            var estado = EstadoDePromesa.Calcular(p.Promesa.ParaEstado(p.OccurredAt, false), pagos.GetValueOrDefault(p.Promesa.LoanId) ?? [], hoy).Estado;
            if (filtro is { } e && estado != e) continue;
            var c = creditos[p.Promesa.LoanId];
            filas.Add(new FilaExportable([c.Number, personas[c.DebtorPersonId].FullName, p.Promesa.PromisedDate, p.Promesa.PromisedAmount, Texto(estado),
                ReferenciasDeGobierno.Usuario(usuarios, p.CollectorUserId)?.Name ?? string.Empty, c.PublicId.ToString()]));
        }
        return Result.Success(new TablaExportable("Promesas de pago", $"A hoy {hoy:yyyy-MM-dd} · {ambito.Descripcion()}", Columnas, filas, null,
            ["Cumplida: lo pagado después de la promesa con fecha valor hasta la prometida llega a lo prometido menos la tolerancia. Incumplida: venció sin eso."]));
    }

    private static string Texto(PaymentPromiseStatus? e) => e switch
    {
        PaymentPromiseStatus.Kept => "Cumplida",
        PaymentPromiseStatus.Broken => "Incumplida",
        _ => "Pendiente",
    };
}

// --------------------------------------------------------------------------------------------------- payment-agreements --

/// <summary>
/// La vista <c>payment-agreements</c> (K4, T0953): los acuerdos de pago con sus cuotas, las cubiertas por los pagos, las vencidas sin cubrir y su
/// estado (el cumplimiento calculado si está aprobado); filtro propio <c>status</c> (<c>PaymentAgreementStatus</c>). (nuevo)
/// </summary>
public sealed record PaymentAgreementsReportQuery(FiltrosDeInformeDeCartera Filtros, string? Status = null) : IRequest<Result<TablaExportable>>;

public sealed class PaymentAgreementsReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, IDateTimeService reloj)
    : IRequestHandler<PaymentAgreementsReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeCartera Vista = VistaDeInformeDeCartera.ConDatosDelDeudor(
        "payment-agreements", "Acuerdos de pago", "Los acuerdos de pago: cuotas, cuotas cumplidas, vencidas sin cubrir y estado.", "acuerdos-de-pago",
        ["branch", "line"], ["status"], EntregaDeCartera.K4);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Crédito"), new("Deudor"), new("Cuotas", TipoDeColumna.Entero), new("Cumplidas", TipoDeColumna.Entero), new("Vencidas", TipoDeColumna.Entero),
        new("Estado"), VistasDeCobranza.Credito,
    ];

    public async Task<Result<TablaExportable>> Handle(PaymentAgreementsReportQuery request, CancellationToken ct)
    {
        PaymentAgreementStatus? filtro = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<PaymentAgreementStatus>(request.Status.Trim(), true, out var e) || !Enum.IsDefined(e))
                return Result.Failure<TablaExportable>(FiltrosDeInformeDeCartera.FiltroInvalido("status", request.Status, Enum.GetNames<PaymentAgreementStatus>()));
            filtro = e;
        }
        var hoy = reloj.HoyLocal;
        var ambito = await AmbitoDeInformeDeCartera.LeerAsync(db, alcanceDeLaPeticion, request.Filtros, ct);
        var delAlcance = ambito.Creditos(db).Select(l => l.Id);
        var consulta = db.PaymentAgreements.AsNoTracking().Include(a => a.Installments).Where(a => !a.IsDeleted && delAlcance.Contains(a.LoanId));
        if (filtro is { } estado) consulta = consulta.Where(a => a.Status == estado);
        var acuerdos = await consulta.OrderByDescending(a => a.Id).ToListAsync(ct);
        var (creditos, personas) = await VistasDeCobranza.CreditosAsync(db, ambito, acuerdos.Select(a => a.LoanId), ct);
        var pagos = await CobranzaDeCartera.PagosVigentesAsync(db, creditos.Keys.ToList(), ct);
        var desfase = reloj.AhoraLocal.Offset;
        var filas = acuerdos.Select(a =>
        {
            var c = creditos[a.LoanId];
            var r = AcuerdosDePago.Cumplimiento(a, pagos.GetValueOrDefault(a.LoanId) ?? [], hoy, desfase);
            var cubiertas = r?.CuotasCubiertas ?? 0;
            var vencidas = a.Status == PaymentAgreementStatus.Approved ? Math.Max(0, a.Installments.Count(i => i.DueDate < hoy) - cubiertas) : 0;
            var texto = r is null ? Texto(a.Status) : r.Estado switch
            {
                PaymentAgreementPerformance.Fulfilled => "Cumplido",
                PaymentAgreementPerformance.Broken => "Incumplido",
                _ => "Al día",
            };
            return new FilaExportable([c.Number, personas[c.DebtorPersonId].FullName, a.InstallmentCount, cubiertas, vencidas, texto, c.PublicId.ToString()]);
        }).ToList();
        return Result.Success(new TablaExportable("Acuerdos de pago", $"A hoy {hoy:yyyy-MM-dd} · {ambito.Descripcion()}", Columnas, filas, null,
            ["Cumplidas: cuotas que los pagos desde la aprobación cubren en orden. Vencidas: cuotas que vencieron y siguen sin cubrir."]));
    }

    private static string Texto(PaymentAgreementStatus e) => e switch
    {
        PaymentAgreementStatus.Proposed => "Propuesto",
        PaymentAgreementStatus.Rejected => "Rechazado",
        PaymentAgreementStatus.Cancelled => "Anulado",
        _ => "Aprobado",
    };
}

// -------------------------------------------------------------------------------------------------------- prior-notices --

/// <summary>
/// La vista <c>prior-notices</c> (K4, T0953): los avisos previos al reporte negativo por episodio, con las comunicaciones exigidas y con constancia,
/// el día de la constancia y la fecha más temprana de reporte (el estado derivado de <see cref="AvisoPrevioPorEpisodio"/>); filtro propio <c>status</c>
/// (<c>PriorNoticeStatus</c>). (nuevo)
/// </summary>
public sealed record PriorNoticesReportQuery(FiltrosDeInformeDeCartera Filtros, string? Status = null) : IRequest<Result<TablaExportable>>;

public sealed class PriorNoticesReportQueryHandler(IApplicationDbContext db, IAlcanceDeCartera alcanceDeLaPeticion, ColaDeMensajesAlDeudor cola,
    IDateTimeService reloj) : IRequestHandler<PriorNoticesReportQuery, Result<TablaExportable>>
{
    public static readonly VistaDeInformeDeCartera Vista = VistaDeInformeDeCartera.ConDatosDelDeudor(
        "prior-notices", "Avisos previos al reporte", "Los avisos previos por episodio de mora: comunicaciones, constancia y fecha más temprana de reporte.",
        "avisos-previos", ["branch", "line"], ["status"], EntregaDeCartera.K4);

    public static readonly IReadOnlyList<ColumnaExportable> Columnas =
    [
        new("Crédito"), new("Deudor"), new("Episodio", TipoDeColumna.Fecha), new("Comunicaciones"), new("Constancia", TipoDeColumna.Fecha),
        new("Fecha más temprana de reporte", TipoDeColumna.Fecha), VistasDeCobranza.Credito,
    ];

    public async Task<Result<TablaExportable>> Handle(PriorNoticesReportQuery request, CancellationToken ct)
    {
        PriorNoticeStatus? filtro = null;
        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<PriorNoticeStatus>(request.Status.Trim(), true, out var e) || !Enum.IsDefined(e))
                return Result.Failure<TablaExportable>(FiltrosDeInformeDeCartera.FiltroInvalido("status", request.Status, Enum.GetNames<PriorNoticeStatus>()));
            filtro = e;
        }
        var ambito = await AmbitoDeInformeDeCartera.LeerAsync(db, alcanceDeLaPeticion, request.Filtros, ct);
        var delAlcance = ambito.Creditos(db).Select(l => l.Id);
        var avisos = await db.PriorNotices.AsNoTracking().Where(n => !n.IsDeleted && delAlcance.Contains(n.LoanId)).OrderByDescending(n => n.EpisodeStartDate)
            .ToListAsync(ct);
        var dtos = await AvisosPrevios.DtosAsync(db, avisos, cola.EnProduccion, reloj.AhoraLocal.Offset, ct);
        var personas = dtos.ToDictionary(d => d.PublicId, d => d.Person.FullName);
        var filas = dtos.Where(d => filtro is not { } e || d.Status == e).Select(d => new FilaExportable(
        [
            d.Loan.Number, personas[d.PublicId], d.EpisodeStart, $"{d.Communications.Count(c => c.Evidence)} de {d.RequiredCommunications}", d.EvidenceDate,
            d.EarliestReportDate, d.Loan.LoanPublicId.ToString(),
        ])).ToList();
        return Result.Success(new TablaExportable("Avisos previos al reporte", $"A hoy {reloj.HoyLocal:yyyy-MM-dd} · {ambito.Descripcion()}", Columnas, filas, null,
            ["Comunicaciones: las que tienen constancia de las que exige el episodio (dos en días distintos bajo el umbral). Sin constancia completa, el dato negativo no se reporta."]));
    }
}
