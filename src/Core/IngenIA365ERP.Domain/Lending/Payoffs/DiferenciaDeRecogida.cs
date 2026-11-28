using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Payoffs;

/// <summary>
/// La diferencia entre lo proyectado en la solicitud y lo que se recogería al desembolsar (feature 028, T008, R9): el total, cada concepto («ahora −
/// proyectado») y sus causas. (nuevo)
/// </summary>
/// <param name="Total">Lo de ahora menos lo proyectado: positivo, se recoge más (el neto baja); negativo, se recoge menos (el neto sube).</param>
/// <param name="PorConcepto">Cada concepto, ahora menos proyectado.</param>
/// <param name="Causa">Las causas que se deducen de los conceptos; <see cref="PayoffDifferenceCause.None"/> si no hay diferencia.</param>
public sealed record ResultadoDeDiferencia(decimal Total, DesgloseDeRecogida PorConcepto, PayoffDifferenceCause Causa)
{
    /// <summary>Si algún concepto cambió (aunque el total sea el mismo) o el producto ya estaba cerrado.</summary>
    public bool HayDiferencia => Causa != PayoffDifferenceCause.None;
}

/// <summary>
/// El comparador puro de dos desgloses de recogida (feature 028, T008, R9). Las causas salen de los conceptos con reglas simples, en este orden y
/// combinables (son banderas):
/// <list type="number">
/// <item>Producto cerrado (lo dice quien llama): <see cref="PayoffDifferenceCause.ProductClosed"/> y nada más; lo de ahora es cero.</item>
/// <item>Capital menor que el proyectado, o anticipo o saldo a favor mayor: <see cref="PayoffDifferenceCause.Payments"/> (entró un pago: a capital,
/// adelantado o de más).</item>
/// <item>Con el capital igual, un concepto que se causa distinto —interés corriente, devuelto o cobrado por proporcionales, seguro o
/// administración—: <see cref="PayoffDifferenceCause.Accrual"/>. Con el capital menor, esos cambios ya los explica el pago.</item>
/// <item>Mora distinta: <see cref="PayoffDifferenceCause.Arrears"/>.</item>
/// <item>Cualquier otro concepto distinto que ninguna regla explicó (capital mayor, otros cargos, anticipo o saldo a favor menor):
/// <see cref="PayoffDifferenceCause.Other"/>.</item>
/// </list>
/// <see cref="PayoffDifferenceCause.CurrentInstallment"/> (cambió la cuota causada o su envío) y <see cref="PayoffDifferenceCause.Parameters"/>
/// (cambió un parámetro entre la proyección y el desembolso) no se deducen de los conceptos: las agrega quien compara, que sabe qué cambió. (nuevo)
/// </summary>
public static class DiferenciaDeRecogida
{
    /// <summary>Compara lo proyectado con lo de ahora.</summary>
    /// <param name="proyectado">Lo guardado en la solicitud.</param>
    /// <param name="actual">Lo que se recogería ahora.</param>
    /// <param name="productoCerrado">El producto ya se canceló por otro camino: no se recoge y lo de ahora es cero.</param>
    public static ResultadoDeDiferencia Comparar(DesgloseDeRecogida proyectado, DesgloseDeRecogida actual, bool productoCerrado = false)
    {
        ArgumentNullException.ThrowIfNull(proyectado);
        ArgumentNullException.ThrowIfNull(actual);

        if (productoCerrado)
        {
            var sinNada = DesgloseDeRecogida.Vacio.Menos(proyectado);
            return new ResultadoDeDiferencia(-proyectado.Total, sinNada, PayoffDifferenceCause.ProductClosed);
        }

        var d = actual.Menos(proyectado);
        var causa = PayoffDifferenceCause.None;
        var explicado = new HashSet<string>(StringComparer.Ordinal);

        var capitalMenor = d.Capital < 0m;
        if (capitalMenor || d.Anticipo > 0m || d.SaldoAFavor > 0m)
        {
            causa |= PayoffDifferenceCause.Payments;
            if (capitalMenor)
            {
                explicado.Add(nameof(DesgloseDeRecogida.Capital));
                // Un pago a capital también cambia lo que se causa sobre él: lo explica el pago.
                explicado.UnionWith(Causados);
            }
            if (d.Anticipo > 0m) explicado.Add(nameof(DesgloseDeRecogida.Anticipo));
            if (d.SaldoAFavor > 0m) explicado.Add(nameof(DesgloseDeRecogida.SaldoAFavor));
        }

        if (d.Capital == 0m && Valores(d).Where(c => Causados.Contains(c.Concepto)).Any(c => c.Valor != 0m))
        {
            causa |= PayoffDifferenceCause.Accrual;
            explicado.UnionWith(Causados);
        }

        if (d.Mora != 0m)
        {
            causa |= PayoffDifferenceCause.Arrears;
            explicado.Add(nameof(DesgloseDeRecogida.Mora));
        }

        if (Valores(d).Any(c => c.Valor != 0m && !explicado.Contains(c.Concepto)))
            causa |= PayoffDifferenceCause.Other;

        return new ResultadoDeDiferencia(actual.Total - proyectado.Total, d, causa);
    }

    /// <summary>Los conceptos que se causan con el paso de los días.</summary>
    private static readonly HashSet<string> Causados = new(StringComparer.Ordinal)
    {
        nameof(DesgloseDeRecogida.InteresCorriente), nameof(DesgloseDeRecogida.InteresDevuelto),
        nameof(DesgloseDeRecogida.InteresCobradoPorProporcionales), nameof(DesgloseDeRecogida.Seguro), nameof(DesgloseDeRecogida.Administracion),
    };

    private static IEnumerable<(string Concepto, decimal Valor)> Valores(DesgloseDeRecogida d) =>
    [
        (nameof(DesgloseDeRecogida.Capital), d.Capital),
        (nameof(DesgloseDeRecogida.InteresCorriente), d.InteresCorriente),
        (nameof(DesgloseDeRecogida.InteresDevuelto), d.InteresDevuelto),
        (nameof(DesgloseDeRecogida.InteresCobradoPorProporcionales), d.InteresCobradoPorProporcionales),
        (nameof(DesgloseDeRecogida.Mora), d.Mora),
        (nameof(DesgloseDeRecogida.Seguro), d.Seguro),
        (nameof(DesgloseDeRecogida.Administracion), d.Administracion),
        (nameof(DesgloseDeRecogida.OtrosCargos), d.OtrosCargos),
        (nameof(DesgloseDeRecogida.Anticipo), d.Anticipo),
        (nameof(DesgloseDeRecogida.SaldoAFavor), d.SaldoAFavor),
    ];
}
