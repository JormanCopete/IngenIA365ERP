using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Nomina;

/// <summary>
/// Feature 010, US1: prima de servicios (contracts/api.md §3.1, <c>/api/payroll/settlements/service-bonus</c>).
/// Listar, calcular, recalcular, aprobar, reversar y descartar; el detalle por empleado, la relación de
/// pago y los comprobantes reutilizan <c>NominaClient.Liquidacion.cs</c>, <c>Pagos.cs</c> y <c>Revision.cs</c>
/// por <c>runId</c>. Las dos vistas del centro de reportes de liquidaciones especiales se piden desde aquí.
/// La cabecera <c>Authorization</c> la pone el handler HTTP, no el cliente.
/// </summary>
public sealed partial class NominaClient
{
    private const string RutaPrima = "/api/payroll/settlements/service-bonus";

    public Task<InvitationApiResult<IReadOnlyList<PrimaResumenDto>>> ListarPrimasAsync(int? anio = null, string? estado = null, CancellationToken ct = default)
    {
        var q = new List<string>();
        if (anio is { } a) q.Add($"year={a}");
        if (!string.IsNullOrWhiteSpace(estado)) q.Add($"status={Uri.EscapeDataString(estado)}");
        var url = RutaPrima + (q.Count > 0 ? "?" + string.Join("&", q) : string.Empty);
        return EnviarAsync<IReadOnlyList<PrimaResumenDto>>(HttpMethod.Get, url, null, ct);
    }

    public Task<InvitationApiResult<ResultadoPrimaDto>> CalcularPrimaAsync(CalcularPrimaRequest request, CancellationToken ct = default) =>
        EnviarAsync<ResultadoPrimaDto>(HttpMethod.Post, RutaPrima, request, ct);

    public Task<InvitationApiResult<ResultadoPrimaDto>> RecalcularPrimaAsync(Guid corridaId, CancellationToken ct = default) =>
        EnviarAsync<ResultadoPrimaDto>(HttpMethod.Post, $"{RutaPrima}/{corridaId}/recalculate", new { }, ct);

    public Task<InvitationApiResult<IReadOnlyList<ExcluidoDePrimaDto>>> ExcluidosDePrimaAsync(Guid corridaId, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<ExcluidoDePrimaDto>>(HttpMethod.Get, $"{RutaPrima}/{corridaId}/excluded", null, ct);

    public Task<InvitationApiResult<PrimaAprobadaDto>> AprobarPrimaAsync(Guid corridaId, AprobarPrimaRequest request, CancellationToken ct = default) =>
        EnviarAsync<PrimaAprobadaDto>(HttpMethod.Post, $"{RutaPrima}/{corridaId}/approve", request, ct);

    public Task<InvitationApiResult<PrimaReversadaDto>> ReversarPrimaAsync(Guid corridaId, string motivo, CancellationToken ct = default) =>
        EnviarAsync<PrimaReversadaDto>(HttpMethod.Post, $"{RutaPrima}/{corridaId}/reverse", new { Reason = motivo }, ct);

    public Task<InvitationApiResult<PrimaDescartadaDto>> DescartarPrimaAsync(Guid corridaId, string motivo, CancellationToken ct = default) =>
        EnviarAsync<PrimaDescartadaDto>(HttpMethod.Post, $"{RutaPrima}/{corridaId}/discard", new { Reason = motivo }, ct);

    // ------------------------------------------- centro de reportes de liquidaciones especiales --

    /// <summary>Vista <c>liquidacion-especial-resumen</c> o <c>liquidacion-especial-detalle</c> en JSON, para pintarla.</summary>
    public Task<InvitationApiResult<TablaReporteDto>> ReporteDeLiquidacionAsync(string vista, Guid corridaId, CancellationToken ct = default) =>
        EnviarAsync<TablaReporteDto>(HttpMethod.Get, $"/api/reports/payroll/{vista}?runId={corridaId}", null, ct);

    /// <summary>La misma vista como archivo (<c>xlsx</c>, <c>pdf</c>, <c>docx</c>); queda en auditoría.</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> DescargarReporteDeLiquidacionAsync(string vista, Guid corridaId, string formato, CancellationToken ct = default) =>
        DescargarAsync($"/api/reports/payroll/{vista}?runId={corridaId}&format={Uri.EscapeDataString(formato)}", ct);
}
