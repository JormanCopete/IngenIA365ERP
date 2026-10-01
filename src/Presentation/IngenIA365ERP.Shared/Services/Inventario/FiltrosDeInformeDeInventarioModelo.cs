using System.Globalization;

namespace IngenIA365ERP.Shared.Services.Inventario;

/// <summary>
/// Los filtros de la pantalla <c>/inventario/informes</c> (feature 012, T182; contracts/api.md §27). Las fechas
/// (<c>from</c>, <c>to</c>, <c>asOf</c>) se editan en pantalla; todo lo demás —bodega, producto, punto, los filtros
/// propios de la vista (<c>groupBy</c>, <c>days</c>…)— llega por el enlace de quien profundiza y viaja tal cual a la
/// consulta y a la exportación en <see cref="Otros"/>. <c>vista</c> y <c>format</c> no son filtros: los pone la pantalla.
/// Cuando cada historia sume sus selectores (bodega, producto…), escribirán en <see cref="Otros"/> con el mismo nombre
/// que la API. (nuevo)
/// </summary>
public sealed class FiltrosDeInformeDeInventarioModelo
{
    private static readonly HashSet<string> NoSonFiltros = new(StringComparer.OrdinalIgnoreCase) { "vista", "format", "from", "to", "asOf" };

    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public DateOnly? AsOf { get; set; }

    /// <summary>Los demás filtros por su nombre en la API (<c>warehouse</c>, <c>product</c>, <c>groupBy</c>…).</summary>
    public Dictionary<string, string> Otros { get; private set; } = new(StringComparer.Ordinal);

    public static FiltrosDeInformeDeInventarioModelo DesdeQuery(string? query)
    {
        var m = new FiltrosDeInformeDeInventarioModelo();
        foreach (var par in (query ?? string.Empty).TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var partes = par.Split('=', 2);
            var nombre = Uri.UnescapeDataString(partes[0]);
            var valor = partes.Length > 1 ? Uri.UnescapeDataString(partes[1].Replace('+', ' ')) : string.Empty;
            if (string.IsNullOrWhiteSpace(valor)) continue;
            switch (nombre)
            {
                case "from": m.From = Fecha(valor); break;
                case "to": m.To = Fecha(valor); break;
                case "asOf": m.AsOf = Fecha(valor); break;
                default:
                    if (!NoSonFiltros.Contains(nombre)) m.Otros[nombre] = valor;
                    break;
            }
        }
        return m;
    }

    /// <summary>La query sin «?»: las fechas primero y los demás por nombre; lo vacío no viaja.</summary>
    public string ToQuery()
    {
        var pares = new List<string>();
        if (From is { } desde) pares.Add($"from={desde:yyyy-MM-dd}");
        if (To is { } hasta) pares.Add($"to={hasta:yyyy-MM-dd}");
        if (AsOf is { } corte) pares.Add($"asOf={corte:yyyy-MM-dd}");
        foreach (var (nombre, valor) in Otros.OrderBy(o => o.Key, StringComparer.Ordinal))
            if (!string.IsNullOrWhiteSpace(valor) && !NoSonFiltros.Contains(nombre))
                pares.Add($"{Uri.EscapeDataString(nombre)}={Uri.EscapeDataString(valor.Trim())}");
        return string.Join('&', pares);
    }

    public FiltrosDeInformeDeInventarioModelo Copia() => new()
    {
        From = From,
        To = To,
        AsOf = AsOf,
        Otros = new Dictionary<string, string>(Otros, StringComparer.Ordinal),
    };

    private static DateOnly? Fecha(string valor) =>
        DateOnly.TryParseExact(valor, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) ? f : null;
}
