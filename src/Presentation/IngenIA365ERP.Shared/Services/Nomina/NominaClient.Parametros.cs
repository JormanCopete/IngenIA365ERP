using System.Text.Json;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Nomina;

/// <summary>US3: definiciones de concepto, cuentas, catálogo heredado, prueba en seco, parámetros legales y retención del empleado.</summary>
public sealed partial class NominaClient
{
    // ---------------------------------------------------------------- conceptos --

    public Task<InvitationApiResult<CreadoDto>> CrearConceptoAsync(DefinicionConceptoRequest definicion, CancellationToken ct = default) =>
        EnviarAsync<CreadoDto>(HttpMethod.Post, "/api/payroll/concept-definitions", definicion, ct);

    public Task<InvitationApiResult<CreadoDto>> RevisarConceptoAsync(string codigo, DefinicionConceptoRequest definicion, CancellationToken ct = default) =>
        EnviarAsync<CreadoDto>(HttpMethod.Put, $"/api/payroll/concept-definitions/{Uri.EscapeDataString(codigo)}", definicion, ct);

    public Task<InvitationApiResult<EmptyResponse>> DesactivarConceptoAsync(string codigo, DateTime hasta, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Post, $"/api/payroll/concept-definitions/{Uri.EscapeDataString(codigo)}/deactivate", new { ValidTo = hasta }, ct);

