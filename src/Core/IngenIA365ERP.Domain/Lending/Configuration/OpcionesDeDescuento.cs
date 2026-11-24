using System.Text.Json;

namespace IngenIA365ERP.Domain.Lending.Configuration;

/// <summary>
/// Una opción de descuento condicionado de una línea (pedido del dueño del 2026-11-23, el caso del SOAT): el asociado compra un bien a un
/// proveedor; la cooperativa le presta el valor del bien menos la <see cref="Fraction"/> y esa parte la paga el fondo <see cref="FundCode"/>.
/// Con un bien de 500.000 y la opción del 50 %, el crédito es de 250.000, el fondo pone 250.000 y al proveedor se le giran 500.000. Una
/// opción sin descuento lleva fracción cero y no nombra fondo. El fondo es un código: la cuenta la pone Contabilidad en su matriz. (nuevo)
/// </summary>
public sealed record OpcionDeDescuento(string Code, string Name, decimal Fraction, string? FundCode);

/// <summary>Las opciones de descuento de una versión de línea, en JSON canónico (como las formas de pago). (nuevo)</summary>
public static class OpcionesDeDescuento
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<OpcionDeDescuento> Leer(string? json) =>
        string.IsNullOrWhiteSpace(json) ? [] : JsonSerializer.Deserialize<List<OpcionDeDescuento>>(json, Json) ?? [];

    /// <summary>Normalizadas (código en mayúsculas, fondo en mayúsculas o nulo) y por código; vacía, nulo.</summary>
    public static string? Escribir(IReadOnlyList<OpcionDeDescuento> opciones) => opciones.Count == 0
        ? null
        : JsonSerializer.Serialize(Normalizar(opciones), Json);

    public static IReadOnlyList<OpcionDeDescuento> Normalizar(IEnumerable<OpcionDeDescuento> opciones) =>
        [.. opciones.Select(o => new OpcionDeDescuento(o.Code.Trim().ToUpperInvariant(), o.Name.Trim(), o.Fraction,
                string.IsNullOrWhiteSpace(o.FundCode) || o.Fraction == 0m ? null : o.FundCode.Trim().ToUpperInvariant()))
            .OrderBy(o => o.Code, StringComparer.Ordinal)];

    /// <summary>Lo que paga el fondo de un bien: el valor por la fracción, al peso.</summary>
    public static decimal Subsidio(decimal valorDelBien, decimal fraccion) => Math.Round(valorDelBien * fraccion, 0, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Por qué las opciones no sirven, o nulo: código y nombre, código que no se repite, fracción en [0, 1) y fondo cuando hay descuento.
    /// </summary>
    public static string? Problema(IReadOnlyList<OpcionDeDescuento> opciones)
    {
        if (opciones.Any(o => string.IsNullOrWhiteSpace(o.Code) || string.IsNullOrWhiteSpace(o.Name) || o.Code.Trim().Length > 10)
            || opciones.Select(o => o.Code.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != opciones.Count)
            return "Cada opción de descuento lleva código (hasta 10 caracteres) y nombre, y el código no se repite.";
        if (opciones.Any(o => o.Fraction is < 0m or >= 1m))
            return "El descuento de cada opción es una fracción entre 0 y menos de 1 (el 50 % es 0,5).";
        if (opciones.Any(o => o.Fraction > 0m && (string.IsNullOrWhiteSpace(o.FundCode) || o.FundCode.Trim().Length > 20)))
            return "Una opción con descuento nombra el fondo que lo paga (código de hasta 20 caracteres).";
        return null;
    }
}
