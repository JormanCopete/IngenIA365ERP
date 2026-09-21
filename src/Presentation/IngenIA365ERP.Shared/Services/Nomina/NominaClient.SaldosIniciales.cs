using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Nomina;

/// <summary>Feature 010: saldos iniciales de prestaciones (contracts/api.md §4) y la ficha ampliada (§12).</summary>
public sealed partial class NominaClient
{
    public Task<InvitationApiResult<IReadOnlyList<SaldoInicialResumenDto>>> ListarSaldosInicialesAsync(
        DateOnly? vigenteA = null, bool soloFaltantes = false, string? buscar = null, CancellationToken ct = default)
    {
        var q = new List<string>();
        if (vigenteA is { } d) q.Add($"asOf={d:yyyy-MM-dd}");
        if (soloFaltantes) q.Add("onlyMissing=true");
        if (!string.IsNullOrWhiteSpace(buscar)) q.Add($"search={Uri.EscapeDataString(buscar)}");
        var url = "/api/payroll/benefit-balances" + (q.Count > 0 ? "?" + string.Join("&", q) : string.Empty);
        return EnviarAsync<IReadOnlyList<SaldoInicialResumenDto>>(HttpMethod.Get, url, null, ct);
    }

    public Task<InvitationApiResult<SaldoInicialDetalleDto>> SaldoInicialDeAsync(Guid empleadoId, CancellationToken ct = default) =>
        EnviarAsync<SaldoInicialDetalleDto>(HttpMethod.Get, $"/api/payroll/benefit-balances/{empleadoId}", null, ct);

    public Task<InvitationApiResult<CreadoDto>> GuardarSaldoInicialAsync(Guid empleadoId, GuardarSaldoInicialRequest request, CancellationToken ct = default) =>
        EnviarAsync<CreadoDto>(HttpMethod.Put, $"/api/payroll/benefit-balances/{empleadoId}", request, ct);

    public Task<InvitationApiResult<CreadoDto>> AjustarSaldoInicialAsync(Guid empleadoId, AjustarSaldoInicialRequest request, CancellationToken ct = default) =>
        EnviarAsync<CreadoDto>(HttpMethod.Post, $"/api/payroll/benefit-balances/{empleadoId}/adjustments", request, ct);

    // ----------------------------------------------------------------- ficha --

    public Task<InvitationApiResult<FichaEmpleadoDto>> FichaDeEmpleadoAsync(Guid empleadoId, CancellationToken ct = default) =>
        EnviarAsync<FichaEmpleadoDto>(HttpMethod.Get, $"/api/payroll/employees/{empleadoId}", null, ct);
}
