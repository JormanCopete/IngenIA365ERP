using System.Text.Json;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Nomina;

/// <summary>US2: calcular, revisar y aprobar (contracts/api.md §4).</summary>
public sealed partial class NominaClient
{
    public Task<InvitationApiResult<ResultadoCalculoDto>> CalcularAsync(Guid periodoId, CancellationToken ct = default) =>
        EnviarAsync<ResultadoCalculoDto>(HttpMethod.Post, $"/api/payroll/pay-periods/{periodoId}/runs", new { }, ct);

    public Task<InvitationApiResult<ResumenCorridaDto>> CorridaActualAsync(Guid periodoId, CancellationToken ct = default) =>
        EnviarAsync<ResumenCorridaDto>(HttpMethod.Get, $"/api/payroll/pay-periods/{periodoId}/runs/current", null, ct);

    public Task<InvitationApiResult<IReadOnlyList<ResumenCorridaDto>>> HistorialCorridasAsync(Guid periodoId, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<ResumenCorridaDto>>(HttpMethod.Get, $"/api/payroll/pay-periods/{periodoId}/runs", null, ct);

    public Task<InvitationApiResult<ResumenCorridaDto>> ResumenAsync(Guid corridaId, CancellationToken ct = default) =>
        EnviarAsync<ResumenCorridaDto>(HttpMethod.Get, $"/api/payroll/runs/{corridaId}", null, ct);

    public Task<InvitationApiResult<IReadOnlyList<EmpleadoDeCorridaDto>>> EmpleadosDeCorridaAsync(Guid corridaId, string? bandera = null, bool? cambiados = null, string? buscar = null, CancellationToken ct = default)
    {
        var q = new List<string>();
        if (!string.IsNullOrWhiteSpace(bandera)) q.Add($"flag={Uri.EscapeDataString(bandera)}");
        if (cambiados is { } c) q.Add($"changed={(c ? "true" : "false")}");
        if (!string.IsNullOrWhiteSpace(buscar)) q.Add($"search={Uri.EscapeDataString(buscar)}");
        var url = $"/api/payroll/runs/{corridaId}/employees" + (q.Count > 0 ? "?" + string.Join("&", q) : string.Empty);
        return EnviarAsync<IReadOnlyList<EmpleadoDeCorridaDto>>(HttpMethod.Get, url, null, ct);
    }

    public Task<InvitationApiResult<DetalleEmpleadoCorridaDto>> DetalleEmpleadoAsync(Guid corridaId, Guid empleadoId, CancellationToken ct = default) =>
        EnviarAsync<DetalleEmpleadoCorridaDto>(HttpMethod.Get, $"/api/payroll/runs/{corridaId}/employees/{empleadoId}", null, ct);

    public Task<InvitationApiResult<ResultadoAprobacionDto>> AprobarAsync(Guid corridaId, AprobarRequest request, CancellationToken ct = default) =>
        EnviarAsync<ResultadoAprobacionDto>(HttpMethod.Post, $"/api/payroll/runs/{corridaId}/approve", request, ct);
}

public sealed record TotalesCorridaDto(decimal Earnings, decimal Deductions, decimal EmployerContributions, decimal Provisions, decimal Net, decimal RoundingAdjustment);

public sealed record TotalPorConceptoDto(string Code, string Name, string Nature, int Employees, decimal Amount)
{
    public string NaturalezaTexto => Nature switch
    {
        "Earning" => "Devengos", "Deduction" => "Deducciones", "EmployerContribution" => "Aportes del empleador",
        "Provision" => "Provisiones", "Informative" => "Informativos", _ => Nature,
    };
}

public sealed record BloqueoDto(Guid EmployeePublicId, string EmployeeName, string Flag, string Detail)
{
    public string BanderaTexto => Bandera(Flag);

