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
}

/// <summary>La tarea <c>tesoreria.vigilancia</c>: una vez al día (cada <see cref="Intervalo"/>), por cooperativa. (nuevo)</summary>
public sealed class TareaDeVigilanciaDeTesoreria : ITareaProgramada
{
    public const string NombreDeLaTarea = "tesoreria.vigilancia";

    public static readonly TimeSpan Intervalo = TimeSpan.FromHours(6);

    public string Nombre => NombreDeLaTarea;

    public bool DebeCorrer(DateTimeOffset ahoraLocal, DateTimeOffset? ultimaCorrida) => ultimaCorrida is null || ahoraLocal - ultimaCorrida.Value >= Intervalo;

    public async Task EjecutarAsync(IServiceProvider servicios, CancellationToken ct) =>
        await servicios.GetRequiredService<VigilanciaDeTesoreria>().OrdenesSinResultadoAsync(ct);
}
