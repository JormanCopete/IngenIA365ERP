using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Nomina;

/// <summary>
/// Feature 010 US2: cesantías e intereses del año y la consignación por fondo
/// (<c>/api/payroll/settlements/severance</c>, contracts/api.md §3.2). El detalle por empleado, la
/// relación de pago de los intereses, los comprobantes y el cuadre son los de la corrida
/// (<c>NominaClient.Liquidacion.cs</c>, <c>Pagos.cs</c>, <c>Revision.cs</c>) porque una liquidación
/// especial es una corrida más. Sin <c>Authorization</c> a mano: la pone el handler.
/// </summary>
public sealed partial class NominaClient
{
    private const string RutaCesantias = "/api/payroll/settlements/severance";

    public Task<InvitationApiResult<IReadOnlyList<LiquidacionCesantiasDto>>> ListarCesantiasAnualesAsync(int? anio = null, string? estado = null, CancellationToken ct = default)
    {
        var q = new List<string>();
        if (anio is { } a) q.Add($"year={a}");
        if (!string.IsNullOrWhiteSpace(estado)) q.Add($"status={Uri.EscapeDataString(estado)}");
        return EnviarAsync<IReadOnlyList<LiquidacionCesantiasDto>>(HttpMethod.Get, RutaCesantias + (q.Count > 0 ? "?" + string.Join("&", q) : string.Empty), null, ct);
    }

    public Task<InvitationApiResult<CesantiasCalculadaDto>> CalcularCesantiasAsync(CalcularCesantiasRequest request, CancellationToken ct = default) =>
        EnviarAsync<CesantiasCalculadaDto>(HttpMethod.Post, RutaCesantias, request, ct);

    public Task<InvitationApiResult<CesantiasCalculadaDto>> RecalcularCesantiasAsync(Guid corridaId, CancellationToken ct = default) =>
        EnviarAsync<CesantiasCalculadaDto>(HttpMethod.Post, $"{RutaCesantias}/{corridaId}/recalculate", new { }, ct);

    public Task<InvitationApiResult<CesantiasAprobadaDto>> AprobarCesantiasAsync(Guid corridaId, AprobarCesantiasRequest request, CancellationToken ct = default) =>
        EnviarAsync<CesantiasAprobadaDto>(HttpMethod.Post, $"{RutaCesantias}/{corridaId}/approve", request, ct);

    public Task<InvitationApiResult<CesantiasReversadaDto>> ReversarCesantiasAsync(Guid corridaId, string motivo, CancellationToken ct = default) =>
        EnviarAsync<CesantiasReversadaDto>(HttpMethod.Post, $"{RutaCesantias}/{corridaId}/reverse", new CesantiasMotivoRequest(motivo), ct);

    public Task<InvitationApiResult<CesantiasDescartadaDto>> DescartarCesantiasAsync(Guid corridaId, string motivo, CancellationToken ct = default) =>
        EnviarAsync<CesantiasDescartadaDto>(HttpMethod.Post, $"{RutaCesantias}/{corridaId}/discard", new CesantiasMotivoRequest(motivo), ct);

    public Task<InvitationApiResult<RelacionDeConsignacionDto>> RelacionDeConsignacionAsync(Guid corridaId, CancellationToken ct = default) =>
        EnviarAsync<RelacionDeConsignacionDto>(HttpMethod.Get, $"{RutaCesantias}/{corridaId}/deposit-schedule", null, ct);

    public Task<InvitationApiResult<ConsignacionRegistradaDto>> MarcarConsignadoAsync(Guid corridaId, Guid fondoId, MarcarConsignadoRequest request, CancellationToken ct = default) =>
        EnviarAsync<ConsignacionRegistradaDto>(HttpMethod.Post, $"{RutaCesantias}/{corridaId}/funds/{fondoId}/mark-deposited", request, ct);

    /// <summary>El archivo plano del fondo; hasta N4 responde 422 <c>Payroll.Severance.FundFormatMissing</c>.</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> ArchivoDelFondoAsync(Guid corridaId, Guid fondoId, Guid? formatoId = null, CancellationToken ct = default) =>
        DescargarAsync($"{RutaCesantias}/{corridaId}/deposit-schedule/{fondoId}/file" + (formatoId is { } f ? $"?formatId={f}" : string.Empty), ct);

    /// <summary>La relación de consignación del centro de reportes (vista <c>consignacion-cesantias</c>): xlsx con una hoja por fondo, pdf o docx.</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> DescargarConsignacionAsync(Guid corridaId, string formato, Guid? fondoId = null, CancellationToken ct = default) =>
        DescargarAsync($"/api/reports/payroll/consignacion-cesantias?runId={corridaId}" + (fondoId is { } f ? $"&fundId={f}" : string.Empty) + $"&format={formato}", ct);
}
