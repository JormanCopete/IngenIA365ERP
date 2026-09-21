using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Application.Payroll.Settlements.Common;

/// <summary>
/// Códigos de error de las liquidaciones especiales (feature 010, contracts/api.md §3.1–3.5).
/// Mensajes en español que dicen qué pasó y qué hacer (Principio IX); los que la pantalla
/// necesita desarmar llevan <c>data</c>. Todos responden 422 salvo <c>*.NotFound</c> (404) y
/// los avisos, que no son errores: viajan en <c>warnings[]</c> con su código.
/// </summary>
public static class SettlementErrors
{
    // ------------------------------------------------------------ comunes (§3.5) --

    public static Error Duplicate(Guid runPublicId, PayrollRunStatus status, string descripcion) =>
        new ErrorConDatos("Payroll.Settlement.Duplicate",
            $"Ya existe {descripcion} en estado {status}. Recalcúlela, descártela o revérsela antes de crear otra (FR-005).",
            new { runPublicId, status = status.ToString() });

    public static Error NoEligibleEmployees(IReadOnlyList<ExcludedEmployeeDto> excluded) =>
        new ErrorConDatos("Payroll.Settlement.NoEligibleEmployees",
            "Ningún empleado tiene derecho a esta liquidación en el período: la lista dice por qué quedó fuera cada uno.",
            new { excluded });

    public static Error ParametersMissing(IReadOnlyList<string> codes, DateTime asOf) =>
        new ErrorConDatos("Payroll.Settlement.ParametersMissing",
            $"No hay vigencia al {asOf:dd/MM/yyyy} para los parámetros legales: {string.Join(", ", codes)}. " +
            "Regístrelos en Nómina › Parámetros legales antes de liquidar.",
            new { codes = codes.Select(c => new { code = c, asOf = asOf.ToString("yyyy-MM-dd") }).ToList() });

    public static Error ConceptAccountsMissing(IReadOnlyList<string> conceptCodes) =>
        new ErrorConDatos("Payroll.Settlement.ConceptAccountsMissing",
            $"Conceptos de la liquidación sin cuentas contables configuradas: {string.Join(", ", conceptCodes)}. " +
            "Configúrelas en Nómina › Conceptos › Cuentas antes de aprobar; no se agregó nada al libro.",
            new { conceptCodes });

    public static readonly Error AccountingNotInitialized = new("Payroll.Settlement.AccountingNotInitialized",
        "La contabilidad de la cooperativa no está iniciada: la liquidación no se puede aprobar porque no hay dónde contabilizarla. " +
        "La inicia quien tenga Accounting.Setup.Manage (Contabilidad › Configuración inicial).");

    public static Error NotDraft(PayrollRunStatus status) =>
        new ErrorConDatos("Payroll.Settlement.NotDraft", $"La liquidación está {status}: sólo se aprueba, ajusta o descarta el borrador vigente.", new { status = status.ToString() });

    public static Error NotApproved(PayrollRunStatus status) =>
        new ErrorConDatos("Payroll.Settlement.NotApproved", $"La liquidación está {status}: sólo se reversa una liquidación aprobada.", new { status = status.ToString() });

    public static Error AlreadyReversed(PayrollRunStatus status) =>
        new ErrorConDatos("Payroll.Settlement.AlreadyReversed", "La liquidación ya fue reversada.", new { status = status.ToString() });

    public static Error KindMismatch(PayrollRunKind kind, PayrollRunKind expected) =>
        new ErrorConDatos("Payroll.Settlement.KindMismatch",
            $"La corrida es una {Nombre(kind)} y esta ruta es de {Nombre(expected)}. Use la ruta de su tipo: {RutaDe(kind)}.",
            new { kind = kind.ToString(), expected = expected.ToString() });

