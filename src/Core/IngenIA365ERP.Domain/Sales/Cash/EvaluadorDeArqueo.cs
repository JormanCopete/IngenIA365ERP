using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Sales.Cash;

/// <summary>Lo contado de un medio al cerrar y su motivo. Nulo = no se contó nada (cuenta cero). (nuevo)</summary>
public sealed record ConteoDeMedio(int PaymentMeansId, decimal? Counted, string? Reason);

/// <summary>Una línea del arqueo (<c>INV_CashCountLines</c>): esperado, contado, diferencia, tolerancia copiada y tratamiento. (nuevo)</summary>
public sealed record LineaDeArqueo(
    int PaymentMeansId,
    string Code,
    CashCountMethod CountMethod,
    decimal Expected,
    decimal Counted,
    decimal Difference,
    int PaymentsCount,
    decimal Tolerance,
    bool WithinTolerance,
    CashDifferenceTreatment? Treatment,
    string? Reason);

/// <summary>Una línea del documento de diferencia (<c>INV_CashDocumentLines</c>): +1 sobrante, −1 faltante, valor positivo. (nuevo)</summary>
public sealed record LineaDeDiferencia(
    short LineNumber,
    int PaymentMeansId,
    string Code,
    short Sign,
    decimal Amount,
    CashDifferenceTreatment Treatment,
    bool WithinTolerance,
    string? Reason);

/// <summary>
/// El arqueo evaluado. <see cref="AmountToApprove"/> = Σ |diferencia| de las líneas por encima de su tolerancia: el monto que
/// evalúa la política del tipo de <c>CashCountDifference</c>; cero = se confirma con el cierre, con los motivos. Los medios de
/// <see cref="MissingReasons"/> tienen diferencia y no traen motivo (<c>Inventory.CashSession.ReasonRequired</c>). (nuevo)
/// </summary>
public sealed record ResultadoDeArqueo(
    IReadOnlyList<LineaDeArqueo> Lines,
    decimal TotalExpected,
    decimal TotalCounted,
    decimal TotalDifference,
    IReadOnlyList<LineaDeDiferencia> DifferenceLines,
    decimal AmountToApprove,
    IReadOnlyList<string> MissingReasons)
{
    /// <summary>Alguna línea difiere: el cierre crea el documento <c>CashCountDifference</c>.</summary>
    public bool HasDifference => DifferenceLines.Count > 0;

    /// <summary>Alguna línea supera su tolerancia: el documento pasa por la aprobación (nunca del cajero).</summary>
    public bool RequiresApproval => AmountToApprove > 0m;
}

/// <summary>
/// El motor puro del arqueo por medio de pago (feature 012, I3, T580; FR-099, T50; data-model §15; contracts/api.md §21.2).
/// <list type="bullet">
/// <item>diferencia = contado − esperado; en los medios sin arqueo (<c>CashCountMethod.None</c>, los créditos) contado = esperado;</item>
/// <item>dentro de la tolerancia <b>copiada</b> del medio si |diferencia| ≤ tolerancia;</item>
/// <item>el sobrante es siempre <c>Surplus</c>; el faltante, el tratamiento sellado en la sesión (<c>Caja.TratamientoFaltante</c>);</item>
/// <item>el documento de diferencia lleva una línea por medio que difiere, también las aceptadas dentro de la tolerancia (F3);</item>
/// <item>monto a aprobar = Σ |diferencia| de las líneas sobre la tolerancia.</item>
/// </list>
/// (nuevo)
/// </summary>
public static class EvaluadorDeArqueo
{
    /// <summary>Los valores sellados de <c>Caja.TratamientoFaltante</c> en <c>INV_CashSessions.ShortageTreatment</c>.</summary>
    public const string FaltanteAlGasto = "Gasto";

    /// <inheritdoc cref="FaltanteAlGasto"/>
    public const string FaltanteACargoDelCajero = "CargoAlCajero";

    /// <summary>El tratamiento del faltante que corresponde al valor sellado en la sesión.</summary>
    public static CashDifferenceTreatment TratamientoDelFaltanteDesde(string valorSellado) => valorSellado switch
    {
        FaltanteAlGasto => CashDifferenceTreatment.ShortageToExpense,
        FaltanteACargoDelCajero => CashDifferenceTreatment.ShortageToCashier,
        _ => throw new ArgumentOutOfRangeException(nameof(valorSellado), valorSellado,
            $"Caja.TratamientoFaltante admite «{FaltanteAlGasto}» o «{FaltanteACargoDelCajero}»."),
    };

    public static ResultadoDeArqueo Evaluar(EsperadoDeLaSesion esperado, IReadOnlyList<ConteoDeMedio> conteos, CashDifferenceTreatment tratamientoDelFaltante)
    {
        ArgumentNullException.ThrowIfNull(esperado);
        ArgumentNullException.ThrowIfNull(conteos);
        if (tratamientoDelFaltante is not (CashDifferenceTreatment.ShortageToCashier or CashDifferenceTreatment.ShortageToExpense))
            throw new ArgumentOutOfRangeException(nameof(tratamientoDelFaltante), tratamientoDelFaltante, "El faltante va al cajero o al gasto.");

        var porMedio = conteos.ToDictionary(c => c.PaymentMeansId);
        var ajenos = porMedio.Keys.Except(esperado.Lines.Select(l => l.Medio.PaymentMeansId)).ToList();
        if (ajenos.Count > 0)
            throw new ArgumentException($"Se contaron medios sin línea en el esperado: {string.Join(", ", ajenos)}.", nameof(conteos));

        var lineas = new List<LineaDeArqueo>();
        var diferencias = new List<LineaDeDiferencia>();
        var sinMotivo = new List<string>();
        var aAprobar = 0m;

        foreach (var e in esperado.Lines)
        {
            var conteo = porMedio.GetValueOrDefault(e.Medio.PaymentMeansId);
            var motivo = string.IsNullOrWhiteSpace(conteo?.Reason) ? null : conteo!.Reason!.Trim();
            var contado = e.Medio.CountMethod == CashCountMethod.None ? e.Expected : conteo?.Counted ?? 0m;
            var diferencia = contado - e.Expected;
            var dentro = Math.Abs(diferencia) <= e.Tolerance;
            CashDifferenceTreatment? tratamiento = diferencia switch
            {
                > 0m => CashDifferenceTreatment.Surplus,
                < 0m => tratamientoDelFaltante,
                _ => null,
            };

            lineas.Add(new LineaDeArqueo(e.Medio.PaymentMeansId, e.Medio.Code, e.Medio.CountMethod, e.Expected, contado, diferencia,
                e.PaymentsCount, e.Tolerance, dentro, tratamiento, motivo));

            if (tratamiento is not { } t) continue;
            diferencias.Add(new LineaDeDiferencia((short)(diferencias.Count + 1), e.Medio.PaymentMeansId, e.Medio.Code,
                (short)Math.Sign(diferencia), Math.Abs(diferencia), t, dentro, motivo));
            if (!dentro) aAprobar += Math.Abs(diferencia);
            if (motivo is null) sinMotivo.Add(e.Medio.Code);
        }

        return new ResultadoDeArqueo(lineas, lineas.Sum(l => l.Expected), lineas.Sum(l => l.Counted), lineas.Sum(l => l.Difference),
            diferencias, aAprobar, sinMotivo);
    }
}
