using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Nomina;

// ============================================================================
// Feature 010 (N2, US7): porcentaje fijo de retención del procedimiento 2 (contracts/api.md §6).
// ============================================================================

/// <summary>Estado: 0 calculado, 1 aprobado, 2 reemplazado, 3 rechazado.</summary>
public sealed record CalculoP2Dto(
    Guid CalculationPublicId, Guid EmployeePublicId, string EmployeeName, string Document, short Year, byte Semester, int Version,
    int MonthsUsed, decimal Divisor, decimal AverageBase, decimal AverageBaseUvt, decimal TheoreticalWithholding, decimal Percentage,
    int Status, DateOnly ValidFrom, DateOnly ValidTo, DateTime CalculatedAt, string CalculatedBy, DateTime? ApprovedAt, string? ApprovedBy,
    string Sequence, bool PlanTableUsed, decimal? CurrentRatePercent)
{
    public string EstadoTexto => Status switch { 0 => "Calculado", 1 => "Aprobado", 2 => "Reemplazado", 3 => "Rechazado", _ => Status.ToString() };
    public bool Calculado => Status == 0;
    public string SemestreTexto => $"{Year}-{Semester} (rige {ValidFrom:dd/MM/yyyy} – {ValidTo:dd/MM/yyyy})";
    public string SecuenciaTexto => Sequence == "DivideThenDepurate" ? "Dividir y luego depurar" : "Depurar y luego dividir";
}

public sealed record CalculoP2OmitidoDto(Guid EmployeePublicId, string EmployeeName, string Reason, string Message);

public sealed record LoteP2Dto(Guid BatchPublicId, IReadOnlyList<CalculoP2Dto> Items, IReadOnlyList<CalculoP2OmitidoDto> Skipped, IReadOnlyList<AvisoCorridaDto> Warnings);

public sealed record CorridaFuenteP2Dto(Guid RunPublicId, int Version, string Kind, decimal Amount);

public sealed record MesP2Dto(short Year, byte Month, decimal GrossTaxable, decimal MandatoryContributions, bool IncludedSpecialRuns, IReadOnlyList<CorridaFuenteP2Dto> SourceRuns);

public sealed record TramoP2Dto(decimal From, decimal? To, decimal? Rate, decimal? Fixed);

public sealed record TablaP2Dto(string Code, DateTime ValidFrom, string Source, IReadOnlyList<TramoP2Dto> Ranges);

public sealed record PasoP2Dto(string Label, decimal? Value, string? Text);

public sealed record DetalleP2Dto(
    CalculoP2Dto Summary, IReadOnlyList<MesP2Dto> Months, decimal Divisor, string DivisorSource, int Sequence,
    decimal TotalGrossIncome, decimal TotalMandatoryContributions, decimal TotalDeclaredDeductions, decimal TotalExemptIncome, decimal DepuratedBase,
    decimal AverageBase, decimal Uvt, decimal AverageBaseUvt, TablaP2Dto Table, string RangeText, decimal TheoreticalWithholding, decimal Percentage,
    string Rounding, IReadOnlyList<PasoP2Dto> Steps, IReadOnlyList<PasoP2Dto> DepurationSteps);

public sealed record P2AprobadoDto(Guid CalculationPublicId, Guid EmployeePublicId, DateOnly ValidFrom, DateOnly ValidTo, DateOnly? PreviousClosedAt, decimal Percentage);

public sealed record P2LoteAprobadoItemDto(Guid CalculationPublicId, bool Approved, string? ErrorCode, string? Message, P2AprobadoDto? Result);

/// <summary>Feature 010 (US7): porcentaje fijo del procedimiento 2.</summary>
public sealed partial class NominaClient
{
    private const string RutaRetencion2 = "/api/payroll/withholding-rates";

    public Task<InvitationApiResult<IReadOnlyList<CalculoP2Dto>>> ListarCalculosP2Async(int? anio = null, int? semestre = null, string? estado = null, Guid? empleadoId = null, CancellationToken ct = default)
    {
        var q = new List<string>();
        if (anio is { } a) q.Add($"year={a}");
        if (semestre is { } s) q.Add($"semester={s}");
        if (estado is not null) q.Add($"status={estado}");
        if (empleadoId is { } e) q.Add($"employeeId={e}");
        return EnviarAsync<IReadOnlyList<CalculoP2Dto>>(HttpMethod.Get, RutaRetencion2 + (q.Count > 0 ? "?" + string.Join("&", q) : string.Empty), null, ct);
    }

    public Task<InvitationApiResult<LoteP2Dto>> CalcularP2Async(int anio, int semestre, IReadOnlyList<Guid>? empleados = null, CancellationToken ct = default) =>
        EnviarAsync<LoteP2Dto>(HttpMethod.Post, $"{RutaRetencion2}/calculate", new { year = anio, semester = semestre, employeePublicIds = empleados }, ct);

    public Task<InvitationApiResult<DetalleP2Dto>> CalculoP2Async(Guid calculoId, CancellationToken ct = default) =>
        EnviarAsync<DetalleP2Dto>(HttpMethod.Get, $"{RutaRetencion2}/{calculoId}", null, ct);

    public Task<InvitationApiResult<P2AprobadoDto>> AprobarP2Async(Guid calculoId, CancellationToken ct = default) =>
        EnviarAsync<P2AprobadoDto>(HttpMethod.Post, $"{RutaRetencion2}/{calculoId}/approve", null, ct);

    public Task<InvitationApiResult<IReadOnlyList<P2LoteAprobadoItemDto>>> AprobarP2LoteAsync(IReadOnlyList<Guid> calculos, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<P2LoteAprobadoItemDto>>(HttpMethod.Post, $"{RutaRetencion2}/approve", new { calculationPublicIds = calculos }, ct);

    public Task<InvitationApiResult<EmptyResponse>> RechazarP2Async(Guid calculoId, string motivo, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"{RutaRetencion2}/{calculoId}/reject", new { reason = motivo }, ct);

    /// <summary>Vista <c>retencion-p2</c> del centro de reportes: un cálculo, o el semestre completo.</summary>
    public Task<InvitationApiResult<ArchivoDescargado>> DescargarReporteP2Async(string formato, Guid? calculoId = null, int? anio = null, int? semestre = null, CancellationToken ct = default) =>
        DescargarAsync($"/api/reports/payroll/retencion-p2?format={formato}" + (calculoId is { } c ? $"&calculationId={c}" : $"&year={anio}&semester={semestre}"), ct);
}
