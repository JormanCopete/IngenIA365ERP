using System.Globalization;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Treasury.Tareas;

/// <summary>
/// La vigilancia de Tesorería (feature 015, US11, T0160; FR-019, FR-123, FR-174; SC-012; DT §2.5): <b>sólo alerta</b>, nunca registra ni cambia un resultado
/// (<c>NingunProcesoAplicaPagosNiInformaResultados</c>). En T0 vigila las órdenes enviadas sin resultado a <c>Giros.DiasSinResultado</c> días hábiles
/// (<c>Tesoreria.OrdenSinResultado</c>); los cheques sin cobrar, las remesas sin confirmar, las partidas conciliatorias antiguas, el cuadre provisional y la política
/// en «advertir» los suma cada fase a esta misma tarea. (nuevo)
/// </summary>
public sealed class VigilanciaDeTesoreria(IApplicationDbContext db, ParametrosDeTesoreria parametros, ICalendarioHabil calendario, IAlertas alertas, IDateTimeService reloj)
{
    /// <summary>Alerta cada orden enviada sin resultado más días hábiles que el parámetro. Devuelve cuántas alertó.</summary>
    public async Task<int> OrdenesSinResultadoAsync(CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var dias = await parametros.EnteroAsync(ParametrosDeRecaudoYTesoreria.GirosDiasSinResultado, hoy, ct);
        if (dias is not { } limite || limite <= 0) return 0;
        var enviadas = await (from o in db.PaymentOrders.AsNoTracking()
                              join i in db.PaymentBatchItems.AsNoTracking() on o.Id equals i.PaymentOrderId
                              join b in db.PaymentBatches.AsNoTracking() on i.PaymentBatchId equals b.Id
                              where o.Status == PaymentOrderState.Sent && i.IsLive && b.SentAt != null
                              select new { o.PublicId, o.Number, o.BeneficiaryName, o.NetAmount, b.SentAt, Lote = b.Number }).ToListAsync(ct);
        if (enviadas.Count == 0) return 0;
        var desde = DateOnly.FromDateTime(enviadas.Min(e => e.SentAt!.Value).ToLocalTime());
        var habil = await calendario.ObtenerAsync(desde, hoy, DiasDeDescanso.SabadoYDomingo, ct: ct);
        var alertadas = 0;
        foreach (var e in enviadas)
        {
            var enviada = DateOnly.FromDateTime(e.SentAt!.Value.ToLocalTime());
            if (habil.Contar(enviada, hoy) < limite) continue;
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.TesoreriaOrdenSinResultado, "Orden de pago sin resultado",
                string.Create(CultureInfo.InvariantCulture,
                    $"La orden {e.Number} ({e.BeneficiaryName}, {e.NetAmount:N0}) del lote {e.Lote} se envió el {enviada:dd/MM/yyyy} y no tiene resultado: cárguelo desde la respuesta del banco o regístrelo con su soporte."),
                "PaymentOrder", e.PublicId, DedupKey: $"Tesoreria.OrdenSinResultado:{e.PublicId:N}", ModuloDeOrigen: "Treasury"), ct);
            alertadas++;
        }
        return alertadas;
    }

    /// <summary>
    /// Feature 015, US19 (T0228; FR-136): alerta cada cheque entregado y no cobrado a <c>Cheques.DiasSinCobrar</c> días (<c>Tesoreria.ChequeSinCobrar</c>). No lo anula: lo
    /// anula una persona con permiso. Devuelve cuántos alertó.
    /// </summary>
    public async Task<int> ChequesSinCobrarAsync(CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var dias = await parametros.EnteroAsync(ParametrosDeRecaudoYTesoreria.ChequesDiasSinCobrar, hoy, ct) ?? 30;
        if (dias <= 0) return 0;
        var limite = hoy.AddDays(-dias);
        var cheques = await (from c in db.IssuedChecks.AsNoTracking()
                             join o in db.PaymentOrders.AsNoTracking() on c.PaymentOrderId equals o.Id
                             where c.Status == IssuedCheckState.Delivered && c.DeliveredOn != null && c.DeliveredOn <= limite
                             select new { c.PublicId, c.CheckNumber, c.Amount, c.DeliveredOn, o.Number, o.BeneficiaryName }).ToListAsync(ct);
        foreach (var c in cheques)
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.TesoreriaChequeSinCobrar, "Cheque girado sin cobrar",
                string.Create(CultureInfo.InvariantCulture,
                    $"El cheque {c.CheckNumber} de la orden {c.Number} ({c.BeneficiaryName}, {c.Amount:N0}) se entregó el {c.DeliveredOn:dd/MM/yyyy} y no se ha cobrado: confírmelo con el extracto o anúlelo como «no cobrado»."),
                "IssuedCheck", c.PublicId, DedupKey: $"Tesoreria.ChequeSinCobrar:{c.PublicId:N}", ModuloDeOrigen: "Treasury"), ct);
        return cheques.Count;
    }

    /// <summary>
    /// Feature 015, US19 (T0228; FR-105): alerta cada remesa en tránsito más de <c>Remesas.DiasParaConfirmar</c> días (<c>Tesoreria.RemesaSinConfirmar</c>). Devuelve
    /// cuántas alertó.
    /// </summary>
    /// <summary>
    /// Feature 015, US8 (T0262; FR-221): mientras <c>Contabilidad.FaltaDeRegla</c> de Tesorería esté en «advertir», la alerta
    /// <c>Tesoreria.PoliticaContableEnAdvertir</c> se repite cada día (una por día).
    /// </summary>
    public async Task<int> PoliticaContableEnAdvertirAsync(CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var politica = await parametros.TextoAsync(ParametrosDeRecaudoYTesoreria.ContabilidadFaltaDeRegla, hoy, ct);
        if (!string.Equals(politica, "Advertir", StringComparison.OrdinalIgnoreCase)) return 0;
        await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.TesoreriaPoliticaContableEnAdvertir, "Política contable de Tesorería en «advertir»",
            "Tesorería confirma operaciones aunque la matriz contable no tenga su regla: complete la matriz y vuelva la política a «bloquear».",
            DedupKey: string.Create(CultureInfo.InvariantCulture, $"Tesoreria.PoliticaContableEnAdvertir:{hoy:yyyy-MM-dd}"), ModuloDeOrigen: "Treasury"), ct);
        return 1;
    }

    public async Task<int> RemesasSinConfirmarAsync(CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var dias = await parametros.EnteroAsync(ParametrosDeRecaudoYTesoreria.RemesasDiasParaConfirmar, hoy, ct) ?? 2;
        if (dias < 0) return 0;
        var limite = hoy.AddDays(-dias);
        var remesas = await db.CashRemittances.AsNoTracking().Where(r => r.Status == RemittanceState.InTransit && r.DispatchDate < limite)
            .Select(r => new { r.PublicId, r.Number, r.Amount, r.DispatchDate, r.Direction }).ToListAsync(ct);
        foreach (var r in remesas)
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.TesoreriaRemesaSinConfirmar, "Remesa sin confirmar",
                string.Create(CultureInfo.InvariantCulture,
                    $"La remesa {r.Number} por {r.Amount:N0} salió el {r.DispatchDate:dd/MM/yyyy} y sigue en tránsito: confírmela con el soporte del banco o con el extracto."),
                "CashRemittance", r.PublicId, DedupKey: $"Tesoreria.RemesaSinConfirmar:{r.PublicId:N}", ModuloDeOrigen: "Treasury"), ct);
        return remesas.Count;
    }

    /// <summary>
    /// Feature 015, US25 (T0279; FR-217): alerta cada partida conciliatoria de una conciliación vigente con más de <c>Conciliacion.DiasPartidaAntigua</c> días a hoy
    /// (<c>Tesoreria.PartidaConciliatoriaAntigua</c>), una vez por partida. Sólo informa. Devuelve cuántas alertó.
    /// </summary>
    public async Task<int> PartidasConciliatoriasAntiguasAsync(CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var dias = await parametros.EnteroAsync(ParametrosDeRecaudoYTesoreria.ConciliacionDiasPartidaAntigua, hoy, ct);
        if (dias is not { } limite || limite <= 0) return 0;
        var desde = hoy.AddDays(-limite);
        var partidas = await (from i in db.TreasuryReconcilingItems.AsNoTracking()
                              join c in db.TreasuryReconciliations.AsNoTracking() on i.ReconciliationId equals c.Id
                              join a in db.OwnBankAccounts.AsNoTracking() on c.BankAccountId equals a.Id
                              where !i.IsVoided && i.Since < desde && (c.Status == ReconciliationState.Open || c.Status == ReconciliationState.Closed)
                              select new { i.PublicId, i.Kind, i.Amount, i.Since, Cuenta = a.Code, c.PeriodYear, c.PeriodMonth, Conciliacion = c.PublicId }).ToListAsync(ct);
        foreach (var p in partidas)
            await alertas.LevantarAsync(new AlertaALevantar(TiposDeAlerta.TesoreriaPartidaConciliatoriaAntigua, "Partida conciliatoria antigua",
                string.Create(CultureInfo.InvariantCulture,
                    $"La partida «{Reconciliation.ConciliacionMinima.Clase(p.Kind)}» de {p.Amount:N0} de la cuenta {p.Cuenta} (conciliación {p.PeriodYear}-{p.PeriodMonth:00}) está abierta desde el {p.Since:dd/MM/yyyy}: regístrela en los libros o confírmela con el banco."),
                "Reconciliation", p.Conciliacion, DedupKey: $"Tesoreria.PartidaConciliatoriaAntigua:{p.PublicId:N}", ModuloDeOrigen: "Treasury"), ct);
        return partidas.Count;
    }
}

/// <summary>La tarea <c>tesoreria.vigilancia</c>: una vez al día (cada <see cref="Intervalo"/>), por cooperativa. (nuevo)</summary>
public sealed class TareaDeVigilanciaDeTesoreria : ITareaProgramada
{
    public const string NombreDeLaTarea = "tesoreria.vigilancia";

    public static readonly TimeSpan Intervalo = TimeSpan.FromHours(6);

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) => ultimaCorrida is null || ahoraLocal - ultimaCorrida.Value >= Intervalo;

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct)
    {
        var vigilancia = servicios.GetRequiredService<VigilanciaDeTesoreria>();
        await vigilancia.OrdenesSinResultadoAsync(ct);
        await vigilancia.ChequesSinCobrarAsync(ct);
        await vigilancia.RemesasSinConfirmarAsync(ct);
        await vigilancia.PoliticaContableEnAdvertirAsync(ct);
        await vigilancia.PartidasConciliatoriasAntiguasAsync(ct);
    }
}
