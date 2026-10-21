using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Deposits;

/// <summary>
/// De qué clases de aporte sale un débito de aportes (feature 014, US3, T086; FR-042): una devolución sin retiro o una cesión toman primero los
/// extraordinarios, luego la revalorización, luego los ordinarios y por último el mínimo, y <b>nunca</b> dejan el total por debajo del mínimo no
/// reducible. Puro. (nuevo)
/// </summary>
public static class DebitoDeAportes
{
    /// <summary>El orden en que se toman las clases.</summary>
    public static IReadOnlyList<MemberAccountConcept> Orden { get; } =
    [
        MemberAccountConcept.ContributionExtraordinary, MemberAccountConcept.ContributionRevaluation, MemberAccountConcept.ContributionOrdinary,
        MemberAccountConcept.ContributionMinimum,
    ];

    /// <summary>
    /// Las porciones (en positivo) que salen de cada clase para debitar <paramref name="monto"/>; nulo si el débito dejaría el total por debajo de
    /// <paramref name="minimo"/> o no alcanza.
    /// </summary>
    public static IReadOnlyList<(MemberAccountConcept Concepto, decimal Monto)>? Repartir(IReadOnlyDictionary<MemberAccountConcept, decimal> saldos, decimal monto,
        decimal minimo)
    {
        ArgumentNullException.ThrowIfNull(saldos);
        if (monto <= 0m) throw new ArgumentOutOfRangeException(nameof(monto), monto, "El débito es mayor que cero.");
        var total = Orden.Sum(c => Math.Max(0m, saldos.GetValueOrDefault(c)));
        if (total - monto < minimo || total < monto) return null;
        var resto = monto;
        var porciones = new List<(MemberAccountConcept, decimal)>();
        foreach (var concepto in Orden)
        {
            if (resto <= 0m) break;
            var toma = Math.Min(resto, Math.Max(0m, saldos.GetValueOrDefault(concepto)));
            if (toma <= 0m) continue;
            porciones.Add((concepto, toma));
            resto -= toma;
        }
        return porciones;
    }

    /// <summary>Lo que se puede devolver sin perforar el mínimo.</summary>
    public static decimal Devolvible(IReadOnlyDictionary<MemberAccountConcept, decimal> saldos, decimal minimo) =>
        Math.Max(0m, Orden.Sum(c => Math.Max(0m, saldos.GetValueOrDefault(c))) - minimo);
}
