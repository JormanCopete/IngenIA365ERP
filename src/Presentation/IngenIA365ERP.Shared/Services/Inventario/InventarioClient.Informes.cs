using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Reportes;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Inventario;

/// <summary>
/// El centro de informes de Inventario (feature 012, T182; contracts/api.md §27): el registro de vistas publicadas
/// (<c>GET /api/reports/inventory</c>) y cada vista en <c>/api/reports/inventory/{vista}</c>, en JSON para la pantalla
/// (<c>TablaDeReporte</c>) o como archivo (<c>xlsx</c>, <c>pdf</c>, <c>docx</c>). Mismo molde que
/// <c>ContabilidadClient.Informes</c>. El tablero (T969) suma aquí su método.
/// </summary>
public sealed partial class InventarioClient
{
    public const string RutaDeInformes = "/api/reports/inventory";

    /// <summary>Las vistas publicadas que la persona puede ver (sin las que exigen un permiso adicional que no tiene).</summary>
    public Task<ResultadoDeInventario<IReadOnlyList<VistaDeInformeDto>>> VistasDeInformeAsync(CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<VistaDeInformeDto>>(HttpMethod.Get, RutaDeInformes, null, null, ct);

    /// <summary>La tabla de una vista con los filtros dados (<paramref name="query"/> sin «?»; puede venir vacía).</summary>
    public Task<ResultadoDeInventario<TablaReporteDto>> InformeAsync(string vista, string query, CancellationToken ct = default) =>
        EnviarAsync<TablaReporteDto>(HttpMethod.Get, RutaDeInforme(vista, query, null), null, null, ct);

    /// <summary>La misma vista con los mismos filtros, como archivo (<paramref name="formato"/> xlsx, pdf o docx).</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> DescargarInformeAsync(string vista, string query, string formato, CancellationToken ct = default) =>
        DescargarAsync(RutaDeInforme(vista, query, formato), $"{vista}.{formato}", ct);

    private static string RutaDeInforme(string vista, string query, string? formato)
    {
        var q = query.TrimStart('?');
        if (!string.IsNullOrEmpty(formato))
            q = string.IsNullOrEmpty(q) ? $"format={Uri.EscapeDataString(formato)}" : $"{q}&format={Uri.EscapeDataString(formato)}";
        return ConQuery($"{RutaDeInformes}/{Uri.EscapeDataString(vista)}", q);
    }
}
