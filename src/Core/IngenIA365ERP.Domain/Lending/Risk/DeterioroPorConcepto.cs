using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Risk;

/// <summary>
/// El deterioro individual de una línea del cierre repartido por concepto (feature 013, US9, T0705; FR-103; contracts/contabilidad.md §11.2,
/// §11.3): el del capital es del capital; el de los intereses (corriente y de mora causados en balance de un crédito suspendido, al 100 %) se
/// reparte en el orden corriente → mora hasta su saldo, y el de los otros conceptos en seguro → gasto de cobranza → otros cargos; lo que sobre por
/// el redondeo queda en el último. Determinista desde la línea: así el cierre siguiente sabe cuánto había por concepto sin otra columna. Puro.
/// (nuevo)
/// </summary>
public static class DeterioroPorConcepto
{
    private static readonly LoanConcept[] DeIntereses = [LoanConcept.CurrentInterest, LoanConcept.DefaultInterest];

    private static readonly LoanConcept[] DeOtros = [LoanConcept.Insurance, LoanConcept.CollectionFee, LoanConcept.OtherCharge];

    /// <summary>El deterioro por concepto (sólo los distintos de cero), en el orden del capital, los intereses y los otros.</summary>
    public static IReadOnlyList<(LoanConcept Concepto, decimal Importe)> Repartir(decimal capital, decimal intereses, decimal otros,
        IReadOnlyDictionary<LoanConcept, decimal> saldos)
    {
        ArgumentNullException.ThrowIfNull(saldos);
        var resultado = new List<(LoanConcept, decimal)>();
        if (capital != 0m) resultado.Add((LoanConcept.Principal, capital));
        resultado.AddRange(Partir(intereses, DeIntereses, saldos));
        resultado.AddRange(Partir(otros, DeOtros, saldos));
        return resultado;
    }

    private static IEnumerable<(LoanConcept, decimal)> Partir(decimal total, LoanConcept[] conceptos, IReadOnlyDictionary<LoanConcept, decimal> saldos)
    {
        if (total == 0m) yield break;
        var restante = total;
        for (var i = 0; i < conceptos.Length; i++)
        {
            var ultimo = i == conceptos.Length - 1;
            var parte = ultimo ? restante : Math.Min(restante, Math.Max(0m, saldos.GetValueOrDefault(conceptos[i])));
            if (parte != 0m) yield return (conceptos[i], parte);
            restante -= parte;
            if (restante == 0m) yield break;
        }
    }
}
