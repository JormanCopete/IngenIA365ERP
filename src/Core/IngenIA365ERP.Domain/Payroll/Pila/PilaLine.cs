using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Payroll.Pila;

/// <summary>
/// Un registro tipo 2 tipado (feature 010, US5): las cifras que la cooperativa lee y cuadra,
/// los 98 valores por número de campo (sin formatear: el <see cref="PilaWriter"/> los pone en
/// su posición) y la explicación de cada valor calculado.
/// </summary>
public sealed class PilaLine
{
    public int LineNumber { get; set; }
    public required PilaContributor Contributor { get; init; }

    /// <summary>Qué representa la línea: la base del cotizante o una novedad con IBC propio.</summary>
    public PilaNoveltyKind? Segment { get; init; }

    public string ContributorType { get; set; } = "1";
    public string ContributorSubType { get; set; } = "00";

    public List<string> Flags { get; } = [];

    public int DaysPension { get; set; }
    public int DaysHealth { get; set; }
    public int DaysWorkRisk { get; set; }
    public int DaysFamilyCompensation { get; set; }

    public decimal Salary { get; set; }
    public bool IntegralSalary { get; set; }

    public decimal IbcPension { get; set; }
    public decimal IbcHealth { get; set; }
    public decimal IbcWorkRisk { get; set; }
    public decimal IbcFamilyCompensation { get; set; }
    public decimal IbcOtherParafiscal { get; set; }

    /// <summary>Devengo de la línea antes del 70 % integral y de los topes: la exoneración del art. 114-1 mira lo devengado, no el IBC.</summary>
    public decimal GrossEarnings { get; set; }

    public decimal PensionRate { get; set; }
    public decimal HealthRate { get; set; }
    public decimal WorkRiskRate { get; set; }
    public decimal FamilyCompensationRate { get; set; }
    public decimal SenaRate { get; set; }
    public decimal IcbfRate { get; set; }

    public decimal Pension { get; set; }
    public decimal VoluntaryEmployee { get; set; }
    public decimal VoluntaryEmployer { get; set; }
    public decimal PensionTotal { get; set; }
    public decimal SolidarityFund { get; set; }
    public decimal SubsistenceFund { get; set; }
    public decimal Health { get; set; }
    public decimal WorkRisk { get; set; }
    public decimal FamilyCompensation { get; set; }
    public decimal Sena { get; set; }
    public decimal Icbf { get; set; }

    public bool Exempt { get; set; }
    public int Hours { get; set; }
    public int IrlDays { get; set; }

    /// <summary>Valores por número de campo, sin formatear (string, decimal, int, DateTime, bool).</summary>
    public Dictionary<int, object?> Values { get; } = [];

    public List<PilaFieldExplanation> Explanations { get; } = [];

    public decimal TotalContributions => PensionTotal + SolidarityFund + SubsistenceFund + Health + WorkRisk + FamilyCompensation + Sena + Icbf;

    public void Explain(int field, string source, string detail, decimal? value = null) =>
        Explanations.Add(new PilaFieldExplanation(field, source, detail, value));
}

/// <summary>De dónde salió el valor de un campo: <c>Calculation</c>, <c>Profile</c>, <c>Settings</c>, <c>Constant</c>, <c>Blank</c>.</summary>
public sealed record PilaFieldExplanation(int Field, string Source, string Detail, decimal? Value);

/// <summary>Una inconsistencia del motor o del validador, en el lenguaje del operador.</summary>
public sealed record PilaIssueItem(PilaIssueSeverity Severity, string Code, string Message, byte? Field = null, int? EmployeeId = null, Guid? EmployeePublicId = null, string? EmployeeName = null, string? LinkRoute = null);

/// <summary>Lo que produce el motor: la cabecera, las líneas, las inconsistencias y los totales.</summary>
public sealed class PilaResult
{
    public Dictionary<int, object?> HeaderValues { get; } = [];
    public List<PilaLine> Lines { get; } = [];
    public List<PilaIssueItem> Issues { get; } = [];

    public int ContributorCount { get; set; }
    public int LineCount => Lines.Count;
    public decimal TotalIbcHealth => Lines.Sum(l => l.IbcHealth);
    public decimal TotalIbcPension => Lines.Sum(l => l.IbcPension);
    public decimal TotalIbcWorkRisk => Lines.Sum(l => l.IbcWorkRisk);
    public decimal TotalIbcFamilyCompensation => Lines.Sum(l => l.IbcFamilyCompensation);
    public decimal TotalHealth => Lines.Sum(l => l.Health);
    public decimal TotalPension => Lines.Sum(l => l.PensionTotal);
    public decimal TotalSolidarityFund => Lines.Sum(l => l.SolidarityFund + l.SubsistenceFund);
    public decimal TotalWorkRisk => Lines.Sum(l => l.WorkRisk);
    public decimal TotalFamilyCompensation => Lines.Sum(l => l.FamilyCompensation);
    public decimal TotalSena => Lines.Sum(l => l.Sena);
    public decimal TotalIcbf => Lines.Sum(l => l.Icbf);
    public decimal TotalContributions => Lines.Sum(l => l.TotalContributions);

    public bool HasBlocking => Issues.Any(i => i.Severity == PilaIssueSeverity.Blocking);
    public int BlockingCount => Issues.Count(i => i.Severity == PilaIssueSeverity.Blocking);
    public int WarningCount => Issues.Count(i => i.Severity == PilaIssueSeverity.Warning);
}
