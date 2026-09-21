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
        "TenDay" => "Decadal",
        "Weekly" => "Semanal",
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
    int PeriodId,
    byte SubPeriodNumber = 1,
    short ImputationYear = 0,
    byte ImputationMonth = 0,
    string SubPeriodLabel = "")
{
    /// <summary>«Quincena 2 · Marzo 2026»; en mensual sólo el mes.</summary>
    public string SubPeriodoTexto =>
        ImputationMonth is >= 1 and <= 12
            ? (PlanPeriodicity == "Monthly" ? "" : SubPeriodLabel + " · ") + new DateTime(ImputationYear == 0 ? StartDate.Year : ImputationYear, ImputationMonth, 1).ToString("MMMM yyyy")
            : SubPeriodLabel;

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
    int PeriodId,
    byte? SubPeriodNumber = null,
    short? ImputationYear = null,
    byte? ImputationMonth = null);

public sealed record ActualizarPeriodoPagoRequest(
    Guid PublicId,
    string? Description,
    string? PayDate,
    DateTime StartDate,
    DateTime EndDate,
    string StatusMessage,
    byte? SubPeriodNumber = null,
    short? ImputationYear = null,
    byte? ImputationMonth = null);

// =============================================================== feature 010 ==
// Espejo de contracts/api.md §4, §10 y §12: los enums viajan por nombre al enviar y llegan
// como número (EnumPorNombreONumero); aquí se leen como int y se traducen con texto propio.

// ------------------------------------------------------- políticas por empresa --

/// <summary>Una política con su valor vigente (§10.1). <c>Allowed</c> vacío = texto libre con <c>Format</c>.</summary>
public sealed record PoliticaEmpresaDto(
    string Key,
    string? Value,
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    IReadOnlyList<string> Allowed,
    string? Format,
    string Description,
    string Source,
    Guid? PublicId,
    string? Notes,
    int VersionCount)
{
    public bool EsBooleana => Allowed.Count == 2 && Allowed.Contains("true") && Allowed.Contains("false");
    public bool EsTextoLibre => Allowed.Count == 0;
    public bool TieneVigenciaRegistrada => PublicId is not null;
    public string ValorTexto => TraducirValor(Value);
    public string VigenciaTexto => ValidFrom is null ? "sin vigencia registrada (defecto)"
        : ValidTo is { } h ? $"{ValidFrom:dd/MM/yyyy} – {h:dd/MM/yyyy}" : $"desde {ValidFrom:dd/MM/yyyy}";

    public static string TraducirValor(string? v) => v switch
    {
        null or "" => "—",
        "true" => "Sí",
        "false" => "No",
        "LunesASabado" => "Lunes a sábado",
        "LunesAViernes" => "Lunes a viernes",
        "Acumulado" => "Acumulado en el año",
        "Mensualizado" => "Mensualizado",
        "DepurarLuegoDividir" => "Depurar y luego dividir",
        "DividirLuegoDepurar" => "Dividir y luego depurar",
        "Calendario" => "Días calendario",
        "Habiles" => "Días hábiles",
        "SaldoTotal" => "Saldo total de la deuda",
        "SoloCuotasCausadas" => "Sólo las cuotas causadas",
        "NoProponer" => "No proponer descuento",
        _ => v,
    };
}

public sealed record VigenciaPoliticaDto(
    Guid PublicId,
    string Key,
    string Value,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string? Notes,
    string? CreatedBy,
    DateTime CreatedAt,
    bool IsCurrent)
{
    public string ValorTexto => PoliticaEmpresaDto.TraducirValor(Value);
    public string VigenciaTexto => ValidTo is { } h ? $"{ValidFrom:dd/MM/yyyy} – {h:dd/MM/yyyy}" : $"desde {ValidFrom:dd/MM/yyyy}";
}

public sealed record NuevaVigenciaPoliticaRequest(string Value, DateOnly ValidFrom, DateOnly? ValidTo, string Reason, bool ClosePrevious = true);

public sealed record VigenciaPoliticaCreadaDto(Guid PublicId, IReadOnlyList<string> Warnings);

// ------------------------------------------------------------------ festivos --

/// <summary><c>Origin</c> es <c>HolidayOrigin</c>: 1 fijo Ley 51, 2 trasladado al lunes, 3 por Pascua, 4 decretado, 5 manual.</summary>
public sealed record FestivoDto(Guid HolidayPublicId, DateOnly Date, string Name, int Origin, bool IsSeeded, string? CreatedBy, DateTime CreatedAt)
{
    public string OrigenTexto => Origin switch
    {
        1 => "Ley 51 (fijo)",
        2 => "Ley 51 (trasladado al lunes)",
        3 => "Ley 51 (por Pascua)",
        4 => "Decretado",
        5 => "Manual",
        _ => Origin.ToString(),
    };
    public string DiaTexto => Date.ToString("dddd");
    public bool SePuedeRetirar => !IsSeeded;
}