    public static Error SegregationViolation(string calculatedBy) =>
        new ErrorConDatos("Payroll.Settlement.SegregationViolation",
            $"Quien calculó la liquidación ({calculatedBy}) no puede aprobarla. Otra persona con el permiso de aprobar debe hacerlo, " +
            "o la cooperativa habilita la política AllowSameUserApproval en Nómina › Políticas y quien aprueba confirma expresamente.",
            new { calculatedBy });

    public static readonly Error ConfirmationRequired = new("Payroll.Settlement.ConfirmationRequired",
        "La aprobación exige confirmación explícita (confirm = true).");

    public static readonly Error ConfirmWithoutSegregationRequired = new("Payroll.Settlement.ConfirmationRequired",
        "Usted calculó esta liquidación. La cooperativa permite aprobarla igual, pero exige una segunda confirmación (confirmWithoutSegregation = true); quedará registrado.");

    public static readonly Error EmptyRunRequiresConfirmation = new("Payroll.Settlement.ConfirmationRequired",
        "La liquidación no tiene empleados con derecho. Aprobarla vacía exige confirmEmpty = true.");

    public static Error PostingDateInvalid(DateOnly postingDate, DateOnly cutoff, DateOnly today) =>
        new ErrorConDatos("Payroll.Settlement.PostingDateInvalid",
            $"La fecha del comprobante ({postingDate:dd/MM/yyyy}) debe estar entre la fecha de corte ({cutoff:dd/MM/yyyy}) y hoy ({today:dd/MM/yyyy}).",
            new { postingDate, cutoffDate = cutoff, today });

    public static Error UseSettlementRoute(PayrollRunKind kind) =>
        new ErrorConDatos("Payroll.Settlement.UseSettlementRoute",
            $"Esta corrida es una {Nombre(kind)}: se aprueba, reversa o descarta por su propia ruta con su propio permiso, no por la de la nómina ordinaria. Ruta: {RutaDe(kind)}.",
            new { kind = kind.ToString(), route = RutaDe(kind) });

    public static Error CalculationRefused(string employeeName, string reason) =>
        new("Payroll.Settlement.CalculationRefused", $"El motor se negó a liquidar a {employeeName}: {reason}");

    public static readonly Error RunNotFound = new("Payroll.Run.NotFound", "No existe la corrida indicada.");
    public static readonly Error EmployeeNotFound = new("Payroll.Employee.NotFound", "No existe el empleado indicado.");
    public static readonly Error ReasonRequired = new("Payroll.Settlement.ReasonRequired", "Indique el motivo.");
    public static readonly Error NothingToPost = new("Payroll.Settlement.NothingToPost", "La liquidación no tiene líneas que afecten contabilidad.");
    public static readonly Error PaymentBlocksReversal = new("Payroll.PaymentBlocksReversal",
        "Hay empleados con marca de pago vigente en esta liquidación. Retire las marcas (con motivo) antes de reversar.");
    public static readonly Error HasBlockers = new("Payroll.Settlement.ApprovalBlocked",
        "La liquidación tiene bloqueos (neto negativo, descuentos sobre el neto, negativas del motor). Corrija la causa y recalcule antes de aprobar.");

    // -------------------------------------------------------- avisos (no bloquean) --

    public const string OpeningBalanceMissingCode = "Payroll.Settlement.OpeningBalanceMissing";
    public const string PortfolioUnavailableCode = "Payroll.Settlement.PortfolioUnavailable";

    public static WarningDto OpeningBalanceMissing(IReadOnlyList<Guid> employeePublicIds, IReadOnlyList<string> names) =>
        new(OpeningBalanceMissingCode,
            $"Ingreso anterior al arranque de la nómina sin saldo inicial de prestaciones: {string.Join(", ", names)}. Regístrelo en Nómina › Saldos iniciales o confirme que no aplica.",
            new { employeePublicIds });

    public static WarningDto PortfolioUnavailable(string detail) =>
        new(PortfolioUnavailableCode,
            "Cartera no respondió al consultar las obligaciones del empleado: la propuesta de descuentos sale vacía. Revise Cartera y recalcule antes de aprobar. " + detail,
            null);

