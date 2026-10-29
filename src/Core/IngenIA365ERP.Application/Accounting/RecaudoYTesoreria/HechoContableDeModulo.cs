using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration;

namespace IngenIA365ERP.Application.Accounting.RecaudoYTesoreria;

/// <summary>
/// Una línea de un hecho de Recaudo o de Tesorería tal como viene en su contenido (<c>FactLineV1</c> o <c>TreasuryFactLineV1</c>): rol, lado, valor, tercero y cruce,
/// y sus dimensiones por código con el nombre de la regla (<c>BranchCode</c>, <c>MeansCode</c>…). La cuenta bancaria propia viaja por su <c>PublicId</c>. (nuevo)
/// </summary>
public sealed record LineaDeHecho(
    string Rol,
    bool Debito,
    decimal Valor,
    Guid? Tercero,
    string? Cruce,
    IReadOnlyDictionary<string, string> Dimensiones,
    Guid? CuentaPropia)
{
    /// <summary>La combinación por código, para los errores y la plantilla: <c>MeansCode=EF · CashBoxCode=C1</c>.</summary>
    public string Combinacion => string.Join(" · ", Dimensiones.OrderBy(d => d.Key, StringComparer.Ordinal).Select(d => $"{d.Key}={d.Value}"));
}

/// <summary>
/// Un hecho de Recaudo o de Tesorería leído para contabilizarlo (feature 015, T0257): el mensaje y sus líneas. Las líneas se leen del <c>PayloadJson</c> —todas las
/// formas de hecho de los dos módulos llevan <c>lines</c> con el mismo cuerpo, contabilidad.md §2—, así el consumidor no conoce cada tipo. (nuevo)
/// </summary>
public sealed record HechoContableDeModulo(MensajeEntrante Mensaje, IReadOnlyList<LineaDeHecho> Lineas)
{
    public string Tipo => Mensaje.Type;

    public DateOnly Fecha => Mensaje.Envelope.Origin.OperationDate;

    public static HechoContableDeModulo De(MensajeEntrante mensaje) => new(mensaje, Leer(mensaje.PayloadJson));

    /// <summary>Las líneas del contenido; vacías si no trae <c>lines</c>.</summary>
    public static IReadOnlyList<LineaDeHecho> Leer(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson)) return [];
        using var documento = JsonDocument.Parse(payloadJson);
        if (!Propiedad(documento.RootElement, "lines", out var lineas) || lineas.ValueKind != JsonValueKind.Array) return [];
        var resultado = new List<LineaDeHecho>();
        foreach (var l in lineas.EnumerateArray())
        {
            var rol = Texto(l, "role") ?? string.Empty;
            var lado = Texto(l, "side") ?? string.Empty;
            var valor = Propiedad(l, "amount", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : 0m;
            var tercero = Guid.TryParse(Texto(l, "thirdPartyPersonPublicId"), out var t) ? t : (Guid?)null;
            var dimensiones = new Dictionary<string, string>(StringComparer.Ordinal);
            Guid? propia = null;
            if (Propiedad(l, "dimensions", out var d) && d.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in d.EnumerateObject())
                {
                    var nombre = char.ToUpperInvariant(p.Name[0]) + p.Name[1..];
                    if (nombre == "OwnBankAccountPublicId")
                    {
                        if (p.Value.ValueKind == JsonValueKind.String && Guid.TryParse(p.Value.GetString(), out var g)) propia = g;
                        continue;
                    }
                    var texto = p.Value.ValueKind switch
                    {
                        JsonValueKind.String => p.Value.GetString(),
                        JsonValueKind.Number => p.Value.GetRawText(),
                        _ => null,
                    };
                    if (!string.IsNullOrWhiteSpace(texto)) dimensiones[nombre] = texto.Trim();
                }
            }
            resultado.Add(new LineaDeHecho(rol, string.Equals(lado, "Debit", StringComparison.OrdinalIgnoreCase), valor, tercero, Texto(l, "crossDocument"), dimensiones,
                propia));
        }
        return resultado;
    }

    private static bool Propiedad(JsonElement e, string nombre, out JsonElement valor)
    {
        foreach (var p in e.EnumerateObject())
        {
            if (string.Equals(p.Name, nombre, StringComparison.OrdinalIgnoreCase))
            {
                valor = p.Value;
                return true;
            }
        }
        valor = default;
        return false;
    }

    private static string? Texto(JsonElement e, string nombre) =>
        Propiedad(e, nombre, out var v) ? v.ValueKind switch
        {
            JsonValueKind.String => v.GetString(),
            JsonValueKind.Number => v.GetRawText(),
            _ => null,
        } : null;

    /// <summary>El número de un valor de una línea para mostrar.</summary>
    public static string Pesos(decimal valor) => valor.ToString("#,##0.##", CultureInfo.InvariantCulture);
}