/// <summary><c>Origin</c> por nombre: <c>Manual</c> o <c>Decreed</c>.</summary>
public sealed record NuevoFestivoRequest(DateOnly Date, string Name, string Origin = "Manual");

// ---------------------------------------------------------- saldos iniciales --

/// <summary><c>Kind</c> es <c>PayrollRunKind</c> (0 ordinaria, 1 prima, 2 cesantías, 3 vacaciones, 4 definitiva).</summary>
public sealed record ConsumidorDeSaldoDto(Guid RunPublicId, int Kind)
{
    public string TipoTexto => Kind switch { 0 => "Nómina", 1 => "Prima", 2 => "Cesantías", 3 => "Vacaciones", 4 => "Definitiva", _ => Kind.ToString() };
}

public sealed record SaldoInicialResumenDto(
    Guid EmployeePublicId,
    string Name,
    string Document,
    DateTime HireDate,
    bool HiredBeforeStart,
    DateOnly? AsOfDate,
    decimal? PendingVacationDays,
    decimal? AccruedSeverance,
    decimal? AccruedSeveranceInterest,
    decimal? AccruedServiceBonus,
    IReadOnlyList<ConsumidorDeSaldoDto> ConsumedBy,
    bool IsEditable,
    DateTime? UpdatedAt,
    string? UpdatedBy)
{
    public bool TieneSaldo => AsOfDate is not null;
    public bool Falta => HiredBeforeStart && !TieneSaldo;
    public string EstadoTexto => !TieneSaldo ? (HiredBeforeStart ? "Falta" : "No aplica") : IsEditable ? "Digitado" : "Consumido";
}

/// <summary><c>Kind</c> es <c>OpeningBalanceKind</c>: 1 apertura, 2 ajuste.</summary>
public sealed record SaldoInicialFilaDto(
    Guid PublicId,
    int Kind,
    DateOnly AsOfDate,
    decimal PendingVacationDays,
    decimal AccruedSeverance,
    decimal AccruedSeveranceInterest,
    decimal AccruedServiceBonus,
    int? ServiceBonusDaysAccrued,
    int? SeveranceDaysAccrued,
    string? Notes,
    Guid? AdjustsBalancePublicId,
    string? AdjustmentReason,
    ConsumidorDeSaldoDto? ConsumedBy,
    string? CreatedBy,
    DateTime CreatedAt,
    string? UpdatedBy,
    DateTime? UpdatedAt)
{
    public string TipoTexto => Kind == 2 ? "Ajuste" : "Apertura";
}

public sealed record SaldoInicialDetalleDto(
    Guid EmployeePublicId,
    string Name,
    string Document,
    DateTime HireDate,
    DateOnly? PayrollStartDate,
    bool HiredBeforeStart,
    SaldoInicialFilaDto? Current,
    IReadOnlyList<SaldoInicialFilaDto> History);

public sealed record GuardarSaldoInicialRequest(
    DateOnly AsOfDate,
    decimal PendingVacationDays,
    decimal AccruedSeverance,
    decimal AccruedSeveranceInterest,
    decimal AccruedServiceBonus,
    int? ServiceBonusDaysAccrued = null,
    int? SeveranceDaysAccrued = null,
    string? Notes = null);

public sealed record AjustarSaldoInicialRequest(
    DateOnly AsOfDate,
    decimal PendingVacationDays,
    decimal AccruedSeverance,
    decimal AccruedSeveranceInterest,
    decimal AccruedServiceBonus,
    string Reason,
    int? ServiceBonusDaysAccrued = null,
    int? SeveranceDaysAccrued = null,
    string? Notes = null);

// ------------------------------------------------------------ ficha ampliada --

/// <summary>Bloque PILA de la ficha (§12). <c>PensionTransitionRegime</c>: 0 no consta, 1 sí, 2 no.</summary>
public sealed record FichaPilaDto
{
    public string? ContributorType { get; init; }
    public string? ContributorSubtype { get; init; }
    public string? DivipolaDepartment { get; init; }
    public string? DivipolaMunicipality { get; init; }
    public string? EconomicActivityCode { get; init; }
    public string? WorkCenter { get; init; }
    public string SalaryTypeCode { get; init; } = "F";
    public bool ForeignNotPensionObligated { get; init; }
    public bool ColombianAbroad { get; init; }
    public int PensionTransitionRegime { get; init; }
    public bool HighRiskPension { get; init; }
}

/// <summary>Bloque DIAN de la ficha (§12). <c>ContractTypeDian</c> 1..5.</summary>
public sealed record FichaDianDto
{
    public string? WorkerType { get; init; }
    public string? WorkerSubtype { get; init; }
    public int? ContractTypeDian { get; init; }
    public bool HighRiskPension { get; init; }
    public string? PaymentMethodCode { get; init; }
    public string? WorkAddress { get; init; }
}

