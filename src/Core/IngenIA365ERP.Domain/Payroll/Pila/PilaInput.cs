using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;

namespace IngenIA365ERP.Domain.Payroll.Pila;

/// <summary>
/// Todo lo que el motor puro de la PILA necesita, cargado por la capa de aplicación
/// (feature 010, US5; research R9). Como <see cref="CalculationInput"/>: aquí no hay
/// consultas, sólo datos; la explicación de cada campo cita de dónde salió cada uno.
/// </summary>
public sealed record PilaInput(
    short Year,
    byte Month,
    PilaEmployer Employer,
    PilaLayout Layout,
    ParameterSet Parameters,
    PilaPolicies Policies,
    IReadOnlyList<PilaContributor> Contributors)
{
    public DateTime MonthStart => new(Year, Month, 1);
    public DateTime MonthEnd => MonthStart.AddMonths(1).AddDays(-1);

    /// <summary>Campo 16: en planilla E el período de salud es el mes siguiente al de los otros sistemas.</summary>
    public DateTime HealthPeriod => MonthStart.AddMonths(1);
}

/// <summary>Datos del aportante para el registro tipo 1 (<c>PAY_PilaSettings</c> + <c>COR_Companies</c>).</summary>
public sealed record PilaEmployer(
    string Name,
    string Nit,
    string CheckDigit,
    string ContributorType,
    string ContributorClass,
    string PresentationForm,
    string? BranchCode,
    string? BranchName,
    string? ArlPilaCode,
    string? OperatorCode,
    string PlanillaType,
    string? DefaultMunicipalityDaneCode,
    string? DefaultEconomicActivityCode);

/// <summary>Políticas de la empresa vigentes al primer día del período.</summary>
/// <param name="Exonerada114_1">Exonerada de salud empleador, SENA e ICBF (ET art. 114-1, Ley 1607/2012) para IBC bajo el umbral.</param>
/// <param name="CotizaArlEnVacaciones">Si la ARL se cotiza en las líneas VAC (la norma lo deja a la empresa).</param>
public sealed record PilaPolicies(bool Exonerada114_1, bool CotizaArlEnVacaciones);

/// <summary>Un cotizante con lo que la ficha, los catálogos y las corridas aprobadas del mes dicen de él.</summary>
public sealed record PilaContributor
{
    public required int EmployeeId { get; init; }
    public required Guid EmployeePublicId { get; init; }

    // --- identificación (campos 3, 4, 11-14) ---
    public required string DocumentType { get; init; }
    public required string Document { get; init; }
    public required string FirstLastName { get; init; }
    public string? SecondLastName { get; init; }
    public required string FirstName { get; init; }
    public string? OtherNames { get; init; }

    // --- ficha ---
    public EmployeeClass Class { get; init; } = EmployeeClass.Standard;
    public ApprenticeStage? ApprenticeStage { get; init; }

    /// <summary>Tipo de cotizante fijado a mano en la ficha; nulo = se deriva de la clase y la etapa.</summary>
    public string? ContributorTypeOverride { get; init; }

    /// <summary>Subtipo fijado en la ficha; nulo = <c>00</c>, o <c>01</c> si es pensionado activo.</summary>
    public string? ContributorSubTypeOverride { get; init; }

    public bool ForeignNotRequiredToContributePension { get; init; }
    public bool ColombianAbroad { get; init; }
    public string? MunicipalityDaneCode { get; init; }
    public string? EconomicActivityCode { get; init; }
    public string? WorkCenterCode { get; init; }
    public bool HighRiskPension { get; init; }
    public PensionTransitionRegime TransitionRegime { get; init; } = PensionTransitionRegime.Unknown;

    public required DateTime HireDate { get; init; }
    public DateTime? TerminationDate { get; init; }

    /// <summary>Salario mensual vigente al último día del mes (campo 40); integral: el 100 %.</summary>
    public required decimal BasicSalary { get; init; }

    /// <summary>Fecha del cambio de salario dentro del mes (VSP), si lo hubo.</summary>
    public DateTime? SalaryChangeDate { get; init; }

    // --- códigos PILA de las administradoras (campos 31, 33, 35, 77) ---
    public string? PensionPilaCode { get; init; }
    public string? HealthPilaCode { get; init; }
    public string? FamilyCompensationPilaCode { get; init; }
    public string? WorkRiskPilaCode { get; init; }
    public bool HasPensionProvider { get; init; }
    public bool HasHealthProvider { get; init; }
    public bool HasFamilyCompensationFund { get; init; }
    public bool HasWorkRiskProvider { get; init; }

    /// <summary>Clase de riesgo 1..5 (<c>PAY_WorkRiskRates.Code</c>); nulo = ficha sin clase.</summary>
    public int? WorkRiskClass { get; init; }

    // --- lo que dicen las corridas aprobadas del mes ---

    /// <summary>Suma de los devengos que forman el IBC (sin auxilio de transporte) en las corridas aprobadas del mes, antes del 70 % integral y de los topes.</summary>
    public decimal ContributionEarnings { get; init; }

    /// <summary>Días liquidados en las corridas ordinarias del mes (suma de <c>DaysWorked</c>).</summary>
    public int DaysWorked { get; init; }

    /// <summary>Aporte voluntario del afiliado a pensión obligatoria (campo 48) en el mes.</summary>
    public decimal VoluntaryPensionEmployee { get; init; }

    /// <summary>Novedades con fechas dentro del mes que generan línea propia o bandera.</summary>
    public IReadOnlyList<PilaNovelty> Novelties { get; init; } = [];

    public string FullName => string.Join(" ", new[] { FirstName, OtherNames, FirstLastName, SecondLastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
}

/// <summary>Una novedad del mes con fechas (IGE, LMA, VAC, SLN, IRL) tal como la registró la nómina.</summary>
public sealed record PilaNovelty(PilaNoveltyKind Kind, DateTime Start, DateTime End, string? AuthorizationNumber = null, Guid? NoveltyPublicId = null);

public enum PilaNoveltyKind
{
    /// <summary>Incapacidad por enfermedad general (campo 25; ARL 0).</summary>
    IGE,

    /// <summary>Licencia de maternidad o paternidad (campo 26; ARL 0).</summary>
    LMA,

    /// <summary>Vacaciones o licencia remunerada (campo 27; ARL según política).</summary>
    VAC,

    /// <summary>Suspensión temporal o licencia no remunerada (campo 24; sólo empleador, FSP 0).</summary>
    SLN,

    /// <summary>Incapacidad por riesgo laboral (campo 30, días).</summary>
    IRL,
}
