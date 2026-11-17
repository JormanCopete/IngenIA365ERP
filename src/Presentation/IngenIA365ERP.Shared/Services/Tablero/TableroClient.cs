using System.Text.Json.Serialization;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Tablero;

/// <summary>
/// 2026-11-17 (pedido del dueño): el tablero de inicio. Lee el tablero de quien entra, cada indicador por separado (así una cifra
/// lenta no detiene las demás) y los pendientes; el administrador lee y fija el tablero de cada rol. El tablero propio se guarda en
/// las preferencias (<see cref="ClaveDeTablero"/>) con <see cref="PreferenciasClient"/>.
/// </summary>
public sealed class TableroClient(HttpClient http, CentralAuthClient auth)
{
    public const string Base = "/api/dashboard";

    /// <summary>La preferencia del usuario con sus tarjetas (<c>UserSettingKeys.Tablero</c>).</summary>
    public const string ClaveDeTablero = "ui.tablero";

    public const int MaximoDeTarjetas = 24;

    public Task<ResultadoDeInventario<MiTableroDto>> MiTableroAsync(CancellationToken ct = default) =>
        Enviar<MiTableroDto>(HttpMethod.Get, $"{Base}/mine", null, ct);

    public Task<ResultadoDeInventario<ValorDeIndicadorDto>> IndicadorAsync(string codigo, bool soloLoMio, CancellationToken ct = default) =>
        Enviar<ValorDeIndicadorDto>(HttpMethod.Get, $"{Base}/indicators/{Uri.EscapeDataString(codigo)}?soloLoMio={(soloLoMio ? "true" : "false")}", null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<PendienteDto>>> PendientesAsync(CancellationToken ct = default) =>
        Enviar<IReadOnlyList<PendienteDto>>(HttpMethod.Get, $"{Base}/pending", null, ct);

    public Task<ResultadoDeInventario<TablerosPorRolDto>> RolesAsync(CancellationToken ct = default) =>
        Enviar<TablerosPorRolDto>(HttpMethod.Get, $"{Base}/roles", null, ct);

    public Task<ResultadoDeInventario<TableroDeRolDto>> FijarDelRolAsync(string rol, IReadOnlyList<TarjetaDelTableroDto> tarjetas, CancellationToken ct = default) =>
        Enviar<TableroDeRolDto>(HttpMethod.Put, $"{Base}/roles/{Uri.EscapeDataString(rol)}", new { tarjetas }, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, CancellationToken ct) =>
        EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, null, ct);
}

/// <summary>Origen del tablero: 1 el del usuario, 2 el de su rol, 3 el sugerido.</summary>
public static class OrigenDelTablero
{
    public const int Usuario = 1;
    public const int Rol = 2;
    public const int Sugerido = 3;
}

/// <summary>Formato: 1 número, 2 moneda, 3 porcentaje. Tipo: 1 cifra, 2 serie.</summary>
public static class FormatoDeIndicador
{
    public const int Numero = 1;
    public const int Moneda = 2;
    public const int Porcentaje = 3;
}

public sealed record TarjetaDelTableroDto(
    [property: JsonPropertyName("codigo")] string Codigo,
    [property: JsonPropertyName("soloLoMio")] bool SoloLoMio = false);

public sealed record IndicadorDisponibleDto(
    string Codigo, string Titulo, string Grupo, string Descripcion, bool AdmiteSoloLoMio, int Tipo, int Formato, string? Ruta, string Permiso);

public sealed record MiTableroDto(int Origen, string? Rol, IReadOnlyList<TarjetaDelTableroDto> Tarjetas, IReadOnlyList<IndicadorDisponibleDto> Disponibles);

public sealed record PuntoDeSerieDto(string Etiqueta, decimal Valor);

public sealed record ValorDeIndicadorDto(
    string Codigo, string Titulo, int Tipo, int Formato, bool SoloLoMio, decimal? Valor, decimal? Anterior, string? EtiquetaDelAnterior,
    IReadOnlyList<PuntoDeSerieDto> Serie, string? Nota, string? Ruta);

public sealed record PendienteDto(string Codigo, string Titulo, string Grupo, int Cantidad, string Ruta);

public sealed record TableroDeRolDto(string Codigo, string Nombre, IReadOnlyList<TarjetaDelTableroDto> Tarjetas);

public sealed record TablerosPorRolDto(IReadOnlyList<TableroDeRolDto> Roles, IReadOnlyList<IndicadorDisponibleDto> Indicadores);

/// <summary>Cómo se escribe una cifra del tablero (pesos sin centavos, porcentaje con uno).</summary>
public static class TextoDeIndicador
{
    private static readonly System.Globalization.CultureInfo Co = new("es-CO");

    public static string Valor(decimal? valor, int formato) => valor is not { } v
        ? "—"
        : formato switch
        {
            FormatoDeIndicador.Moneda => "$ " + decimal.Round(v, 0).ToString("N0", Co),
            FormatoDeIndicador.Porcentaje => v.ToString("N1", Co) + " %",
            _ => v.ToString(v == decimal.Truncate(v) ? "N0" : "N2", Co),
        };

    /// <summary>La variación contra el anterior: «▲ 12,5 % vs. ayer»; nulo si no hay con qué comparar.</summary>
    public static string? Variacion(decimal? valor, decimal? anterior, string? etiqueta)
    {
        if (valor is not { } v || anterior is not { } a || etiqueta is null) return null;
        if (a == 0m) return v == 0m ? $"= {etiqueta}" : $"▲ vs. {etiqueta} (0)";
        var cambio = (v - a) / Math.Abs(a) * 100m;
        var flecha = cambio > 0 ? "▲" : cambio < 0 ? "▼" : "=";
        return $"{flecha} {Math.Abs(cambio).ToString("N1", Co)} % vs. {etiqueta}";
    }
}
