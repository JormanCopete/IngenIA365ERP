namespace IngenIA365ERP.Shared.Services.Nomina;

/// <summary>Página de resultados tal como la serializa <c>PagedList&lt;T&gt;</c> en la API.</summary>
public sealed record PaginaDto<T>(
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    IReadOnlyList<T> Items);

// ------------------------------------------------------------------- planes --

public sealed record PlanNominaDto(
    Guid PublicId,
    string Code,
    string Name,
    string Periodicity,
    bool IsDefault,
    bool IsActive,
    int EmployeeCount)
{
    public string PeriodicidadTexto => Periodicity switch
    {
        "Monthly" => "Mensual",
        "Biweekly" => "Quincenal",
        _ => Periodicity,
    };
}

public sealed record CrearPlanNominaRequest(string Code, string Name, string Periodicity);
public sealed record ActualizarPlanNominaRequest(string Name, bool IsActive, string? Periodicity = null);
public sealed record CambiarPlanEmpleadoRequest(Guid PlanPublicId, DateTime EffectiveFrom);

// ----------------------------------------------------------------- períodos --

public sealed record PeriodoPagoDto(
    Guid PublicId,
    Guid PlanPublicId,
    string PlanCode,
    string PlanName,
    string PlanPeriodicity,
    int PlanId,
    int PayrollCompanyId,
    string? Description,
    string? PayDate,
    DateTime StartDate,
    DateTime EndDate,
    int? Periodicity,
    string Status,
    string StatusMessage,
    DateTime? ApprovedAt,
    string? ApprovedBy,
    Guid? CurrentRunPublicId,
    int PeriodId)
{
    public string EstadoTexto => Status switch
    {
        "Open" => "Abierto",
        "Calculated" => "Calculado",
        "Approved" => "Aprobado",
        "Reversed" => "Reversado",
        _ => Status,
    };

    public string Etiqueta =>
        string.IsNullOrWhiteSpace(Description)
            ? $"{StartDate:dd/MM/yyyy} – {EndDate:dd/MM/yyyy}"
            : $"{Description} ({StartDate:dd/MM/yyyy} – {EndDate:dd/MM/yyyy})";

    public bool EstaAbierto => Status is "Open" or "Calculated";
}

public sealed record CrearPeriodoPagoRequest(
    Guid? PlanPublicId,
    int PlanId,
    int PayrollCompanyId,
    string? Description,
    string? PayDate,
    DateTime StartDate,
    DateTime EndDate,
    int? Periodicity,
    string StatusMessage,
    int PeriodId);

public sealed record ActualizarPeriodoPagoRequest(
    Guid PublicId,
    string? Description,
    string? PayDate,
    DateTime StartDate,
    DateTime EndDate,
    string StatusMessage);