public sealed record SaldoVacacionesFichaDto(decimal PendingDays, DateOnly AsOf);

public sealed record SaldoInicialFichaDto(
    Guid PublicId, int Kind, DateOnly AsOfDate, decimal PendingVacationDays, decimal AccruedSeverance,
    decimal AccruedSeveranceInterest, decimal AccruedServiceBonus, string? EnteredBy, DateTime EnteredAt, Guid? ConsumedByRunPublicId);

/// <summary><c>Origin</c>: 0 manual, 1 calculado por el procedimiento 2.</summary>
public sealed record PorcentajeRetencionFichaDto(decimal RatePercent, DateTime ValidFrom, DateTime? ValidTo, int Origin, byte Procedure)
{
    public string OrigenTexto => Origin == 1 ? "calculado (procedimiento 2)" : "digitado a mano";
}

/// <summary><c>Status</c> es <c>TerminationStatus</c>: 0 registrada, 1 liquidada, 2 reintegrado, 3 anulada.</summary>
public sealed record TerminacionFichaDto(Guid PublicId, DateOnly TerminationDate, string ReasonCode, string ReasonName, bool GeneratesSeverancePay, int Status)
{
    public string EstadoTexto => Status switch { 0 => "Registrada", 1 => "Liquidada", 2 => "Reintegrado", 3 => "Anulada", _ => Status.ToString() };
}

/// <summary>
/// La ficha completa como la sirve <c>GET /api/payroll/employees/{id}</c> (feature 008 + 010).
/// Clase con <c>init</c> y no record posicional porque la API suma campos al final y la
/// pantalla sólo lee lo que necesita.
/// </summary>
public sealed class FichaEmpleadoDto
{
    public Guid PublicId { get; init; }
    public Guid PersonPublicId { get; init; }
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public string IdentificationNumber { get; init; } = "";
    public string Email { get; init; } = "";
    public string Phone { get; init; } = "";
    public string Mobile { get; init; } = "";
    public string Address { get; init; } = "";
    public decimal Salary { get; init; }
    public int ContractType { get; init; }
    public DateTime HireDate { get; init; }
    public DateTime? TerminationDate { get; init; }
    public string TerminationCause { get; init; } = "";
    public int Status { get; init; }
    public string HealthInsuranceName { get; init; } = "";
    public string PensionProviderName { get; init; } = "";
    public string WorkRiskProviderName { get; init; } = "";
    public string PayrollBankAccountNumber { get; init; } = "";
    public int PayrollBankAccountType { get; init; }
    public Guid? HealthInsurancePublicId { get; init; }
    public Guid? PensionProviderPublicId { get; init; }
    public Guid? WorkRiskProviderPublicId { get; init; }
    public Guid? PayrollBankPublicId { get; init; }
    public string? PayrollBankName { get; init; }
    public Guid? WorkRiskRatePublicId { get; init; }
    public string WorkRiskRateName { get; init; } = "";
    public Guid? SeveranceProviderPublicId { get; init; }
    public string SeveranceProviderName { get; init; } = "";
    public Guid? FamilyCompensationFundPublicId { get; init; }
    public string FamilyCompensationFundName { get; init; } = "";
    public Guid? PayrollPlanPublicId { get; init; }
    public string PayrollPlanName { get; init; } = "";
    public DateTime? PayrollPlanEffectiveFrom { get; init; }
    public List<HistorialSalarialFichaDto> SalaryHistory { get; init; } = [];
    public List<MovimientoRecienteFichaDto> RecentEntries { get; init; } = [];

    // Feature 010
    public string EmployeeClass { get; init; } = "Standard";
    public FichaPilaDto? Pila { get; init; }
    public FichaDianDto? Dian { get; init; }
    /// <summary><c>ApprenticeStage</c>: 1 lectiva, 2 práctica.</summary>
    public int? ApprenticeStage { get; init; }
    public Guid? DisbursementBankPublicId { get; init; }
    public string? DisbursementBankName { get; init; }
    public string? DisbursementBankTransferCode { get; init; }
    public SaldoVacacionesFichaDto? VacationBalance { get; init; }
    public SaldoInicialFichaDto? OpeningBalance { get; init; }
    public PorcentajeRetencionFichaDto? CurrentWithholdingRate { get; init; }
    public TerminacionFichaDto? Termination { get; init; }

    public bool EsAprendizOPasante => EmployeeClass is "Apprentice" or "Intern";
    public string ClaseTexto => EmployeeClass switch
    {
        "Standard" => "Estándar", "IntegralSalary" => "Salario integral", "Apprentice" => "Aprendiz", "Intern" => "Pasante", "Pensioner" => "Pensionado", _ => EmployeeClass,
    };
}

public sealed record HistorialSalarialFichaDto(DateTime EffectiveDate, decimal NewSalary, string? UserName);

public sealed record MovimientoRecienteFichaDto(string ConceptName, decimal? Amount, string PeriodDescription);