    public Task<InvitationApiResult<IReadOnlyList<CuentaDeConceptoDto>>> ObtenerCuentasDeConceptoAsync(string codigo, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<CuentaDeConceptoDto>>(HttpMethod.Get, $"/api/payroll/concept-definitions/{Uri.EscapeDataString(codigo)}/accounts", null, ct);

    public Task<InvitationApiResult<EmptyResponse>> GuardarCuentasDeConceptoAsync(string codigo, IReadOnlyList<CuentaDeConceptoRequest> filas, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Put, $"/api/payroll/concept-definitions/{Uri.EscapeDataString(codigo)}/accounts", new { Rows = filas }, ct);

    public Task<InvitationApiResult<IReadOnlyList<ConceptoHeredadoDto>>> ListarConceptosHeredadosAsync(string? buscar = null, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<ConceptoHeredadoDto>>(HttpMethod.Get,
            "/api/payroll/concept-definitions/legacy" + (string.IsNullOrWhiteSpace(buscar) ? string.Empty : $"?search={Uri.EscapeDataString(buscar)}"), null, ct);

    public Task<InvitationApiResult<ResultadoPruebaEnSecoDto>> ProbarEnSecoAsync(PruebaEnSecoRequest request, CancellationToken ct = default) =>
        EnviarAsync<ResultadoPruebaEnSecoDto>(HttpMethod.Post, "/api/payroll/concept-definitions/dry-run", request, ct);

    public Task<InvitationApiResult<IReadOnlyList<SemillaReaplicadaDto>>> ReaplicarSemillaAsync(CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<SemillaReaplicadaDto>>(HttpMethod.Post, "/api/payroll/concept-definitions/seed", new { }, ct);

    // ------------------------------------------------------- parámetros legales --

    public Task<InvitationApiResult<ResumenParametrosLegalesDto>> ListarParametrosLegalesAsync(DateTime? vigenteA = null, CancellationToken ct = default) =>
        EnviarAsync<ResumenParametrosLegalesDto>(HttpMethod.Get, "/api/payroll/legal-parameters" + (vigenteA is { } d ? $"?asOf={d:yyyy-MM-dd}" : string.Empty), null, ct);

    public Task<InvitationApiResult<IReadOnlyList<ParametroLegalDto>>> VersionesDeParametroAsync(string codigo, CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyList<ParametroLegalDto>>(HttpMethod.Get, $"/api/payroll/legal-parameters/{Uri.EscapeDataString(codigo)}/versions", null, ct);

    public Task<InvitationApiResult<CreadoDto>> NuevaVigenciaAsync(string codigo, NuevaVigenciaRequest request, CancellationToken ct = default) =>
        EnviarAsync<CreadoDto>(HttpMethod.Post, $"/api/payroll/legal-parameters/{Uri.EscapeDataString(codigo)}/versions", request, ct);

    // -------------------------------------------------------- retención empleado --

    public Task<InvitationApiResult<RetencionEmpleadoDto>> RetencionDeEmpleadoAsync(Guid empleadoId, CancellationToken ct = default) =>
        EnviarAsync<RetencionEmpleadoDto>(HttpMethod.Get, $"/api/payroll/employees/{empleadoId}/withholding", null, ct);

    public Task<InvitationApiResult<EmptyResponse>> GuardarRetencionDeEmpleadoAsync(Guid empleadoId, GuardarRetencionRequest request, CancellationToken ct = default) =>
        EnviarAsync<EmptyResponse>(HttpMethod.Put, $"/api/payroll/employees/{empleadoId}/withholding",
            new { EmployeePublicId = empleadoId, request.Procedure, request.Rates, request.Deductions, request.EmployeeClass }, ct);
}

public sealed record DefinicionConceptoRequest(
    string Code, string Name, string Nature, string CalculationKind,
    decimal? FixedAmount, string? AmountParameterCode, bool ProrateByDays,
    string? BaseKind, decimal? Percent, string? PercentParameterCode,
    string? UnitKind, decimal? UnitFactor, string? TableParameterCode, string? ComponentConceptCodes,
    bool AffectsSalaryBase, bool AffectsContributionBase, bool AffectsBenefitsBase, bool AffectsWithholdingBase, bool IsBenefitRelated,
    bool AllowsRepeatInPeriod, decimal? MaxQuantity, decimal? MaxAmount, int ApplicableClasses,
    bool RequiresDates, bool RequiresQuantity, bool RequiresAmount, bool IsAutomatic, bool ReducesWorkedDays, DateTime ValidFrom);

public sealed record CuentaDeConceptoRequest(Guid? CostCenterPublicId, string DebitAccountCode, string CreditAccountCode);

/// <summary>Feature 009: una cuenta parametrizada con sus reglas y, si ya no sirve, el porqué.</summary>
public sealed record CuentaParametrizadaDto(Guid PublicId, string Code, string Name, bool IsMovement, bool IsActive, bool RequiresThirdParty, bool RequiresCrossDocument, bool RequiresCostCenter, bool RequiresBranch, string? Problem);

public sealed record CuentaDeConceptoDto(Guid? CostCenterPublicId, string? CostCenterName, CuentaParametrizadaDto Debit, CuentaParametrizadaDto Credit);

public sealed record ConceptoHeredadoDto(
    int LegacyConceptId, int ConceptCode, string Name, string ShortName, int ConceptClass, int Nature, decimal Value, decimal Factor, int Base,
    bool IsAutomatic, bool AffectsSalary, bool AffectsBenefits, bool AffectsWithholding, bool IsBenefit, bool IntegralSalary,
    string? TranslatedToCode, string TranslationHint);

public sealed record PruebaEnSecoRequest(DefinicionConceptoRequest Definition, Guid EmployeePublicId, Guid PeriodPublicId, decimal? Quantity, decimal? Amount, DateTime? StartDate, DateTime? EndDate);

public sealed record LineaPruebaEnSecoDto(string ConceptCode, string ConceptName, string Nature, decimal? Quantity, decimal? BaseAmount, decimal? Factor, decimal Amount, JsonElement Explanation);

public sealed record ResultadoPruebaEnSecoDto(IReadOnlyList<LineaPruebaEnSecoDto> Lines, IReadOnlyList<string> Refusals, IReadOnlyList<string> Skips, TotalesCorridaDto Totals);

public sealed record SemillaReaplicadaDto(string Seeder, int Inserted);

public sealed record TramoDto(decimal FromValue, decimal? ToValue, decimal? Rate, decimal? FixedValue, int Order = 0);

public sealed record ParametroLegalDto(
    Guid PublicId, string Code, string Name, string Kind, decimal? Value, DateTime ValidFrom, DateTime? ValidTo, string? Source,
    string? RangeUnitParameterCode, bool RangeIsMarginal, IReadOnlyList<TramoDto> Ranges, bool IsRequired, int VersionCount, string? CreatedBy, DateTime CreatedAt)
{
    public string TipoTexto => Kind switch { "Amount" => "Valor", "Percent" => "Porcentaje", "RangeTable" => "Tabla por rangos", "DateInYear" => "Fecha del año", _ => Kind };
    public bool EsTabla => Kind == "RangeTable";
    public string ValorTexto => EsTabla ? $"{Ranges.Count} tramo(s)"
        : Kind == "Percent" ? $"{Value:0.###} %"
        : Kind == "DateInYear" ? FechaDelAño(Value)
        : Value?.ToString("N2") ?? string.Empty;

    /// <summary>Feature 010 (D-07): el valor de una fecha del año es MMDD (1220 → «20/12»).</summary>
    private static string FechaDelAño(decimal? mmdd)
    {
        if (mmdd is null) return string.Empty;
        var n = (int)mmdd.Value;
        return $"{n % 100:00}/{n / 100:00}";
    }

    /// <summary>
    /// Feature 010 (D-08): qué significan las dos columnas de cada tramo. En la tabla de indemnización
    /// del art. 64 CST son días (del primer año y por cada año adicional), en la de cesantías gravadas el
    /// porcentaje NO gravado, y en el plazo PILA el día hábil; en las demás, tarifa y fijo.
    /// </summary>
    public string EtiquetaTarifa => Code switch
    {
        "INDEMNIZACION_TABLA" => "Días por año adicional",
        "CESANTIAS_GRAVADA_TABLA_UVT" => "% no gravado",
        "PILA_PLAZO_PAGO_POR_NIT" => "—",
        _ => "Tarifa %",
    };

    public string EtiquetaFijo => Code switch
    {
        "INDEMNIZACION_TABLA" => "Días del 1.er año",
        "CESANTIAS_GRAVADA_TABLA_UVT" => "—",
        "PILA_PLAZO_PAGO_POR_NIT" => "Día hábil del mes",
        _ => $"Fijo ({RangeUnitParameterCode ?? "pesos"})",
    };
    public string VigenciaTexto => ValidTo is { } h ? $"{ValidFrom:dd/MM/yyyy} – {h:dd/MM/yyyy}" : $"desde {ValidFrom:dd/MM/yyyy}";
}

public sealed record ResumenParametrosLegalesDto(DateTime AsOf, IReadOnlyList<ParametroLegalDto> Items, IReadOnlyList<string> MissingThisYear, IReadOnlyList<string> MissingNextYear);

public sealed record NuevaVigenciaRequest(
    DateTime ValidFrom, decimal? Value, IReadOnlyList<TramoDto>? Ranges, string Source,
    string? Name = null, string? Kind = null, string? RangeUnitParameterCode = null, bool? RangeIsMarginal = null);

public sealed record TasaRetencionDto(decimal RatePercent, DateTime ValidFrom, DateTime? ValidTo);
public sealed record DeduccionTributariaDto(string Kind, decimal? MonthlyAmount, decimal? Percent, DateTime ValidFrom, DateTime? ValidTo)
{
    public string TipoTexto => Kind switch
    {
        "HousingInterest" => "Intereses de vivienda",
        "PrepaidHealth" => "Medicina prepagada",
        "Dependents" => "Dependientes",
        "VoluntaryPension" => "Pensión voluntaria",
        "AfcSavings" => "Ahorro AFC",
        _ => Kind,
    };
}

public sealed record RetencionEmpleadoDto(
    Guid EmployeePublicId, byte Procedure, IReadOnlyList<TasaRetencionDto> Rates, IReadOnlyList<DeduccionTributariaDto> Deductions,
    Guid PlanPublicId, string PlanName, DateTime? PlanEffectiveFrom, string EmployeeClass);

public sealed record GuardarRetencionRequest(byte Procedure, IReadOnlyList<TasaRetencionDto> Rates, IReadOnlyList<DeduccionTributariaDto> Deductions, string? EmployeeClass);
