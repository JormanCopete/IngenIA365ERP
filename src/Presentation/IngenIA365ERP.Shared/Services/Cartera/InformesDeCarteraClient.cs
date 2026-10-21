using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Reportes;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Cliente tipado del centro de informes de Cartera (feature 013, US15, T0799; contracts/api.md §17): el registro de vistas publicadas
/// (<c>GET /api/reports/lending</c>, sin las que exigen un permiso que la persona no tiene) y cada vista en <c>/api/reports/lending/{vista}</c>,
/// en JSON para la pantalla (<c>TablaDeReporte</c>) o como archivo (<c>xlsx</c>, <c>pdf</c>, <c>docx</c>). Todo es lectura: sin clave. Sin
/// <c>Authorization</c> a mano (lo pone el handler). Molde: <c>InventarioClient.Informes</c>. (nuevo)
/// </summary>
public sealed class InformesDeCarteraClient(HttpClient http, CentralAuthClient auth)
{
    public const string RutaDeInformes = "/api/reports/lending";

    /// <summary>Las vistas publicadas que la persona puede ver.</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<VistaDeInformeDeCarteraDto>>> VistasAsync(CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<IReadOnlyList<VistaDeInformeDeCarteraDto>>(http, auth, HttpMethod.Get, RutaDeInformes, null, null, ct);

    /// <summary>La tabla de una vista con los filtros dados (<paramref name="query"/> sin «?»; puede venir vacía).</summary>
    public Task<ResultadoDeInventario<TablaReporteDto>> InformeAsync(string vista, string query, CancellationToken ct = default) =>
        EnvioDeVentas.EnviarAsync<TablaReporteDto>(http, auth, HttpMethod.Get, Ruta(vista, query, null), null, null, ct);

    /// <summary>La misma vista con los mismos filtros, como archivo (<paramref name="formato"/> xlsx, pdf o docx).</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> DescargarAsync(string vista, string query, string formato, CancellationToken ct = default) =>
        EnvioDeVentas.DescargarAsync(http, auth, Ruta(vista, query, formato), $"{vista}.{formato}", ct);

    private static string Ruta(string vista, string query, string? formato)
    {
        var q = query.TrimStart('?');
        if (!string.IsNullOrEmpty(formato))
            q = string.IsNullOrEmpty(q) ? $"format={Uri.EscapeDataString(formato)}" : $"{q}&format={Uri.EscapeDataString(formato)}";
        return EnvioDeVentas.ConQuery($"{RutaDeInformes}/{Uri.EscapeDataString(vista)}", q);
    }
}

/// <summary>Una vista del centro de informes de Cartera como la publica el registro (api.md §17). (nuevo)</summary>
public sealed record VistaDeInformeDeCarteraDto
{
    public string Key { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string FileName { get; init; } = "";
    /// <summary>Los filtros comunes que usa (<c>from</c>, <c>to</c>, <c>asOf</c>, <c>branch</c>, <c>line</c>…).</summary>
    public IReadOnlyList<string> Filters { get; init; } = [];
    /// <summary>Los filtros propios (<c>groupBy</c>, <c>dateKind</c>, <c>ranges</c>…).</summary>
    public IReadOnlyList<string>? OwnFilters { get; init; }
    /// <summary>Trae datos del deudor: exportarla exige además <c>Lending.Reports.ExportPersonalData</c>.</summary>
    public bool PersonalData { get; init; }
    /// <summary>La entrega de Cartera desde la que existe (1 = K1).</summary>
    public int AvailableFrom { get; init; }
    public string? RequiredPermission { get; init; }
    /// <summary>K5: la vista no trae datos del deudor cuando su filtro propio vale uno de estos (la concentración por línea, sector o pagaduría).</summary>
    public ExcepcionDeDatosDelDeudorDto? PersonalDataExceptWhen { get; init; }

    /// <summary>¿La consulta con estos filtros propios trae datos del deudor?</summary>
    public bool TraeDatosDelDeudor(IReadOnlyDictionary<string, string> propios) =>
        PersonalData && (PersonalDataExceptWhen is not { } e
                         || !propios.TryGetValue(e.Filter, out var valor) || string.IsNullOrWhiteSpace(valor)
                         || !e.Values.Contains(valor.Trim(), StringComparer.OrdinalIgnoreCase));
}

/// <summary>Cuándo una vista con datos del deudor deja de traerlos: con <see cref="Filter"/> en uno de <see cref="Values"/>. (nuevo)</summary>
public sealed record ExcepcionDeDatosDelDeudorDto
{
    public string Filter { get; init; } = "";
    public IReadOnlyList<string> Values { get; init; } = [];
}
