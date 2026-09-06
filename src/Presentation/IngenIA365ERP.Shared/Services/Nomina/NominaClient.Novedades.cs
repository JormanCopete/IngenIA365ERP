using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Nomina;

/// <summary>US1: novedades del período y cambios de salario (contracts/api.md §3).</summary>
public sealed partial class NominaClient
{
    public Task<InvitationApiResult<IReadOnlyList<NovedadDto>>> ListarNovedadesAsync(
        Guid periodoId, Guid? empleadoId = null, string? concepto = null, string? estado = null, string? origen = null, string? buscar = null,
        CancellationToken ct = default)
    {
        var q = new List<string>();
        if (empleadoId is { } e) q.Add($"employeeId={e}");
        if (!string.IsNullOrWhiteSpace(concepto)) q.Add($"conceptCode={Uri.EscapeDataString(concepto)}");
        if (!string.IsNullOrWhiteSpace(estado)) q.Add($"status={Uri.EscapeDataString(estado)}");
        if (!string.IsNullOrWhiteSpace(origen)) q.Add($"origin={Uri.EscapeDataString(origen)}");
        if (!string.IsNullOrWhiteSpace(buscar)) q.Add($"search={Uri.EscapeDataString(buscar)}");
        var url = $"/api/payroll/pay-periods/{periodoId}/novelties" + (q.Count > 0 ? "?" + string.Join("&", q) : string.Empty);
        return EnviarAsync<IReadOnlyList<NovedadDto>>(HttpMethod.Get, url, null, ct);
    }

    public Task<InvitationApiResult<CreadoDto>> RegistrarNovedadAsync(Guid periodoId, RegistrarNovedadRequest request, CancellationToken ct = default) =>
        EnviarAsync<CreadoDto>(HttpMethod.Post, $"/api/payroll/pay-periods/{periodoId}/novelties", request, ct);

    public Task<InvitationApiResult<CreadoDto>> CorregirNovedadAsync(Guid novedadId, CorregirNovedadRequest request, CancellationToken ct = default) =>
        EnviarAsync<CreadoDto>(HttpMethod.Put, $"/api/payroll/novelties/{novedadId}", request, ct);

    public Task<InvitationApiResult<EmptyResponse>> AnularNovedadAsync(Guid novedadId, string motivo, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"/api/payroll/novelties/{novedadId}/cancel", new { Reason = motivo }, ct);

    public Task<InvitationApiResult<IReadOnlyList<NovedadDto>>> HistorialNovedadAsync(Guid novedadId, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<NovedadDto>>(HttpMethod.Get, $"/api/payroll/novelties/{novedadId}/history", null, ct);

    public Task<InvitationApiResult<CambioSalarioCreadoDto>> RegistrarCambioDeSalarioAsync(Guid empleadoId, CambioSalarioRequest request, CancellationToken ct = default) =>
        EnviarAsync<CambioSalarioCreadoDto>(HttpMethod.Post, $"/api/payroll/employees/{empleadoId}/salary-changes", request, ct);

    public Task<InvitationApiResult<IReadOnlyList<CambioSalarioDto>>> HistorialSalariosAsync(Guid empleadoId, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<CambioSalarioDto>>(HttpMethod.Get, $"/api/payroll/employees/{empleadoId}/salary-changes", null, ct);
}

public sealed record NovedadDto(
    Guid PublicId,
    Guid PeriodPublicId,
    Guid EmployeePublicId,
    string EmployeeName,
    string EmployeeDocument,
    string ConceptCode,
    string ConceptName,
    string Nature,
    decimal? Quantity,
    decimal? Amount,
    DateTime? StartDate,
    DateTime? EndDate,
    int DaysInPeriod,
    int CarryOverDays,
    decimal? EstimatedAmount,
    string Status,
    string? StatusReason,
    string Origin,
    int? InstallmentNumber,
    int? InstallmentTotal,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    Guid? SupersedesPublicId,
    string? Notes)
{
    public string NaturalezaTexto => Nature switch
    {
        "Earning" => "Devengo",
        "Deduction" => "Deducción",
        "EmployerContribution" => "Aporte empleador",
        "Provision" => "Provisión",
        "Informative" => "Informativo",
        _ => Nature,
    };

    public string EstadoTexto => Status switch
    {
        "Active" => "Activa",
        "Superseded" => "Corregida",
        "Cancelled" => "Anulada",
        _ => Status,
    };

    public string OrigenTexto => Origin switch
    {
        "Manual" => "Manual",
        "Import" => "Importada",
        "Recurring" => "Recurrente",
        "Retroactive" => "Retroactiva",
        "LoanDeduction" => "Cartera",
        "CarryOver" => "Traslado",
        _ => Origin,
    };

    public string CantidadOValor =>
        Quantity is { } q ? q.ToString("0.##") : Amount is { } a ? a.ToString("N0") : StartDate is not null ? $"{DaysInPeriod} d" : string.Empty;

    public string Fechas => StartDate is { } s && EndDate is { } e ? $"{s:dd/MM} – {e:dd/MM/yyyy}" : string.Empty;

    public bool EsActiva => Status == "Active";
}

public sealed record RegistrarNovedadRequest(
    Guid EmployeePublicId,
    string ConceptCode,
    decimal? Quantity,
    decimal? Amount,
    DateTime? StartDate,
    DateTime? EndDate,
    string? Notes,
    Guid? RetroactiveOfPeriodPublicId);

public sealed record CorregirNovedadRequest(
    decimal? Quantity,
    decimal? Amount,
    DateTime? StartDate,
    DateTime? EndDate,
    string? Notes,
    string Reason);

public sealed record CambioSalarioRequest(decimal NewSalary, DateTime EffectiveFrom, string Reason);
public sealed record CambioSalarioCreadoDto(long Id);
public sealed record CambioSalarioDto(long Id, DateTime EffectiveDate, decimal NewSalary, string? RegisteredBy, DateTime? RegisteredAt);
