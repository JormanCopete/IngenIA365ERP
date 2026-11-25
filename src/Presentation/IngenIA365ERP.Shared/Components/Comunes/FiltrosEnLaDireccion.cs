using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace IngenIA365ERP.Shared.Components.Comunes;

/// <summary>
/// 2026-11-24 (pedido del dueño, filtros de Ventas): los filtros de una lista viajan en la dirección de la página, así al volver de un
/// documento (o con «atrás» del navegador) la lista sale como estaba, y un enlace copiado abre la misma búsqueda. La pantalla lee la
/// dirección al iniciar (<see cref="FiltrosEnLaDireccion(string)"/>) y la reemplaza en cada consulta (<see cref="Guardar"/>), sin sumar
/// entradas al historial. Lo vacío no viaja.
/// </summary>
public sealed class FiltrosEnLaDireccion
{
    private readonly Dictionary<string, string> _valores = new(StringComparer.Ordinal);

    public FiltrosEnLaDireccion(string uri)
    {
        var inicio = uri.IndexOf('?');
        if (inicio < 0) return;
        var fin = uri.IndexOf('#', inicio);
        var query = fin < 0 ? uri[(inicio + 1)..] : uri[(inicio + 1)..fin];
        foreach (var par in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var partes = par.Split('=', 2);
            var valor = partes.Length > 1 ? Uri.UnescapeDataString(partes[1].Replace('+', ' ')) : string.Empty;
            if (!string.IsNullOrWhiteSpace(valor)) _valores[Uri.UnescapeDataString(partes[0])] = valor.Trim();
        }
    }

    public string? Texto(string clave) => _valores.TryGetValue(clave, out var v) ? v : null;

    public int? Entero(string clave) => int.TryParse(Texto(clave), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    public long? Largo(string clave) => long.TryParse(Texto(clave), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;

    public decimal? Decimal(string clave) => decimal.TryParse(Texto(clave), NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;

    public DateOnly? Fecha(string clave) =>
        DateOnly.TryParseExact(Texto(clave), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) ? f : null;

    public Guid? Id(string clave) => Guid.TryParse(Texto(clave), out var id) ? id : null;

    public bool Marca(string clave) => Texto(clave) is "1" or "true";

    // --------------------------------------------------------------------------------------- de vuelta a texto --

    public static string? Valor(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    public static string? Valor(int? numero) => numero?.ToString(CultureInfo.InvariantCulture);

    public static string? Valor(long? numero) => numero?.ToString(CultureInfo.InvariantCulture);

    public static string? Valor(decimal? numero) => numero?.ToString(CultureInfo.InvariantCulture);

    public static string? Valor(DateOnly? fecha) => fecha?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string? Valor(Guid? id) => id?.ToString();

    public static string? Valor(bool marca) => marca ? "1" : null;

    /// <summary>Reemplaza la dirección con estos filtros (los nulos se quitan) y devuelve la ruta relativa, para el enlace de vuelta.</summary>
    public static string Guardar(NavigationManager nav, IReadOnlyDictionary<string, string?> filtros)
    {
        var uri = nav.GetUriWithQueryParameters(filtros.ToDictionary(p => p.Key, p => (object?)Valor(p.Value), StringComparer.Ordinal));
        if (!string.Equals(uri, nav.Uri, StringComparison.Ordinal)) nav.NavigateTo(uri, replace: true);
        return "/" + nav.ToBaseRelativePath(uri);
    }
}
