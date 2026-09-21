using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Calculation.Bases;
using IngenIA365ERP.Domain.Payroll.Calculation.Rules;

namespace IngenIA365ERP.Domain.Payroll.Settlements;

/// <summary>
/// Lo que toda regla de liquidación puede consultar: la entrada, los parámetros y
/// conceptos vigentes al corte, las líneas que van saliendo y los avisos. Es el hermano de
/// <see cref="RuleContext"/> para un corte en vez de un período: aquí los tramos de salario
/// se piden por ventana de fechas, porque la prima mira un semestre, las cesantías un año
/// y las vacaciones toda la vinculación.
/// </summary>
public sealed class SettlementContext
{
    public required SettlementInput Input { get; init; }
    public required ParameterSet Parameters { get; init; }
    public required ConceptSet Concepts { get; init; }

    public List<CalculationLine> Lines { get; } = [];
    public List<SettlementSkip> Skips { get; } = [];
    public List<string> Warnings { get; } = [];
    public List<string> Refusals { get; } = [];
    public List<ExplanationStep> BaseSteps { get; } = [];
    public SettlementFlags Flags { get; set; } = SettlementFlags.None;
    public VacationBalance Vacations { get; set; } = VacationBalance.Empty;

    public SettlementEmployeeInput Employee => Input.Employee;
    public DateTime Cutoff => Input.CutoffDate.Date;
    public SettlementPolicies Policies => Input.Policies;

    /// <summary>Fin efectivo de cualquier ventana: el corte, o el retiro si es anterior.</summary>
    public DateTime EffectiveEnd =>
        Employee.TerminationDate is { } t && t.Date < Cutoff ? t.Date : Cutoff;

    public decimal Round(decimal value) => PayrollCalculationEngine.Round(value, Policies.Rounding);

    public void Add(CalculationLine line)
    {
        line.Amount = Round(line.RawAmount);
        line.Order = Lines.Count + 1;
        Lines.Add(line);
    }

    public void Skip(string conceptCode, string reasonCode, string text) => Skips.Add(new SettlementSkip(conceptCode, reasonCode, text));

    public void Warn(string text) => Warnings.Add(text);

    public void Refuse(string text) => Refusals.Add(text);

    /// <summary>La versión vigente del concepto, o nulo dejando el motivo en las omisiones.</summary>
    public PayrollConceptDefinition? Concept(string code)
    {
        var def = Concepts.Find(code);
        if (def is null)
            Skip(code, SettlementReasonCodes.ConceptoSinVersionVigente,
                $"{code}: no hay una versión vigente del concepto al {Fmt.Date(Cutoff)}; el rubro no se liquidó. Reaplique la semilla de nómina o cree el concepto.");
        return def;
    }

