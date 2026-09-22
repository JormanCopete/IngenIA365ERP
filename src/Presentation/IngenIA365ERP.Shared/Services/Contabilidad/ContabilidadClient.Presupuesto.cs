using System.Globalization;
using IngenIA365ERP.Shared.Services.Nomina;
using IngenIA365ERP.Shared.Services.Reportes;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Contabilidad;

/// <summary>
/// Presupuesto y ejecución presupuestal (feature 009 E2, US9, FR-061..FR-064) sobre
/// <c>/api/accounting/budgets</c>. La ejecución de la pantalla de presupuesto va por
/// <c>/api/accounting/budgets/execution</c>, que exige <c>Accounting.Budget.View</c> como el resto de
/// la pantalla; la misma vista existe como <c>budget-execution</c> en <see cref="InformeAsync"/> para el
/// centro de informes, con <c>Accounting.Reports.View</c>. Hasta el 2026-09-20 la pestaña pedía la del
/// centro de informes y un rol con sólo <c>Budget.View</c> veía «No se pudo consultar la ejecución».
/// </summary>
public sealed partial class ContabilidadClient
{
    private const string BasePresupuesto = "/api/accounting/budgets";

    /// <summary>El presupuesto vigente del año, o la versión pedida. Sin presupuesto responde éxito con <c>Status = "None"</c>, no 404.</summary>
    public Task<InvitationApiResult<PresupuestoDto>> ObtenerPresupuestoAsync(int year, int? version = null, CancellationToken ct = default) =>
        EnviarAsync<PresupuestoDto>(HttpMethod.Get,
            $"{BasePresupuesto}?year={year}{(version is { } v ? $"&version={v}" : string.Empty)}", null, ct);

    public Task<InvitationApiResult<PresupuestoDto>> CrearPresupuestoAsync(int year, IReadOnlyList<LineaPresupuestoInput> lineas, CancellationToken ct = default) =>
        EnviarAsync<PresupuestoDto>(HttpMethod.Post, BasePresupuesto, new CrearPresupuestoRequest(year, lineas), ct);

    /// <summary>Sobre un borrador reemplaza las líneas; sobre un aprobado exige <paramref name="motivo"/> y crea la versión siguiente.</summary>
    public Task<InvitationApiResult<PresupuestoDto>> ActualizarPresupuestoAsync(int year, IReadOnlyList<LineaPresupuestoInput> lineas, string? motivo, CancellationToken ct = default) =>
        EnviarAsync<PresupuestoDto>(HttpMethod.Put, $"{BasePresupuesto}/{year}", new ActualizarPresupuestoRequest(lineas, motivo), ct);

    public Task<InvitationApiResult<PresupuestoDto>> AprobarPresupuestoAsync(int year, CancellationToken ct = default) =>
        EnviarAsync<PresupuestoDto>(HttpMethod.Post, $"{BasePresupuesto}/{year}/approve", null, ct);

    /// <summary>Copia la versión vigente de <paramref name="anterior"/> al borrador de <paramref name="year"/>, ajustada en <paramref name="porcentaje"/> % y redondeada a pesos.</summary>
    public Task<InvitationApiResult<PresupuestoDto>> CopiarPresupuestoAsync(int year, int anterior, decimal porcentaje, CancellationToken ct = default) =>
        EnviarAsync<PresupuestoDto>(HttpMethod.Post,
            $"{BasePresupuesto}/{year}/copy-from/{anterior}?adjustPercent={porcentaje.ToString(CultureInfo.InvariantCulture)}", null, ct);

    public Task<InvitationApiResult<PresupuestoDto>> DistribuirPresupuestoAsync(int year, DistribucionInput input, CancellationToken ct = default) =>
        EnviarAsync<PresupuestoDto>(HttpMethod.Post, $"{BasePresupuesto}/{year}/distribute", input, ct);

    // ---------------------------------------------------------------------------------- ejecución --

    /// <summary>La tabla de la ejecución presupuestal con <paramref name="query"/> (<c>year</c>, <c>month</c> y los filtros de informe, sin «?»).</summary>
    public Task<InvitationApiResult<TablaReporteDto>> EjecucionPresupuestalAsync(string query, CancellationToken ct = default) =>
        EnviarAsync<TablaReporteDto>(HttpMethod.Get, RutaDeEjecucionPresupuestal(query, null), null, ct);

    /// <summary>La misma ejecución como archivo (<paramref name="formato"/> xlsx, pdf o docx); exige además <c>Accounting.Reports.Export</c>.</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> DescargarEjecucionPresupuestalAsync(string query, string formato, CancellationToken ct = default) =>
        DescargarAsync(RutaDeEjecucionPresupuestal(query, formato), ct);

    /// <summary>La ruta de la ejecución bajo el presupuesto; pública para que la prueba fije que la pantalla no vuelve a la del centro de informes.</summary>
    public static string RutaDeEjecucionPresupuestal(string query, string? formato)
    {
        var q = query.TrimStart('?');
        if (!string.IsNullOrEmpty(formato)) q = FiltrosDeInformeModelo.Unir(q, $"format={Uri.EscapeDataString(formato)}");
        return string.IsNullOrEmpty(q) ? $"{BasePresupuesto}/execution" : $"{BasePresupuesto}/execution?{q}";
    }
}
