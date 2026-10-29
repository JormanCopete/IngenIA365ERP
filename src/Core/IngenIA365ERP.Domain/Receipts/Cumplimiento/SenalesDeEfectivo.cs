namespace IngenIA365ERP.Domain.Receipts.Cumplimiento;

/// <summary>Una transacción en efectivo de Recaudo para las señales: la persona (titular o pagador), quien pagó, la fecha y el efectivo con su signo (la anulación resta). (nuevo)</summary>
public sealed record TransaccionDeEfectivo(int PersonId, int PayerPersonId, DateOnly Fecha, decimal Efectivo);

/// <summary>
/// Los parámetros de las señales con la versión que los fijó (FR-097): el umbral de la transacción individual, el de las múltiples del mes, el de la declaración de origen de
/// fondos, el fraccionamiento «operaciones;días», cuántos titulares distintos hacen frecuente a un pagador tercero y cuántas partidas abiertas repiten a un consignante. (nuevo)
/// </summary>
public sealed record ParametrosDeSenales(
    decimal UmbralIndividual,
    decimal UmbralMultiples,
    decimal UmbralDeclaracion,
    int FraccionamientoOperaciones,
    int FraccionamientoDias,
    int TercerosFrecuentes,
    int ConsignanteRepetido,
    string? Version = null);

/// <summary>Las señales de efectivo (data-model §7.2). Se guardan como int y un valor nunca se renumera. (nuevo)</summary>
public enum SenalDeEfectivo
{
    Accumulated = 1,
    Fragmentation = 2,
    FrequentThirdPartyPayer = 3,
    RepeatedUnidentifiedDepositor = 4,
    BindingListMatch = 5,
    NonBindingListMatch = 6,
}

/// <summary>Una señal detectada: de qué persona (o consignante), la ventana, cuántas operaciones y cuánto. (nuevo)</summary>
public sealed record SenalDetectada(SenalDeEfectivo Senal, int? PersonId, string? Consignante, DateOnly Desde, DateOnly Hasta, int Cantidad, decimal Total);

/// <summary>
/// El motor puro de las señales de efectivo de Recaudo (feature 015, US14, T0238; R-24; FR-095, FR-202): no lee ni escribe nada, recibe las transacciones y los parámetros y
/// devuelve las señales. <b>Sólo informa</b>: quien lo llama alerta al oficial; ninguna señal bloquea (las listas vinculantes sí, en otro sitio).
/// <list type="bullet">
/// <item><b>Acumulado</b>: el efectivo de la persona en el mes calendario de <c>hoy</c> llega al umbral de las transacciones múltiples;</item>
/// <item><b>Fraccionamiento</b>: en la ventana de <c>días</c> que termina hoy, al menos <c>operaciones</c> pagos en efectivo de la persona, cada uno por debajo del umbral de
/// declaración, que juntos lo alcanzan;</item>
/// <item><b>Pagador tercero frecuente</b>: en el mes, el pagador entregó efectivo para al menos <c>TercerosFrecuentes</c> titulares distintos de él;</item>
/// <item><b>Consignante no identificado repetido</b>: el consignante tiene al menos <c>ConsignanteRepetido</c> partidas por identificar abiertas.</item>
/// </list>
/// (nuevo)
/// </summary>
public static class SenalesDeEfectivo
{
    public static IReadOnlyList<SenalDetectada> Evaluar(int personId, IReadOnlyList<TransaccionDeEfectivo> transacciones, DateOnly hoy, ParametrosDeSenales p)
    {
        ArgumentNullException.ThrowIfNull(transacciones);
        ArgumentNullException.ThrowIfNull(p);
        var senales = new List<SenalDetectada>();
        var inicioDelMes = new DateOnly(hoy.Year, hoy.Month, 1);
        var delMes = transacciones.Where(t => t.PersonId == personId && t.Fecha >= inicioDelMes && t.Fecha <= hoy).ToList();
        var acumulado = delMes.Sum(t => t.Efectivo);
        if (p.UmbralMultiples > 0m && acumulado >= p.UmbralMultiples)
            senales.Add(new SenalDetectada(SenalDeEfectivo.Accumulated, personId, null, inicioDelMes, hoy, delMes.Count(t => t.Efectivo > 0m), acumulado));

        if (p.FraccionamientoOperaciones > 0 && p.FraccionamientoDias > 0 && p.UmbralDeclaracion > 0m)
        {
            var desde = hoy.AddDays(1 - p.FraccionamientoDias);
            var enVentana = transacciones.Where(t => t.PersonId == personId && t.Fecha >= desde && t.Fecha <= hoy && t.Efectivo > 0m && t.Efectivo < p.UmbralDeclaracion)
                .ToList();
            var total = enVentana.Sum(t => t.Efectivo);
            if (enVentana.Count >= p.FraccionamientoOperaciones && total >= p.UmbralDeclaracion)
                senales.Add(new SenalDetectada(SenalDeEfectivo.Fragmentation, personId, null, desde, hoy, enVentana.Count, total));
        }

        if (p.TercerosFrecuentes > 0)
        {
            var comoPagador = transacciones.Where(t => t.PayerPersonId == personId && t.PersonId != personId && t.Fecha >= inicioDelMes && t.Fecha <= hoy && t.Efectivo > 0m)
                .ToList();
            var titulares = comoPagador.Select(t => t.PersonId).Distinct().Count();
            if (titulares >= p.TercerosFrecuentes)
                senales.Add(new SenalDetectada(SenalDeEfectivo.FrequentThirdPartyPayer, personId, null, inicioDelMes, hoy, comoPagador.Count, comoPagador.Sum(t => t.Efectivo)));
        }
        return senales;
    }

    /// <summary>La señal del consignante no identificado repetido: sus partidas por identificar abiertas a hoy.</summary>
    public static SenalDetectada? Consignante(string consignante, IReadOnlyList<(DateOnly Fecha, decimal Valor)> partidasAbiertas, DateOnly hoy, ParametrosDeSenales p)
    {
        ArgumentNullException.ThrowIfNull(partidasAbiertas);
        ArgumentNullException.ThrowIfNull(p);
        if (string.IsNullOrWhiteSpace(consignante) || p.ConsignanteRepetido <= 0 || partidasAbiertas.Count < p.ConsignanteRepetido) return null;
        return new SenalDetectada(SenalDeEfectivo.RepeatedUnidentifiedDepositor, null, consignante.Trim(), partidasAbiertas.Min(x => x.Fecha), hoy, partidasAbiertas.Count,
            partidasAbiertas.Sum(x => x.Valor));
    }
}
