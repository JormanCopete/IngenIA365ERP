using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Collections;

/// <summary>Una cuota del calendario de un acuerdo de pago (<c>LND_PaymentAgreementInstallments</c>, data-model §26.9). (nuevo)</summary>
public sealed record CuotaDeAcuerdo(int Numero, DateOnly Vence, decimal Valor);

/// <summary>
/// El cumplimiento derivado de un acuerdo aprobado: el estado, lo pagado que cuenta, el total del calendario, cuántas cuotas cubren los pagos
/// (acumulando en orden) y, si se incumplió, la primera cuota incumplida y el día en que quedó incumplida (el siguiente a su vencimiento). (nuevo)
/// </summary>
public sealed record ResultadoDeAcuerdo(
    PaymentAgreementPerformance Estado, decimal Pagado, decimal Total, int CuotasCubiertas, int? CuotaIncumplida, DateOnly? IncumplidoEl);

/// <summary>
/// Motor puro del cumplimiento de un acuerdo de pago (feature 013, K4, T0937; FR-120, data-model §26.9). El acuerdo no guarda cumplimiento:
/// se deriva de los pagos vigentes con fecha valor desde su aprobación.
/// <list type="number">
/// <item><b>Broken</b> si hay una cuota vencida (vence antes de hoy) cuyo acumulado del calendario hasta ella no lo cubren los pagos con
/// fecha valor hasta su vencimiento. Se queda así: incumplir es un hecho de ese día, aunque después se pague (el ciclo diario devuelve el
/// crédito a su etapa al detectarlo).</item>
/// <item><b>Fulfilled</b> si no y los pagos cubren el total.</item>
/// <item><b>Current</b> en otro caso.</item>
/// </list>
/// (nuevo)
/// </summary>
public static class CumplimientoDeAcuerdo
{
    public static ResultadoDeAcuerdo Calcular(IReadOnlyList<CuotaDeAcuerdo> cuotas, DateOnly aprobadoEl, IEnumerable<PagoParaCobranza> pagos, DateOnly hoy)
    {
        ArgumentNullException.ThrowIfNull(cuotas);
        ArgumentNullException.ThrowIfNull(pagos);
        if (cuotas.Count == 0) throw new ArgumentException("Un acuerdo tiene al menos una cuota.", nameof(cuotas));

        var calendario = cuotas.OrderBy(c => c.Numero).ToList();
        var cuentan = pagos.Where(p => p.FechaValor >= aprobadoEl && p.Monto > 0m).ToList();
        var total = calendario.Sum(c => c.Valor);
        var pagado = cuentan.Sum(p => p.Monto);

        var cubiertas = 0;
        var acumuladoCalendario = 0m;
        foreach (var c in calendario)
        {
            acumuladoCalendario += c.Valor;
            if (pagado >= acumuladoCalendario) cubiertas++;
        }

        acumuladoCalendario = 0m;
        foreach (var c in calendario)
        {
            acumuladoCalendario += c.Valor;
            if (c.Vence >= hoy) break;
            var pagadoAlVencer = cuentan.Where(p => p.FechaValor <= c.Vence).Sum(p => p.Monto);
            if (pagadoAlVencer < acumuladoCalendario)
                return new ResultadoDeAcuerdo(PaymentAgreementPerformance.Broken, pagado, total, cubiertas, c.Numero, c.Vence.AddDays(1));
        }

        var estado = pagado >= total ? PaymentAgreementPerformance.Fulfilled : PaymentAgreementPerformance.Current;
        return new ResultadoDeAcuerdo(estado, pagado, total, cubiertas, null, null);
    }
}