    public decimal LineAmount(string code) =>
        Lines.Where(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase)).Sum(l => l.Amount);

    public CalculationLine? Line(string code) =>
        Lines.FirstOrDefault(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

    // ------------------------------------------------------------ fechas y tramos --

    /// <summary>Primer día que cuenta para el empleado: su ingreso, o el inicio de la etapa práctica si es aprendiz.</summary>
    public DateTime EmploymentStart
    {
        get
        {
            var inicio = Employee.JoinDate.Date;
            if (Employee.Class == EmployeeClass.Apprentice && Employee.ApprenticeStage == ApprenticeStage.Practical
                && Employee.ApprenticeStageFrom is { } desde && desde.Date > inicio)
                inicio = desde.Date;
            return inicio;
        }
    }

    /// <summary>Tramos de salario dentro de una ventana, recortados por ingreso y retiro y con las ausencias descontadas del salario.</summary>
    public IReadOnlyList<SalaryTranche> Tranches(DateTime from, DateTime to)
    {
        if (to.Date < from.Date) return [];
        var period = new PeriodInput(from.Date, to.Date, PayrollPeriodicity.Monthly);
        var employee = new EmployeeInput
        {
            PublicId = Employee.PublicId,
            DisplayName = Employee.DisplayName,
            Class = Employee.Class,
            JoinDate = EmploymentStart,
            TerminationDate = Employee.TerminationDate,
            SalaryHistory = Employee.SalaryHistory,
        };
        var ausencias = Input.Absences.Select(a => (a.From.Date, a.To.Date)).ToList();
        return SalaryTranches.Build(period, employee, ausencias);
    }

    /// <summary>Días comerciales de suspensión del contrato dentro de la ventana (CST art. 53).</summary>
    public int SuspensionDays(DateTime from, DateTime to)
    {
        var total = 0;
        foreach (var a in Input.Absences.Where(a => a.IsSuspension))
        {
            var solape = CalendarConventions.Overlap(from.Date, to.Date, a.From.Date, a.To.Date);
            if (solape is { } s) total += CalendarConventions.Days(s.From, s.To);
        }
        return total;
    }

    /// <summary>Salario mensual vigente en una fecha, según el historial.</summary>
    public decimal SalaryAt(DateTime date)
    {
        var history = Employee.SalaryHistory.OrderBy(h => h.EffectiveDate).ToList();
        if (history.Count == 0)
            throw new CalculationRefusedException($"El empleado {Employee.DisplayName} ({Employee.PublicId}) no tiene salario registrado.", []);
        return (history.LastOrDefault(h => h.EffectiveDate.Date <= date.Date) ?? history[0]).MonthlySalary;
    }

    /// <summary>Hubo un cambio de salario con efecto dentro de (desde, hasta]. Un cambio justo el primer día de la ventana no cuenta como cambio.</summary>
    public bool SalaryChangedWithin(DateTime exclusiveFrom, DateTime inclusiveTo) =>
        Employee.SalaryHistory.Any(h => h.EffectiveDate.Date > exclusiveFrom.Date && h.EffectiveDate.Date <= inclusiveTo.Date);

    // ------------------------------------------------------------ auxilio y variables --

    /// <summary>
    /// El auxilio de transporte mensual que corresponde a un salario, o cero: la ficha dice
    /// que lo devenga, el concepto existe y aplica a la clase, y el salario no supera el tope
    /// en SMMLV (FR-012). Todo desde parámetros.
    /// </summary>
    public decimal TransportAllowanceFor(decimal monthlySalary)
    {
        if (!Employee.TransportAllowanceEntitled) return 0m;
        var def = Concepts.Find(WellKnownConceptCodes.TransportAllowance);
        if (def is null || !def.AppliesTo(Employee.Class)) return 0m;
        var smmlv = Parameters.Value(LegalParameterCodes.Smmlv);
        var tope = smmlv * Parameters.Value(LegalParameterCodes.TransportAllowanceCapSmmlv);
        return monthlySalary <= tope ? Parameters.Value(LegalParameterCodes.TransportAllowance) : 0m;
    }

    /// <summary>Descripción del derecho al auxilio para la explicación.</summary>
    public string TransportAllowanceText(decimal monthlySalary)
    {
        var aux = TransportAllowanceFor(monthlySalary);
        if (aux == 0m) return "sin auxilio de transporte";
        var p = Parameters.Describe(LegalParameterCodes.TransportAllowance);
        return $"auxilio de transporte {Fmt.Money(aux)} ({p.Code}, vigente desde {Fmt.Date(p.ValidFrom)})";
    }

    /// <summary>Promedio mensual de los devengos variables (prestacionales o de vacaciones) de los meses de la ventana que trajeron las corridas aprobadas.</summary>
    public decimal AverageVariable(DateTime from, DateTime to, bool vacationBase)
    {
        var meses = Input.MonthlyBases
            .Where(m => EstaEnVentana(m, from, to))
            .ToList();
        if (meses.Count == 0) return 0m;
        var total = meses.Sum(m => vacationBase ? m.VariableVacationEarnings : m.VariableBenefitsEarnings);
        return total / meses.Count;
    }

    /// <summary>
    /// Ingreso laboral mensual del empleado, promediado sobre los últimos <paramref name="months"/>
    /// meses de corridas aprobadas; si no las hay, salario al corte más auxilio y variables. Es lo
    /// que las reglas de retención comparan con los topes en UVT (ET arts. 206 num. 4 y 401-3).
    /// </summary>
    public (decimal Value, string Text) MonthlyIncome(int months)
    {
        var hasta = Cutoff;
        var desde = new DateTime(hasta.Year, hasta.Month, 1).AddMonths(1 - months);
        var meses = Input.MonthlyBases.Where(m => EstaEnVentana(m, desde, hasta)).ToList();
        if (meses.Count > 0)
        {
            var promedio = meses.Sum(m => m.LaborIncome) / meses.Count;
            return (promedio, $"promedio del ingreso laboral de {meses.Count} meses de corridas aprobadas");
        }
        var salario = SalaryAt(hasta);
        var estimado = salario + TransportAllowanceFor(salario) + AverageVariable(desde, hasta, vacationBase: false);
        return (estimado, "salario vigente al corte más auxilio y variables (sin corridas aprobadas informadas)");
    }

    private static bool EstaEnVentana(MonthlyBaseInput m, DateTime from, DateTime to)
    {
        var inicio = new DateTime(m.Year, m.Month, 1);
        var fin = inicio.AddMonths(1).AddDays(-1);
        return fin >= from.Date && inicio <= to.Date;
    }

    // ------------------------------------------------------------ elegibilidad --

    /// <summary>
    /// Por qué el empleado no tiene derecho a prestaciones (prima, cesantías, intereses,
    /// vacaciones): salario integral (CST art. 132), aprendiz en etapa lectiva (Ley 2466/2025) o
    /// pasante sin contrato de aprendizaje (FR-009, FR-013). Nulo = tiene derecho. Las vacaciones
    /// las conserva el salario integral (edge case de la spec).
    /// </summary>
    public string? BenefitsExclusion(bool forVacations = false)
    {
        switch (Employee.Class)
        {
            case EmployeeClass.IntegralSalary when !forVacations:
                return SettlementReasonCodes.SalarioIntegral;
            case EmployeeClass.Intern:
                return SettlementReasonCodes.Pasante;
            case EmployeeClass.Apprentice when Employee.ApprenticeStage != ApprenticeStage.Practical:
                return SettlementReasonCodes.AprendizLectiva;
            default:
                return null;
        }
    }

    public static string ExclusionText(string reasonCode) => reasonCode switch
    {
        SettlementReasonCodes.SalarioIntegral => "Salario integral (CST art. 132): la prima y las cesantías están incluidas en el factor prestacional; no se liquidan.",
        SettlementReasonCodes.AprendizLectiva => "Aprendiz en etapa lectiva (Ley 2466/2025 art. 21): apoyo de sostenimiento sin prestaciones; queda fuera.",
        SettlementReasonCodes.Pasante => "Pasante sin contrato de aprendizaje: sin prestaciones sociales; queda fuera.",
        SettlementReasonCodes.YaPagadaEnDefinitiva => "La prima proporcional de este semestre ya se pagó en la liquidación definitiva aprobada del empleado (FR-009).",
        SettlementReasonCodes.YaPagadaEnCorridaSemestral => "La prima de este semestre ya se pagó en la corrida semestral aprobada antes de registrar el retiro: la definitiva no la liquida de nuevo (D-28).",
        SettlementReasonCodes.YaPagadaEnCorridaAnual => "Las cesantías y los intereses del año ya se pagaron en la corrida anual aprobada hasta su corte y el retiro no deja días posteriores: la definitiva no los liquida de nuevo (D-28).",
        SettlementReasonCodes.SinDiasEnElSemestre => "El empleado no tiene días vinculados dentro del semestre.",
        SettlementReasonCodes.SinDiasEnElAnio => "El empleado no tiene días vinculados dentro del año.",
        _ => reasonCode,
    };
}
