namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// 2026-11-17 (pedido del dueño): una cuota del estado de cuenta por rubro, con el interés corriente y el de mora aparte (antes salían juntos en
/// «Interés» y la mora parecía interés del plan). En una cuota pendiente son lo que debe hoy de cada rubro (<see cref="CuotaDelEstadoDto.Owed"/>, por
/// el nombre del concepto de Cartera); en una ya cubierta, los valores del plan.
/// </summary>
public sealed record RubrosDeCuota(decimal Capital, decimal Corriente, decimal Mora, decimal Seguro, decimal Administracion, decimal Otros)
{
    private static readonly string[] DeCorriente = ["CurrentInterest", "CurrentInterestInSuspense"];
    private static readonly string[] DeMora = ["DefaultInterest", "DefaultInterestInSuspense"];
    private static readonly string[] AFavor = ["Advance", "CreditBalance"];

    public decimal Total => Capital + Corriente + Mora + Seguro + Administracion + Otros;

    /// <summary>Lo que debe hoy de la cuota, sin el anticipo ni el saldo a favor (que no se cobran en ella).</summary>
    public static decimal Debe(CuotaDelEstadoDto q) => De(q, loQueDebe: true).Total;

    /// <summary>Los rubros de la cuota: lo que debe (<paramref name="loQueDebe"/>) o los del plan.</summary>
    public static RubrosDeCuota De(CuotaDelEstadoDto q, bool loQueDebe)
    {
        ArgumentNullException.ThrowIfNull(q);
        if (!loQueDebe) return new RubrosDeCuota(q.Principal, q.Interest, 0m, q.Insurance, q.Administration, 0m);
        var o = q.Owed;
        decimal Suma(IEnumerable<string> claves) => claves.Sum(k => o.TryGetValue(k, out var v) ? v : 0m);
        var capital = Suma(["Principal"]);
        var corriente = Suma(DeCorriente);
        var mora = Suma(DeMora);
        var seguro = Suma(["Insurance"]);
        var administracion = Suma(["Administration"]);
        var otros = o.Where(x => x.Key is not ("Principal" or "Insurance" or "Administration") && !DeCorriente.Contains(x.Key) && !DeMora.Contains(x.Key)
                                 && !AFavor.Contains(x.Key)).Sum(x => x.Value);
        return new RubrosDeCuota(capital, corriente, mora, seguro, administracion, otros);
    }
}
