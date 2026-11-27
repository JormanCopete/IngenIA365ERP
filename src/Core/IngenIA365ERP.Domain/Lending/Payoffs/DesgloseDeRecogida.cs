using System.Text.Json;
using System.Text.Json.Serialization;

namespace IngenIA365ERP.Domain.Lending.Payoffs;

/// <summary>
/// Lo que cuesta recoger un producto, por concepto, en pesos (feature 028, FR-003): el mismo desglose para lo proyectado en la solicitud
/// (<c>LND_CreditApplicationPayoffs.ProjectedBreakdown</c>), lo de ahora en el desembolso y la diferencia entre los dos
/// (<c>LND_DisbursementPayoffs.DifferenceDetail</c>). Cada concepto va en positivo; los que se descuentan —el interés devuelto por proporcionales, el
/// anticipo y el saldo a favor— restan en <see cref="Total"/>. En una diferencia (<see cref="DiferenciaDeRecogida"/>) cada concepto es «ahora −
/// proyectado» y puede ser negativo. (nuevo)
/// </summary>
/// <param name="Capital">El capital que se cancela o abona.</param>
/// <param name="InteresCorriente">El interés corriente causado que se cobra.</param>
/// <param name="InteresDevuelto">El interés ya causado de los días que faltan, que se devuelve por proporcionales (resta).</param>
/// <param name="InteresCobradoPorProporcionales">El interés de los días aún no causados, o el de la cuota completa, que se cobra por la regla de
/// proporcionales.</param>
/// <param name="Mora">El interés de mora.</param>
/// <param name="Seguro">El seguro causado.</param>
/// <param name="Administracion">La administración causada.</param>
/// <param name="OtrosCargos">Gastos de cobranza y otros cargos.</param>
/// <param name="Anticipo">El anticipo a favor del crédito que se descuenta (resta).</param>
/// <param name="SaldoAFavor">El saldo a favor que se descuenta (resta).</param>
public sealed record DesgloseDeRecogida(
    decimal Capital = 0m,
    decimal InteresCorriente = 0m,
    decimal InteresDevuelto = 0m,
    decimal InteresCobradoPorProporcionales = 0m,
    decimal Mora = 0m,
    decimal Seguro = 0m,
    decimal Administracion = 0m,
    decimal OtrosCargos = 0m,
    decimal Anticipo = 0m,
    decimal SaldoAFavor = 0m)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Sin nada que recoger (un producto ya cancelado, o una fila sin proyección).</summary>
    public static DesgloseDeRecogida Vacio { get; } = new();

    /// <summary>Lo que se recoge: los conceptos que se cobran menos los que se descuentan. No se guarda: sale de los conceptos.</summary>
    [JsonIgnore]
    public decimal Total => Capital + InteresCorriente - InteresDevuelto + InteresCobradoPorProporcionales + Mora + Seguro + Administracion + OtrosCargos
        - Anticipo - SaldoAFavor;

    /// <summary>Si algún concepto es distinto de cero.</summary>
    [JsonIgnore]
    public bool TieneAlgo => this != Vacio;

    /// <summary>Concepto a concepto: este menos el otro.</summary>
    public DesgloseDeRecogida Menos(DesgloseDeRecogida otro) => new(
        Capital - otro.Capital,
        InteresCorriente - otro.InteresCorriente,
        InteresDevuelto - otro.InteresDevuelto,
        InteresCobradoPorProporcionales - otro.InteresCobradoPorProporcionales,
        Mora - otro.Mora,
        Seguro - otro.Seguro,
        Administracion - otro.Administracion,
        OtrosCargos - otro.OtrosCargos,
        Anticipo - otro.Anticipo,
        SaldoAFavor - otro.SaldoAFavor);

    /// <summary>El JSON con que se guarda en las columnas de texto (camelCase).</summary>
    public string Serializar() => JsonSerializer.Serialize(this, Json);

    /// <summary>
    /// Lee lo guardado; vacío o <c>{}</c> (una fila sin proyección, como las que copió la migración de <c>PayoffRequest</c>), <see cref="Vacio"/>. Lo
    /// guardado siempre sale de <see cref="Serializar"/>: un JSON ilegible es un dato dañado y la excepción sale, no se disimula.
    /// </summary>
    public static DesgloseDeRecogida Leer(string? json) =>
        string.IsNullOrWhiteSpace(json) ? Vacio : JsonSerializer.Deserialize<DesgloseDeRecogida>(json, Json) ?? Vacio;
}
