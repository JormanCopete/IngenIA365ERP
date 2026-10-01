using IngenIA365ERP.Domain.Inventory.Costing;

namespace IngenIA365ERP.Domain.Sales.Pricing;

/// <summary>El tope vigente de un rol (<c>INV_DiscountCaps</c>): fracciones por línea y por total, con su vigencia. (nuevo)</summary>
public sealed record TopeDeRol(int RoleId, decimal MaxLineRate, decimal MaxDocumentRate, DateOnly ValidFrom, DateOnly? ValidTo);

/// <summary>
/// El tope efectivo de un usuario a una fecha: el mayor de los topes vigentes de sus roles activos, por línea y por total por
/// separado; sin fila, 0 (T51). <see cref="FromRoles"/> son los roles con tope vigente (<c>GET /discount-caps/mine</c>). (nuevo)
/// </summary>
public sealed record TopeDelUsuario(decimal MaxLineRate, decimal MaxDocumentRate, IReadOnlyList<int> FromRoles)
{
    /// <summary>Sin tope: todo descuento pide aprobación.</summary>
    public static TopeDelUsuario Ninguno { get; } = new(0m, 0m, []);
}

/// <summary>
/// Un descuento de línea evaluado contra el tope (data-model §14 <c>INV_DocumentLineDiscounts</c>): el valor, la fracción
/// efectiva (a seis decimales, <c>Rate</c> 9,6), si nace de cambiar el precio de lista, la copia del tope aplicado y si pide
/// aprobación. Sobre el tope no se rechaza: la línea queda con la aprobación pendiente (contracts/api.md §19.3). (nuevo)
/// </summary>
public sealed record DescuentoDeLinea(decimal Amount, decimal Rate, bool IsPriceOverride, decimal CapRateApplied, bool RequiresApproval);

/// <summary>
/// Un descuento por total prorrateado: una parte por línea (<c>FromDocumentDiscount = 1</c>) cuya suma es exactamente el
/// descuento, la fracción sobre la suma de los netos, la copia del tope por total y si pide aprobación. (nuevo)
/// </summary>
public sealed record DescuentoPorTotal(IReadOnlyList<decimal> Parts, decimal Rate, decimal CapRateApplied, bool RequiresApproval);

/// <summary>
/// Los topes de descuento y la prorrata del descuento por total (feature 012, I3, T578; FR-054, FR-017, T51). Puro: recibe los
/// topes de los roles del usuario, los valores de la línea y el redondeo vigente (<c>Redondeo.Montos</c>, <c>Redondeo.Residuo</c>).
/// Cambiar a mano el precio de lista <b>es</b> un descuento: no hay otra forma de vender por debajo de la lista. La comparación
/// con el tope se hace con la fracción exacta; la fracción guardada va a seis decimales. (nuevo)
/// </summary>
public static class TopeDeDescuento
{
    /// <summary>Decimales de una fracción (tarifa 9,6).</summary>
    public const int DecimalesDeTarifa = 6;

    /// <summary>El mayor de los topes vigentes a <paramref name="fecha"/> de los roles activos del usuario; sin fila, 0.</summary>
    public static TopeDelUsuario Efectivo(IEnumerable<TopeDeRol> topesDeSusRolesActivos, DateOnly fecha)
    {
        ArgumentNullException.ThrowIfNull(topesDeSusRolesActivos);
        var vigentes = topesDeSusRolesActivos
            .Where(t => t.ValidFrom <= fecha && (t.ValidTo is null || t.ValidTo >= fecha))
            .ToList();
        if (vigentes.Count == 0) return TopeDelUsuario.Ninguno;

        return new TopeDelUsuario(
            vigentes.Max(t => t.MaxLineRate),
            vigentes.Max(t => t.MaxDocumentRate),
            vigentes.Select(t => t.RoleId).Distinct().OrderBy(r => r).ToList());
    }

    /// <summary>
    /// El precio digitado bajo el de lista: descuento <c>IsPriceOverride</c> de (lista − precio) × cantidad, medido contra el
    /// bruto a precio de lista. Nulo si el precio no baja (subirlo no es un descuento).
    /// </summary>
    public static DescuentoDeLinea? PorPrecioDigitado(decimal listPrice, decimal precioDigitado, decimal cantidad, TopeDelUsuario tope, RedondeoDeMontos montos)
    {
        ArgumentNullException.ThrowIfNull(tope);
        if (precioDigitado >= listPrice) return null;

        var bruto = Redondeo.Monto(cantidad * listPrice, montos);
        var valor = Redondeo.Monto((listPrice - precioDigitado) * cantidad, montos);
        return Linea(bruto, valor, isPriceOverride: true, tope);
    }

    /// <summary>Un porcentaje (fracción) sobre el bruto de la línea.</summary>
    public static DescuentoDeLinea PorPorcentaje(decimal bruto, decimal tasa, TopeDelUsuario tope, RedondeoDeMontos montos)
    {
        ArgumentNullException.ThrowIfNull(tope);
        return Linea(bruto, Redondeo.Monto(bruto * tasa, montos), isPriceOverride: false, tope);
    }

    /// <summary>Un valor sobre el bruto de la línea.</summary>
    public static DescuentoDeLinea PorValor(decimal bruto, decimal valor, TopeDelUsuario tope)
    {
        ArgumentNullException.ThrowIfNull(tope);
        return Linea(bruto, valor, isPriceOverride: false, tope);
    }

    /// <summary>¿La suma de los descuentos de una línea supera el tope por línea? (varios descuentos se miden juntos)</summary>
    public static bool SuperaTopeDeLinea(decimal bruto, IEnumerable<decimal> descuentosDeLaLinea, TopeDelUsuario tope)
    {
        ArgumentNullException.ThrowIfNull(descuentosDeLaLinea);
        ArgumentNullException.ThrowIfNull(tope);
        return bruto > 0m && descuentosDeLaLinea.Sum() / bruto > tope.MaxLineRate;
    }

    /// <summary>
    /// Prorratea un descuento por total a las líneas en proporción a su neto, con el residuo según <paramref name="residuo"/>
    /// (suma exacta, FR-017), y lo compara con el tope por total.
    /// </summary>
    public static DescuentoPorTotal PorTotal(decimal descuento, IReadOnlyList<decimal> netosDeLinea, TopeDelUsuario tope, RedondeoDeMontos montos, ResiduoDeRedondeo residuo)
    {
        ArgumentNullException.ThrowIfNull(netosDeLinea);
        ArgumentNullException.ThrowIfNull(tope);

        var total = Redondeo.Monto(descuento, montos);
        var suma = netosDeLinea.Sum();
        if (suma <= 0m) return new DescuentoPorTotal(netosDeLinea.Select(_ => 0m).ToList(), 0m, tope.MaxDocumentRate, total > 0m);

        var partes = Redondeo.Repartir(total, netosDeLinea.Select(n => total * n / suma).ToList(), montos, residuo);
        var fraccion = total / suma;
        return new DescuentoPorTotal(partes, Fraccion(fraccion), tope.MaxDocumentRate, fraccion > tope.MaxDocumentRate);
    }

    private static DescuentoDeLinea Linea(decimal bruto, decimal valor, bool isPriceOverride, TopeDelUsuario tope)
    {
        var fraccion = bruto > 0m ? valor / bruto : 0m;
        return new DescuentoDeLinea(valor, Fraccion(fraccion), isPriceOverride, tope.MaxLineRate, fraccion > tope.MaxLineRate);
    }

    private static decimal Fraccion(decimal valor) => Math.Round(valor, DecimalesDeTarifa, MidpointRounding.AwayFromZero);
}
