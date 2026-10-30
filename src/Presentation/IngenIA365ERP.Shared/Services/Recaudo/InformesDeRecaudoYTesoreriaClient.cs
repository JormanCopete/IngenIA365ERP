using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Reportes;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Recaudo;

/// <summary>
/// Cliente tipado de los centros de informes de Recaudo (<c>/api/reports/receipts</c>) y de Tesorería (<c>/api/reports/treasury</c>) (feature 015, US21, T0316;
/// api.md §17): el registro de vistas y cada vista, en JSON para <c>TablaDeReporte</c> o como archivo (<c>xlsx</c>, <c>pdf</c>, <c>docx</c>). Todo es lectura: sin
/// clave; la cabecera la pone el handler. Molde: <c>InformesDeCarteraClient</c>. (nuevo)
/// </summary>
/// <summary>Feature 015, R1 (T0401): una cifra del tablero de caja. (nuevo)</summary>
public sealed record CifraDelTableroDto(string Key, string Label, decimal Amount, int Count);

public sealed record TableroDeCajaDto(DateOnly Date, IReadOnlyList<CifraDelTableroDto> Figures, IReadOnlyList<string> Notes);

public sealed class InformesDeRecaudoYTesoreriaClient(HttpClient http, CentralAuthClient auth)
{
    public const string Recaudo = "receipts";
    public const string Tesoreria = "treasury";

    public static string Ruta(string modulo) => $"/api/reports/{modulo}";

    public Task<ResultadoDeInventario<IReadOnlyList<VistaDeInformeDeModuloDto>>> VistasAsync(string modulo, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<IReadOnlyList<VistaDeInformeDeModuloDto>>(http, auth, HttpMethod.Get, Ruta(modulo), null, null, ct);

    public Task<ResultadoDeInventario<TablaReporteDto>> InformeAsync(string modulo, string vista, string query, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<TablaReporteDto>(http, auth, HttpMethod.Get, RutaDeVista(modulo, vista, query, null), null, null, ct);

    public Task<InvitationApiResult<ArchivoDescargado>> DescargarAsync(string modulo, string vista, string query, string formato, CancellationToken ct = default) =>
        EnvioDeVentas.DescargarAsync(http, auth, RutaDeVista(modulo, vista, query, formato), $"{vista}.{formato}", ct);

    /// <summary>Feature 015, R1 (T0401): el tablero de caja de Recaudo, sin datos personales.</summary>
    public Task<ResultadoDeInventario<TableroDeCajaDto>> TableroDeCajaAsync(DateOnly? dia = null, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<TableroDeCajaDto>(http, auth, HttpMethod.Get,
            EnvioDeVentas.ConQuery("/api/receipts/dashboard", EnvioDeVentas.Query(("date", EnvioDeVentas.Texto(dia)))), null, null, ct);

    private static string RutaDeVista(string modulo, string vista, string query, string? formato)
    {
        var q = query.TrimStart('?');
        if (!string.IsNullOrEmpty(formato))
            q = string.IsNullOrEmpty(q) ? $"format={Uri.EscapeDataString(formato)}" : $"{q}&format={Uri.EscapeDataString(formato)}";
        return EnvioDeVentas.ConQuery($"{Ruta(modulo)}/{Uri.EscapeDataString(vista)}", q);
    }
}

/// <summary>Una vista de Recaudo o de Tesorería como la publica su registro (api.md §17). (nuevo)</summary>
public sealed record VistaDeInformeDeModuloDto
{
    public string Key { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string FileName { get; init; } = "";
    public IReadOnlyList<string> Filters { get; init; } = [];
    public IReadOnlyList<string>? OwnFilters { get; init; }
}