    // ----------------------------------------------------- prima (§3.1) y cesantías (§3.2) --

    public static readonly Error SemesterInvalid = new("Payroll.ServiceBonus.SemesterInvalid", "El semestre es 1 (enero–junio) o 2 (julio–diciembre).");
    public static readonly Error SeveranceNotApproved = new("Payroll.Severance.NotApproved", "La consignación se marca sobre una liquidación de cesantías aprobada.");
    public static Error SeveranceAlreadyDeposited(Guid fundPublicId) =>
        new ErrorConDatos("Payroll.Severance.AlreadyDeposited", "Ya se registró la consignación a ese fondo para esta liquidación.", new { fundPublicId });
    public static readonly Error SeveranceFundFormatMissing = new("Payroll.Severance.FundFormatMissing", "El fondo no tiene un formato de archivo vigente.");

    // ----------------------------------------------------------- vacaciones (§3.3) --

    public static readonly Error VacationDatesInvalid = new("Payroll.Vacation.DatesInvalid", "La fecha final del disfrute debe ser igual o posterior a la inicial.");
    public static readonly Error VacationNoWorkingDays = new("Payroll.Vacation.NoWorkingDays", "El rango no tiene ningún día hábil: son todos domingos o festivos.");
    public static Error VacationNoBalance(decimal pendingDays) =>
        new ErrorConDatos("Payroll.Vacation.NoBalance", $"El empleado no tiene saldo de vacaciones suficiente (pendientes: {pendingDays:0.##} días hábiles).", new { pendingDays });
    public static Error VacationCompensationOverMax(decimal requestedDays, decimal maxDays, decimal accruedDays) =>
        new ErrorConDatos("Payroll.Vacation.CompensationOverMax",
            $"Se pidieron {requestedDays:0.##} días en dinero y el máximo compensable es {maxDays:0.##} (parámetro VACACIONES_COMPENSABLE_PCT sobre {accruedDays:0.##} causados, CST art. 189).",
            new { requestedDays, maxDays, accruedDays, policyCode = "VACACIONES_COMPENSABLE_PCT" });
    public static Error VacationPeriodApproved(Guid periodPublicId, Guid? retroactiveTargetPeriodPublicId) =>
        new ErrorConDatos("Payroll.Vacation.PeriodApproved",
            "El disfrute cae en un período de nómina ya aprobado: la novedad se ofrece como ajuste retroactivo en el período abierto.",
            new { periodPublicId, retroactiveTargetPeriodPublicId });
    public static Error VacationOverlaps(Guid movementPublicId) =>
        new ErrorConDatos("Payroll.Vacation.Overlaps", "Las fechas se cruzan con otro disfrute registrado del mismo empleado.", new { movementPublicId });
    public static readonly Error VacationEmployeeTerminated = new("Payroll.Vacation.EmployeeTerminated", "El empleado está retirado: las vacaciones pendientes se pagan en la liquidación definitiva.");
    public static readonly Error VacationMovementConfirmed = new("Payroll.Vacation.MovementConfirmed", "El movimiento ya fue liquidado por una corrida aprobada: reverse la corrida en vez de anularlo.");
    public static readonly Error VacationMovementNotFound = new("Payroll.Vacation.MovementNotFound", "No existe el movimiento de vacaciones indicado.");
    public static Error VacationMovementCancelled(string queHacer) =>
        new("Payroll.Vacation.MovementCancelled", $"El movimiento de vacaciones está anulado: {queHacer}");

    // ---------------------------------------------- terminación y definitiva (§3.4) --

