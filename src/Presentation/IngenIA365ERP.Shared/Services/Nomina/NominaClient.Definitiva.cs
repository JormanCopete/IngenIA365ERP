using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Nomina;

/// <summary>
/// Feature 010, US3: terminación del contrato y liquidación definitiva (contracts/api.md §3.4) y las
/// dos vistas del centro de reportes que nacen con ella (§11). El detalle por empleado con
/// explicación, el resumen y los comprobantes son los de la corrida (<c>NominaClient.Liquidacion.cs</c>,
/// <c>Revision.cs</c>, <c>Pagos.cs</c>) por <c>runId</c>. Sin poner <c>Authorization</c> a mano.
/// </summary>
public sealed partial class NominaClient
{
    private const string Terminaciones = "/api/payroll/settlements/terminations";

    public Task<InvitationApiResult<IReadOnlyList<TerminacionDto>>> ListarTerminacionesAsync(int? anio = null, int? estado = null, Guid? empleadoId = null, CancellationToken ct = default)
    {
        var q = new List<string>();
        if (anio is { } a) q.Add($"year={a}");
        if (estado is { } e) q.Add($"status={e}");
        if (empleadoId is { } emp) q.Add($"employeeId={emp}");
        return EnviarAsync<IReadOnlyList<TerminacionDto>>(HttpMethod.Get, Terminaciones + (q.Count > 0 ? "?" + string.Join("&", q) : string.Empty), null, ct);
    }

    public Task<InvitationApiResult<TerminacionRegistradaDto>> RegistrarTerminacionAsync(RegistrarTerminacionRequest request, CancellationToken ct = default) =>
        EnviarAsync<TerminacionRegistradaDto>(HttpMethod.Post, Terminaciones, request, ct);

    public Task<InvitationApiResult<TerminacionRegistradaDto>> RecalcularDefinitivaAsync(Guid runId, CancellationToken ct = default) =>
        EnviarAsync<TerminacionRegistradaDto>(HttpMethod.Post, $"{Terminaciones}/{runId}/recalculate", new { }, ct);

    public Task<InvitationApiResult<DefinitivaAprobadaDto>> AprobarDefinitivaAsync(Guid runId, AprobarLiquidacionRequest request, CancellationToken ct = default) =>
        EnviarAsync<DefinitivaAprobadaDto>(HttpMethod.Post, $"{Terminaciones}/{runId}/approve", request, ct);

    public Task<InvitationApiResult<DefinitivaReversadaDto>> ReversarDefinitivaAsync(Guid runId, string motivo, CancellationToken ct = default) =>
        EnviarAsync<DefinitivaReversadaDto>(HttpMethod.Post, $"{Terminaciones}/{runId}/reverse", new MotivoDeLiquidacionRequest(motivo), ct);

    public Task<InvitationApiResult<LiquidacionDescartadaDto>> DescartarDefinitivaAsync(Guid runId, string motivo, CancellationToken ct = default) =>
        EnviarAsync<LiquidacionDescartadaDto>(HttpMethod.Post, $"{Terminaciones}/{runId}/discard", new MotivoDeLiquidacionRequest(motivo), ct);

    // ---------------------------------------------------------------- descuentos --

    public Task<InvitationApiResult<DescuentosDefinitivaDto>> DescuentosDeDefinitivaAsync(Guid runId, CancellationToken ct = default) =>
        EnviarAsync<DescuentosDefinitivaDto>(HttpMethod.Get, $"{Terminaciones}/{runId}/deductions", null, ct);

    public Task<InvitationApiResult<DescuentoDefinitivaDto>> AjustarDescuentoAsync(Guid runId, Guid obligacionId, AjustarDescuentoRequest request, CancellationToken ct = default) =>
        EnviarAsync<DescuentoDefinitivaDto>(HttpMethod.Put, $"{Terminaciones}/{runId}/deductions/{obligacionId}", request, ct);

    // ---------------------------------------------------- documento y sanción --

    public Task<InvitationApiResult<ArchivoDescargado>> DocumentoDeDefinitivaAsync(Guid runId, CancellationToken ct = default) =>
        DescargarAsync($"{Terminaciones}/{runId}/document", ct);

    public Task<InvitationApiResult<SancionMoratoriaDto>> SancionMoratoriaAsync(Guid runId, DateOnly? aFecha = null, CancellationToken ct = default) =>
        EnviarAsync<SancionMoratoriaDto>(HttpMethod.Get, $"{Terminaciones}/{runId}/late-payment-penalty" + (aFecha is { } f ? $"?asOf={f:yyyy-MM-dd}" : string.Empty), null, ct);

    // ------------------------------------------------------------------ motivos --

    public Task<InvitationApiResult<IReadOnlyList<MotivoDeRetiroDto>>> ListarMotivosDeRetiroAsync(bool incluirInactivos = false, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<MotivoDeRetiroDto>>(HttpMethod.Get, $"{Terminaciones}/reasons?includeInactive={(incluirInactivos ? "true" : "false")}", null, ct);

    public Task<InvitationApiResult<CreadoDto>> CrearMotivoDeRetiroAsync(GuardarMotivoDeRetiroRequest request, CancellationToken ct = default) =>
        EnviarAsync<CreadoDto>(HttpMethod.Post, $"{Terminaciones}/reasons", request, ct);

    public Task<InvitationApiResult<EmptyResponse>> ActualizarMotivoDeRetiroAsync(Guid id, GuardarMotivoDeRetiroRequest request, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Put, $"{Terminaciones}/reasons/{id}", new { PublicId = id, request.Code, request.Name, request.GeneratesSeverancePay, request.RequiresContractEndDate, request.LegalBasis, request.IsActive }, ct);

    public Task<InvitationApiResult<EmptyResponse>> DesactivarMotivoDeRetiroAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"{Terminaciones}/reasons/{id}/deactivate", new { }, ct);

    // ----------------------------------------------------------------- reportes --

    /// <summary>Vista <c>terminaciones</c> del centro de reportes (contracts/api.md §11), en pantalla o como archivo.</summary>
    public Task<InvitationApiResult<TablaReporteDto>> ReporteDeTerminacionesAsync(DateOnly desde, DateOnly hasta, CancellationToken ct = default) =>
        ReporteAsync($"terminaciones?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}", ct);

    public Task<InvitationApiResult<ArchivoDescargado>> DescargarReporteDeTerminacionesAsync(DateOnly desde, DateOnly hasta, string formato, CancellationToken ct = default) =>
        DescargarReporteAsync($"terminaciones?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}", formato, ct);

    /// <summary>Vista <c>saldos-iniciales-prestaciones</c> del centro de reportes.</summary>
    public Task<InvitationApiResult<TablaReporteDto>> ReporteDeSaldosInicialesAsync(DateOnly? vigenteA = null, CancellationToken ct = default) =>
        ReporteAsync("saldos-iniciales-prestaciones" + (vigenteA is { } d ? $"?asOf={d:yyyy-MM-dd}" : string.Empty), ct);

    public Task<InvitationApiResult<ArchivoDescargado>> DescargarReporteDeSaldosInicialesAsync(DateOnly? vigenteA, string formato, CancellationToken ct = default) =>
        DescargarReporteAsync("saldos-iniciales-prestaciones" + (vigenteA is { } d ? $"?asOf={d:yyyy-MM-dd}" : string.Empty), formato, ct);
}
