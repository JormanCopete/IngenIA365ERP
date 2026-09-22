using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Nomina;

/// <summary>Feature 010 US4: saldo, movimientos y liquidación de vacaciones (contracts/api.md §5 y §3.3) y sus dos vistas del centro de reportes (§11).</summary>
public sealed partial class NominaClient
{
    // ------------------------------------------------------------ saldo y movimientos --

    public Task<InvitationApiResult<IReadOnlyList<SaldoVacacionesDto>>> SaldosDeVacacionesAsync(DateOnly? alDia = null, string? buscar = null, CancellationToken ct = default)
    {
        var q = new List<string>();
        if (alDia is { } d) q.Add($"asOf={d:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(buscar)) q.Add($"search={Uri.EscapeDataString(buscar)}");
        var url = "/api/payroll/vacations/balances" + (q.Count > 0 ? "?" + string.Join("&", q) : string.Empty);
        return EnviarAsync<IReadOnlyList<SaldoVacacionesDto>>(HttpMethod.Get, url, null, ct);
    }

    public Task<InvitationApiResult<SaldoVacacionesDetalleDto>> SaldoDeVacacionesAsync(Guid empleadoId, DateOnly? alDia = null, CancellationToken ct = default) =>
        EnviarAsync<SaldoVacacionesDetalleDto>(HttpMethod.Get, $"/api/payroll/vacations/employees/{empleadoId}/balance" + (alDia is { } d ? $"?asOf={d:yyyy-MM-dd}" : string.Empty), null, ct);

    public Task<InvitationApiResult<IReadOnlyList<MovimientoVacacionesDto>>> MovimientosDeVacacionesAsync(Guid empleadoId, bool incluirAnulados = true, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<MovimientoVacacionesDto>>(HttpMethod.Get, $"/api/payroll/vacations/employees/{empleadoId}/movements?includeCancelled={(incluirAnulados ? "true" : "false")}", null, ct);

    /// <summary>La vista previa obligatoria antes de guardar un disfrute (FR-015): hábiles, calendario y cada día saltado.</summary>
    public Task<InvitationApiResult<VistaPreviaHabilesDto>> DiasHabilesAsync(DateOnly desde, DateOnly hasta, Guid? empleadoId = null, CancellationToken ct = default) =>
        EnviarAsync<VistaPreviaHabilesDto>(HttpMethod.Post, "/api/payroll/vacations/working-days", new { From = desde, To = hasta, EmployeePublicId = empleadoId }, ct);

    public Task<InvitationApiResult<MovimientoCreadoDto>> AjustarVacacionesAsync(Guid empleadoId, decimal dias, string motivo, CancellationToken ct = default) =>
        EnviarAsync<MovimientoCreadoDto>(HttpMethod.Post, $"/api/payroll/vacations/employees/{empleadoId}/adjustments", new { Days = dias, Reason = motivo }, ct);

    public Task<InvitationApiResult<EmptyResponse>> AnularMovimientoDeVacacionesAsync(Guid movimientoId, string motivo, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"/api/payroll/vacations/movements/{movimientoId}/cancel", new { Reason = motivo }, ct);

    // ------------------------------------------------------------------ liquidación --

    public Task<InvitationApiResult<IReadOnlyList<LiquidacionVacacionesDto>>> LiquidacionesDeVacacionesAsync(Guid? empleadoId = null, int? anio = null, string? estado = null, bool incluirReemplazadas = false, CancellationToken ct = default)
    {
        var q = new List<string>();
        if (empleadoId is { } e) q.Add($"employeeId={e}");
        if (anio is { } a) q.Add($"year={a}");
        if (!string.IsNullOrWhiteSpace(estado)) q.Add($"status={Uri.EscapeDataString(estado)}");
        if (incluirReemplazadas) q.Add("includeSuperseded=true");
        var url = "/api/payroll/settlements/vacations" + (q.Count > 0 ? "?" + string.Join("&", q) : string.Empty);
        return EnviarAsync<IReadOnlyList<LiquidacionVacacionesDto>>(HttpMethod.Get, url, null, ct);
    }

    /// <summary>Registra el disfrute o la compensación y calcula su liquidación en una acción; <c>Kind</c> viaja por nombre («Enjoyment», «Compensation»).</summary>
    public Task<InvitationApiResult<VacacionesCalculadasDto>> RegistrarVacacionesAsync(RegistrarVacacionesRequest request, CancellationToken ct = default) =>
        EnviarAsync<VacacionesCalculadasDto>(HttpMethod.Post, "/api/payroll/settlements/vacations", request, ct);

    public Task<InvitationApiResult<VacacionesCalculadasDto>> RecalcularVacacionesAsync(Guid corridaId, bool aceptarRetroactivo = false, CancellationToken ct = default) =>
        EnviarAsync<VacacionesCalculadasDto>(HttpMethod.Post, $"/api/payroll/settlements/vacations/{corridaId}/recalculate", new { AcceptRetroactive = aceptarRetroactivo }, ct);

    public Task<InvitationApiResult<LiquidacionAprobadaDto>> AprobarVacacionesAsync(Guid corridaId, AprobarLiquidacionRequest request, CancellationToken ct = default) =>
        EnviarAsync<LiquidacionAprobadaDto>(HttpMethod.Post, $"/api/payroll/settlements/vacations/{corridaId}/approve", request, ct);

    public Task<InvitationApiResult<LiquidacionReversadaDto>> ReversarVacacionesAsync(Guid corridaId, string motivo, CancellationToken ct = default) =>
        EnviarAsync<LiquidacionReversadaDto>(HttpMethod.Post, $"/api/payroll/settlements/vacations/{corridaId}/reverse", new MotivoDeLiquidacionRequest(motivo), ct);

    public Task<InvitationApiResult<LiquidacionDescartadaDto>> DescartarVacacionesAsync(Guid corridaId, string motivo, CancellationToken ct = default) =>
        EnviarAsync<LiquidacionDescartadaDto>(HttpMethod.Post, $"/api/payroll/settlements/vacations/{corridaId}/discard", new MotivoDeLiquidacionRequest(motivo), ct);

    // --------------------------------------------------------------------- reportes --

    /// <summary>Las vistas <c>saldos-vacaciones</c> y <c>movimientos-vacaciones</c> del centro de reportes, en el formato pedido (xlsx, pdf, docx).</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> DescargarSaldosDeVacacionesAsync(DateOnly alDia, string formato, string? buscar = null, CancellationToken ct = default) =>
        DescargarReporteAsync($"saldos-vacaciones?asOf={alDia:yyyy-MM-dd}" + (string.IsNullOrWhiteSpace(buscar) ? string.Empty : $"&search={Uri.EscapeDataString(buscar)}"), formato, ct);

    public Task<InvitationApiResult<ArchivoDescargado>> DescargarMovimientosDeVacacionesAsync(DateOnly desde, DateOnly hasta, string formato, Guid? empleadoId = null, CancellationToken ct = default) =>
        DescargarReporteAsync($"movimientos-vacaciones?desde={desde:yyyy-MM-dd}&hasta={hasta:yyyy-MM-dd}" + (empleadoId is { } e ? $"&employeeId={e}" : string.Empty), formato, ct);
}