    public static Error TerminationPeriodApproved(Guid periodPublicId, string periodName, Guid? openPeriodPublicId) =>
        new ErrorConDatos("Payroll.Termination.PeriodApproved",
            $"La fecha de retiro cae en el período «{periodName}», que ya está aprobado. Reverse ese período o liquide con una fecha dentro del período abierto (FR-021).",
            new { periodPublicId, periodName, openPeriodPublicId });
    public static readonly Error TerminationDateBeforeHire = new("Payroll.Termination.DateBeforeHire", "La fecha de retiro es anterior a la fecha de ingreso.");
    public static readonly Error TerminationDateInFuture = new("Payroll.Termination.DateInFuture", "La fecha de retiro no puede ser posterior a hoy.");
    public static Error EmployeeAlreadyTerminated(Guid terminationPublicId) =>
        new ErrorConDatos("Payroll.Termination.EmployeeAlreadyTerminated", "El empleado ya tiene una terminación viva registrada.", new { terminationPublicId });
    public static Error PendingSettlement(Guid runPublicId) =>
        new ErrorConDatos("Payroll.Termination.PendingSettlement", "El empleado ya tiene una liquidación definitiva en borrador o aprobada.", new { runPublicId });
    public static readonly Error TerminationReasonNotFound = new("Payroll.Termination.ReasonNotFound", "No existe el motivo de retiro indicado o está inactivo.");
    public static readonly Error ContractEndDateRequired = new("Payroll.Termination.ContractEndDateRequired",
        "En un contrato a término fijo o por obra cuyo motivo genera indemnización, indique hasta cuándo iba el contrato (el tiempo que faltaba, CST art. 64).");
    public static readonly Error TerminationReasonSeeded = new("Payroll.Termination.ReasonSeeded", "Un motivo sembrado por el programa no cambia de código ni de marca de indemnización.");
    public static readonly Error TerminationNotFound = new("Payroll.Termination.NotFound", "No existe la terminación indicada.");

    public static Error DeductionAboveProposed(decimal proposed) =>
        new ErrorConDatos("Payroll.Settlement.DeductionAboveProposed", $"El descuento aplicado no puede superar lo propuesto ({proposed:N0}). Sólo se baja, con motivo.", new { proposed });
    public static readonly Error DeductionReasonRequired = new("Payroll.Settlement.DeductionReasonRequired", "Bajar un descuento propuesto exige motivo.");
    public static readonly Error DeductionNotFound = new("Payroll.Settlement.DeductionNotFound", "No existe ese descuento en la liquidación.");
    public static Error ConceptMissing(string conceptCode, DateTime asOf) =>
        new ErrorConDatos("Payroll.Settlement.ConceptMissing",
            $"No hay una versión vigente del concepto {conceptCode} al {asOf:dd/MM/yyyy}: no se puede volver línea el descuento. Revise Nómina › Conceptos.",
            new { conceptCode, asOf = asOf.ToString("yyyy-MM-dd") });

    // ------------------------------------------------------------ prima (§3.1) --

    public static readonly Error KeyMissing = new("Payroll.Settlement.KeyMissing",
        "La corrida de prima no tiene año y semestre: no se puede recalcular.");

    // ------------------------------------------------------------------ textos --

    public static string Nombre(PayrollRunKind kind) => kind switch
    {
        PayrollRunKind.Ordinary => "nómina ordinaria",
        PayrollRunKind.ServiceBonus => "prima de servicios",
        PayrollRunKind.Severance => "liquidación de cesantías e intereses",
        PayrollRunKind.Vacation => "liquidación de vacaciones",
        PayrollRunKind.Settlement => "liquidación definitiva",
        _ => kind.ToString(),
    };

    /// <summary>La ruta propia de cada tipo (contracts/api.md §3); la ordinaria sigue en <c>/api/payroll/runs</c>.</summary>
    public static string RutaDe(PayrollRunKind kind) => kind switch
    {
        PayrollRunKind.Ordinary => "/api/payroll/runs/{runId}",
        PayrollRunKind.ServiceBonus => "/api/payroll/settlements/service-bonus/{runId}",
        PayrollRunKind.Severance => "/api/payroll/settlements/severance/{runId}",
        PayrollRunKind.Vacation => "/api/payroll/settlements/vacations/{runId}",
        PayrollRunKind.Settlement => "/api/payroll/settlements/terminations/{runId}",
        _ => "/api/payroll/settlements",
    };
}
