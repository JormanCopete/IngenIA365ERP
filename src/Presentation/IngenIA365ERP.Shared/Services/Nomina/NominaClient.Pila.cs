using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Nomina;

/// <summary>Feature 010 (US5): la planilla PILA (contracts/api.md §7).</summary>
public sealed partial class NominaClient
{
    private const string RutaPila = "/api/payroll/pila";

    public Task<InvitationApiResult<DatosDelAportanteDto>> DatosDelAportanteAsync(CancellationToken ct = default) =>
        EnviarAsync<DatosDelAportanteDto>(HttpMethod.Get, $"{RutaPila}/settings", null, ct);

    public Task<InvitationApiResult<DatosDelAportanteDto>> GuardarDatosDelAportanteAsync(DatosDelAportanteRequest request, CancellationToken ct = default) =>
        EnviarAsync<DatosDelAportanteDto>(HttpMethod.Put, $"{RutaPila}/settings", request, ct);

    public Task<InvitationApiResult<IReadOnlyList<LayoutPilaDto>>> LayoutsPilaAsync(CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<LayoutPilaDto>>(HttpMethod.Get, $"{RutaPila}/layouts", null, ct);

    public Task<InvitationApiResult<FechaLimitePilaDto>> FechaLimitePilaAsync(int anio, int mes, CancellationToken ct = default) =>
        EnviarAsync<FechaLimitePilaDto>(HttpMethod.Get, $"{RutaPila}/{anio}/{mes}/due-date", null, ct);

    public Task<InvitationApiResult<ValidacionPilaDto>> ValidarPilaAsync(int anio, int mes, CancellationToken ct = default) =>
        EnviarAsync<ValidacionPilaDto>(HttpMethod.Post, $"{RutaPila}/{anio}/{mes}/validate", null, ct);

    public Task<InvitationApiResult<PilaGeneradaDto>> GenerarPilaAsync(int anio, int mes, bool reconocerAlertas, CancellationToken ct = default) =>
        EnviarAsync<PilaGeneradaDto>(HttpMethod.Post, $"{RutaPila}/{anio}/{mes}/generate", new { acknowledgeWarnings = reconocerAlertas }, ct);

    public Task<InvitationApiResult<IReadOnlyList<GeneracionPilaDto>>> ListarPilaAsync(int? anio = null, int? mes = null, CancellationToken ct = default)
    {
        var q = new List<string>();
        if (anio is { } a) q.Add($"year={a}");
        if (mes is { } m) q.Add($"month={m}");
        return EnviarAsync<IReadOnlyList<GeneracionPilaDto>>(HttpMethod.Get, RutaPila + (q.Count > 0 ? "?" + string.Join("&", q) : string.Empty), null, ct);
    }

    public Task<InvitationApiResult<DetalleDePilaDto>> PilaAsync(Guid generacionId, CancellationToken ct = default) =>
        EnviarAsync<DetalleDePilaDto>(HttpMethod.Get, $"{RutaPila}/{generacionId}", null, ct);

    public Task<InvitationApiResult<ExplicacionDeLineaPilaDto>> ExplicacionDeLineaPilaAsync(Guid generacionId, int linea, CancellationToken ct = default) =>
        EnviarAsync<ExplicacionDeLineaPilaDto>(HttpMethod.Get, $"{RutaPila}/{generacionId}/lines/{linea}/explanation", null, ct);

    /// <summary>El <c>.txt</c>; con diferencia en el cuadre exige <paramref name="reconocerDiferencia"/> (FR-027).</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> DescargarPilaAsync(Guid generacionId, bool reconocerDiferencia = false, CancellationToken ct = default) =>
        DescargarAsync($"{RutaPila}/{generacionId}/file" + (reconocerDiferencia ? "?acknowledgeDifference=true" : string.Empty), ct);

    public Task<InvitationApiResult<PilaCargadaDto>> MarcarPilaCargadaAsync(Guid generacionId, MarcarCargadaRequest request, CancellationToken ct = default) =>
        EnviarAsync<PilaCargadaDto>(HttpMethod.Post, $"{RutaPila}/{generacionId}/mark-uploaded", request, ct);

    /// <summary>Vistas <c>pila-lineas</c>, <c>pila-cuadre</c> y <c>pila-inconsistencias</c> del centro de reportes.</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> DescargarReportePilaAsync(string vista, Guid generacionId, string formato, CancellationToken ct = default) =>
        DescargarAsync($"/api/reports/payroll/{vista}?generationId={generacionId}&format={formato}", ct);
}
