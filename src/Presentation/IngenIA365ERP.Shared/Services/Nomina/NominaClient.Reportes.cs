using IngenIA365ERP.Shared.Services.Reportes;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Nomina;

/// <summary>Feature 006 US5: centro de reportes de nómina (<c>/api/reports/payroll/{vista}</c>).</summary>
public sealed partial class NominaClient
{
    public Task<InvitationApiResult<TablaReporteDto>> ReporteAsync(string ruta, CancellationToken ct = default) =>
        EnviarAsync<TablaReporteDto>(HttpMethod.Get, $"/api/reports/payroll/{ruta}", null, ct);

    public Task<InvitationApiResult<ArchivoDescargado>> DescargarReporteAsync(string ruta, string formato, CancellationToken ct = default) =>
        DescargarAsync($"/api/reports/payroll/{ruta}{(ruta.Contains('?') ? "&" : "?")}format={formato}", ct);
}