    public static string Bandera(string flag) => flag switch
    {
        "NegativeNet" => "Neto negativo",
        "DeductionsOverMax" => "Deducciones sobre el máximo",
        "MissingAffiliation" => "Afiliación faltante",
        "ConceptWithoutAccounts" => "Concepto sin cuentas",
        "WithholdingRateMissing" => "Sin porcentaje de retención (proc. 2)",
        _ => flag,
    };
}

public sealed record ExcepcionAprobacionDto(Guid EmployeePublicId, string Flag, string Reason, string? AuthorizedBy = null, DateTime? AuthorizedAt = null);

public sealed record ResumenCorridaDto(
    Guid PublicId,
    Guid? PeriodPublicId,
    int Version,
    string Status,
    DateTime CalculatedAt,
    string CalculatedBy,
    DateTime? ApprovedAt,
    string? ApprovedBy,
    DateTime? ReversedAt,
    string? ReversedBy,
    string? ReversalReason,
    int EmployeeCount,
    TotalesCorridaDto Totals,
    IReadOnlyList<TotalPorConceptoDto> ByConcept,
    IReadOnlyList<BloqueoDto> Blockers,
    int ChangedEmployees,
    string InputsHash,
    Guid? AccountingDocumentPublicId,
    string? AccountingDocumentNumber,
    Guid? ReversalAccountingDocumentPublicId,
    bool ApprovedWithoutSegregation,
    IReadOnlyList<ExcepcionAprobacionDto> Exceptions,
    DateTime? DiscardedAt = null,
    string? DiscardedBy = null,
    string? DiscardReason = null,
    string Kind = "Ordinary",
    DateOnly? CutoffDate = null,
    DateOnly? PayDate = null,
    int? Year = null,
    int? Semester = null,
    Guid? EmployeePublicId = null,
    IReadOnlyList<AvisoCorridaDto>? Warnings = null)
{
    /// <summary>Feature 010: prima, cesantías, vacaciones o definitiva; la ordinaria es «Ordinary».</summary>
    public bool EsEspecial => Kind is not "Ordinary";

    public string TipoTexto => Kind switch
    {
        "Ordinary" => "Nómina ordinaria",
        "ServiceBonus" => Year is { } a && Semester is { } s ? $"Prima de servicios {a}-{(s == 1 ? "I" : "II")}" : "Prima de servicios",
        "Severance" => Year is { } a2 ? $"Cesantías e intereses {a2}" : "Cesantías e intereses",
        "Vacation" => "Vacaciones",
        "Settlement" => "Liquidación definitiva",
        _ => Kind,
    };

    public string EstadoTexto => Status switch
    {
        "Draft" => "Borrador",
        "Stale" => "Desactualizado",
        "Superseded" => DiscardedAt is null ? "Reemplazado" : "Descartado",
        "Approved" => "Aprobado",
        "Reversed" => "Reversado",
        _ => Status,
    };

    public bool EsBorrador => Status is "Draft" or "Stale";

    /// <summary>
    /// Sólo <c>Draft</c> se aprueba: el servidor rechaza <c>Stale</c> con <c>Payroll.Settlement.NotDraft</c>
    /// (toda vigencia nueva de política, parámetro legal o concepto deja los borradores desactualizados,
    /// D-19). Hasta el 2026-09-21 las pantallas ofrecían «Aprobar» con <see cref="EsBorrador"/>, que
    /// incluye <c>Stale</c>, y la persona recibía el rechazo con el nombre del estado en inglés.
    /// </summary>
    public bool SePuedeAprobar => Status == "Draft";

    /// <summary>Borrador que hay que recalcular antes de aprobar.</summary>
    public bool EstaDesactualizada => Status == "Stale";
}

/// <summary>Aviso de una corrida especial con su código (feature 010, contracts/api.md §3.5).</summary>
public sealed record AvisoCorridaDto(string Code, string Message, System.Text.Json.JsonElement? Data);

public sealed record ResultadoCalculoDto(Guid RunPublicId, int Version, int EmployeeCount, int ChangedEmployees, TotalesCorridaDto Totals, IReadOnlyList<BloqueoDto> Blockers, IReadOnlyList<string> Warnings);

public sealed record EmpleadoDeCorridaDto(
    Guid EmployeePublicId, string EmployeeName, string Document, string EmployeeClass, int DaysWorked,
    decimal TotalEarnings, decimal TotalDeductions, decimal TotalEmployerContributions, decimal TotalProvisions, decimal NetPay,
    IReadOnlyList<string> Flags, bool Changed, bool HasNotes)
{
    public bool TieneBloqueos => Flags.Count > 0;
    public string BanderasTexto => string.Join(", ", Flags.Select(BloqueoDto.Bandera));
}

public sealed record TramoSalarioDto(DateTime From, DateTime To, int Days, int AbsenceDays, decimal MonthlySalary, int PaidDays);

public sealed record PasoDto(string Label, decimal? Value, string? Text);

public sealed record LineaCorridaDto(
    Guid PublicId, string ConceptCode, string ConceptName, string Nature, decimal? Quantity, decimal? BaseAmount, decimal? Factor,
    decimal? RangeFrom, decimal? RangeTo, decimal Amount, bool AffectsAccounting, Guid? NoveltyPublicId, int Order, JsonElement Explanation)
{
    public string NaturalezaTexto => Nature switch
    {
        "Earning" => "Devengo", "Deduction" => "Deducción", "EmployerContribution" => "Aporte empleador",
        "Provision" => "Provisión", "Informative" => "Informativo", _ => Nature,
    };
}

public sealed record DetalleEmpleadoCorridaDto(
    Guid RunPublicId, Guid EmployeePublicId, string EmployeeName, string Document, string EmployeeClass, int DaysWorked,
    IReadOnlyList<TramoSalarioDto> SalaryTranches, IReadOnlyList<LineaCorridaDto> Lines, TotalesCorridaDto Totals,
    IReadOnlyList<string> Flags, bool Changed, IReadOnlyList<PasoDto> Bases, IReadOnlyList<string> Refusals, IReadOnlyList<string> Skips);

public sealed record AprobarRequest(bool Confirm, IReadOnlyList<ExcepcionAprobacionDto> Exceptions, bool ConfirmEmpty = false, bool ConfirmWithoutSegregation = false);

public sealed record ResultadoAprobacionDto(Guid RunPublicId, Guid PeriodPublicId, Guid AccountingDocumentPublicId, string AccountingDocumentNumber, TotalesCorridaDto Totals, int EmployeeCount, bool ApprovedWithoutSegregation);
