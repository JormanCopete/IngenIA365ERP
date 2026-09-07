using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Nomina;

/// <summary>Conceptos con vigencia y búsqueda de empleados (lo que las pantallas de novedades y liquidación necesitan de otros módulos).</summary>
public sealed partial class NominaClient
{
    public Task<InvitationApiResult<IReadOnlyList<ConceptoDto>>> ListarConceptosAsync(DateTime? vigenteA = null, bool incluirInactivos = false, CancellationToken ct = default)
    {
        var q = new List<string>();
        if (vigenteA is { } d) q.Add($"asOf={d:yyyy-MM-dd}");
        if (incluirInactivos) q.Add("includeInactive=true");
        var url = "/api/payroll/concept-definitions" + (q.Count > 0 ? "?" + string.Join("&", q) : string.Empty);
        return EnviarAsync<IReadOnlyList<ConceptoDto>>(HttpMethod.Get, url, null, ct);
    }

    public Task<InvitationApiResult<IReadOnlyList<ConceptoDto>>> VersionesDeConceptoAsync(string codigo, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<ConceptoDto>>(HttpMethod.Get, $"/api/payroll/concept-definitions/{Uri.EscapeDataString(codigo)}/versions", null, ct);

    /// <summary>Empleados activos por nombre o documento (módulo de empleados, existente).</summary>
    public Task<InvitationApiResult<PaginaDto<EmpleadoResumenDto>>> BuscarEmpleadosAsync(string? buscar, int tamano = 20, CancellationToken ct = default)
    {
        var url = $"/api/payroll/employees?PageNumber=1&PageSize={tamano}&ActiveOnly=true";
        if (!string.IsNullOrWhiteSpace(buscar)) url += $"&Search={Uri.EscapeDataString(buscar.Trim())}";
        return EnviarAsync<PaginaDto<EmpleadoResumenDto>>(HttpMethod.Get, url, null, ct);
    }
}

public sealed record ConceptoDto(
    Guid PublicId,
    string Code,
    string Name,
    string Nature,
    string CalculationKind,
    decimal? FixedAmount,
    string? AmountParameterCode,
    bool ProrateByDays,
    string? BaseKind,
    decimal? Percent,
    string? PercentParameterCode,
    string? UnitKind,
    decimal? UnitFactor,
    string? TableParameterCode,
    string? ComponentConceptCodes,
    bool AffectsSalaryBase,
    bool AffectsContributionBase,
    bool AffectsBenefitsBase,
    bool AffectsWithholdingBase,
    bool IsBenefitRelated,
    bool AllowsRepeatInPeriod,
    decimal? MaxQuantity,
    decimal? MaxAmount,
    int ApplicableClasses,
    IReadOnlyList<string> ApplicableClassNames,
    bool RequiresDates,
    bool RequiresQuantity,
    bool RequiresAmount,
    bool IsAutomatic,
    bool ReducesWorkedDays,
    string Origin,
    int? LegacyConceptId,
    DateTime ValidFrom,
    DateTime? ValidTo,
    bool IsActive,
    bool HasAccounts,
    bool RegistrableComoNovedad)
{
    public string NaturalezaTexto => Nature switch
    {
        "Earning" => "Devengos",
        "Deduction" => "Deducciones",
        "EmployerContribution" => "Aportes del empleador",
        "Provision" => "Provisiones",
        "Informative" => "Informativos",
        _ => Nature,
    };

    public string FormaTexto => CalculationKind switch
    {
        "FixedAmount" => "Valor fijo",
        "PercentOfBase" => "Porcentaje sobre base",
        "QuantityTimesUnit" => "Cantidad × unidad",
        "RangeTable" => "Tabla por rangos",
        "CompositeOfConcepts" => "Suma de conceptos",
        _ => CalculationKind,
    };

    public string OrigenTexto => Origin switch
    {
        "Seed" => "Semilla",
        "Custom" => "Propio",
        "TranslatedLegacy" => "Traducido",
        _ => Origin,
    };

    public string UnidadTexto => UnitKind switch
    {
        "OrdinaryHour" => "horas",
        "HourWithSurcharge" => "horas",
        "Day" => "días",
        _ => "cantidad",
    };

    public bool AplicaA(string clase) => ApplicableClasses == 0 || ApplicableClassNames.Contains(clase);
}

public sealed record EmpleadoResumenDto(
    Guid PublicId,
    string FullName,
    string IdentificationNumber,
    string PositionName,
    decimal Salary,
    DateTime HireDate,
    string StatusText,
    string Email);
