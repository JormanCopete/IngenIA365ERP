using System.Text.Json;
using System.Text.Json.Serialization;

namespace IngenIA365ERP.Application.Tablero;

/// <summary>Una tarjeta del tablero: qué indicador y si muestra sólo lo del usuario o todo lo que él puede ver.</summary>
public sealed record TarjetaDelTablero(
    [property: JsonPropertyName("codigo")] string Codigo,
    [property: JsonPropertyName("soloLoMio")] bool SoloLoMio = false);

/// <summary>
/// Cómo se guarda un tablero (el del usuario en <c>ui.tablero</c>, el de un rol en <c>COR_SystemSettings</c>): JSON de
/// <see cref="TarjetaDelTablero"/> en orden, hasta <see cref="CodigosDelTablero.MaximoDeTarjetas"/>, sin repetir.
/// </summary>
public static class TarjetasDelTablero
{
    private static readonly JsonSerializerOptions Opciones = new() { PropertyNameCaseInsensitive = true };

    public static string Serializar(IReadOnlyList<TarjetaDelTablero> tarjetas) => JsonSerializer.Serialize(tarjetas, Opciones);

    /// <summary>Lee un tablero guardado; nulo si no hay o no se entiende (se usa entonces el siguiente defecto).</summary>
    public static IReadOnlyList<TarjetaDelTablero>? Leer(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var tarjetas = JsonSerializer.Deserialize<List<TarjetaDelTablero>>(json, Opciones);
            return tarjetas is null ? null : Normalizar(tarjetas);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>¿Es un tablero que se puede guardar? (lo usa el validador de preferencias: lista blanca de forma, no de contenido).</summary>
    public static bool EsJsonValido(string json)
    {
        if (json.Length > 4000) return false;
        try
        {
            var tarjetas = JsonSerializer.Deserialize<List<TarjetaDelTablero>>(json, Opciones);
            return tarjetas is not null && EsValido(tarjetas);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool EsValido(IReadOnlyList<TarjetaDelTablero> tarjetas) =>
        tarjetas.Count <= CodigosDelTablero.MaximoDeTarjetas
        && tarjetas.All(t => CodigosDelTablero.EsValido(t.Codigo))
        && tarjetas.Select(t => t.Codigo).Distinct(StringComparer.Ordinal).Count() == tarjetas.Count;

    /// <summary>Sin repetidos ni códigos mal formados, hasta el tope: lo guardado por versiones anteriores nunca rompe el tablero.</summary>
    public static IReadOnlyList<TarjetaDelTablero> Normalizar(IEnumerable<TarjetaDelTablero> tarjetas) =>
        tarjetas.Where(t => CodigosDelTablero.EsValido(t.Codigo))
            .DistinctBy(t => t.Codigo, StringComparer.Ordinal)
            .Take(CodigosDelTablero.MaximoDeTarjetas)
            .ToList();
}
