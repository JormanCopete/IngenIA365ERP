using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Collections;

/// <summary>
/// Un pago <b>vigente</b> (no reversado) del crédito, como lo miran la promesa y el acuerdo: su fecha valor, su monto y cuándo se registró
/// (UTC). Quien llama trae sólo los vigentes. (nuevo)
/// </summary>
public sealed record PagoParaCobranza(DateOnly FechaValor, decimal Monto, DateTime RegistradoEn);

/// <summary>
/// Lo que el cálculo mira de una promesa (<c>LND_PaymentPromises</c>, data-model §26.8): cuándo se registró su gestión (UTC), la fecha y el
/// valor prometidos, la tolerancia sellada y si su gestión fue anulada. (nuevo)
/// </summary>
public sealed record PromesaParaEstado(DateTime RegistradaEn, DateOnly FechaPrometida, decimal ValorPrometido, decimal Tolerancia, bool GestionAnulada = false);

/// <summary>
/// El estado derivado de una promesa: <see cref="Estado"/> nulo si está <see cref="Anulada"/>; lo abonado que cuenta y el día (fecha valor) en
/// que se alcanzó lo prometido menos la tolerancia. (nuevo)
/// </summary>
public sealed record EstadoDePromesaCalculado(PaymentPromiseStatus? Estado, bool Anulada, decimal Abonado, DateOnly? CumplidaEl);

/// <summary>
/// Motor puro del estado de una promesa de pago (feature 013, K4, T0937; FR-119, data-model §26.8). La promesa no guarda estado: se deriva.
/// <list type="bullet">
/// <item><b>Anulada</b> si su gestión fue anulada (una corrección es otra gestión).</item>
/// <item><b>Kept</b> si los pagos vigentes registrados <b>después</b> de la promesa con fecha valor ≤ la prometida suman al menos
/// <c>valor − tolerancia</c> (y algo se pagó).</item>
/// <item><b>Broken</b> si no y la fecha prometida ya pasó (hoy &gt; prometida): ese día todavía se puede pagar.</item>
/// <item><b>Pending</b> en otro caso.</item>
/// </list>
/// (nuevo)
/// </summary>
public static class EstadoDePromesa
{
    public static EstadoDePromesaCalculado Calcular(PromesaParaEstado promesa, IEnumerable<PagoParaCobranza> pagos, DateOnly hoy)
    {
        ArgumentNullException.ThrowIfNull(promesa);
        ArgumentNullException.ThrowIfNull(pagos);
        if (promesa.ValorPrometido <= 0m)
            throw new ArgumentOutOfRangeException(nameof(promesa), promesa.ValorPrometido, "Lo prometido es mayor que cero.");
        if (promesa.Tolerancia < 0m)
            throw new ArgumentOutOfRangeException(nameof(promesa), promesa.Tolerancia, "La tolerancia no puede ser negativa.");

        var cuentan = pagos
            .Where(p => p.RegistradoEn > promesa.RegistradaEn && p.FechaValor <= promesa.FechaPrometida && p.Monto > 0m)
            .OrderBy(p => p.FechaValor).ThenBy(p => p.RegistradoEn)
            .ToList();
        var abonado = cuentan.Sum(p => p.Monto);

        if (promesa.GestionAnulada)
            return new EstadoDePromesaCalculado(null, true, abonado, null);

        var meta = Math.Max(promesa.ValorPrometido - promesa.Tolerancia, 0m);
        var acumulado = 0m;
        foreach (var p in cuentan)
        {
            acumulado += p.Monto;
            if (acumulado >= meta)
                return new EstadoDePromesaCalculado(PaymentPromiseStatus.Kept, false, abonado, p.FechaValor);
        }

        var estado = hoy > promesa.FechaPrometida ? PaymentPromiseStatus.Broken : PaymentPromiseStatus.Pending;
        return new EstadoDePromesaCalculado(estado, false, abonado, null);
    }
}
