namespace IngenIA365ERP.Domain.Enums.Lending;

// 2026-11-16 (pedido del dueño; feature 024, fase 3): el proceso de causación (LND_AccrualProcesses). (nuevo)

/// <summary>
/// La clase de un proceso de causación (feature 024, fase 3): qué hace al ponerse en firme. <see cref="Posting"/> contabiliza lo causado de los
/// conceptos en modo manual (<c>Causacion.Modo* = Manual</c>): el motor ya lo causó día a día y su hecho espera; el proceso ordena su lote. La fase 4
/// agrega <c>PeriodRecognition = 2</c> (el reconocimiento de la cuota completa al inicio del ciclo): su cálculo arma las líneas por crédito y su puesta
/// en firme emite los hechos, sobre la misma cabecera, el mismo borrador y el mismo control de cuatro ojos. (nuevo)
/// </summary>
public enum AccrualProcessKind
{
    /// <summary>Contabilizar lo causado que espera, por concepto y rango de días causados.</summary>
    Posting = 1,
}

/// <summary>
/// El estado de un proceso de causación (feature 024, fase 3):
/// <code>
/// Draft ──poner en firme (motivo)──▶ PendingApproval ──otra persona aprueba──▶ Confirmed ──reproceso en firme──▶ Reprocessed
///   ▲   └──recalcular──┘                 │ rechazo, retiro o recálculo
///   └────────────────────────────────────┘
/// Draft · PendingApproval ──anular (motivo)──▶ Voided
/// </code>
/// Un proceso en firme no se anula ni se borra: se reprocesa. (nuevo)
/// </summary>
public enum AccrualProcessStatus
{
    Draft = 1,
    PendingApproval = 2,
    Confirmed = 3,
    Reprocessed = 4,
    Voided = 5,
}

/// <summary>
/// La periodicidad con que se elige el rango del proceso (feature 024, fase 3). Los valores coinciden con los de <see cref="RepaymentPeriodicity"/>
/// (7, 10, 15, 30) para que la fase 4 empareje el proceso con la periodicidad de cada crédito; la diaria no es una periodicidad de pago y vale 1. (nuevo)
/// </summary>
public enum AccrualProcessPeriodicity
{
    Daily = 1,
    Weekly = 7,
    TenDay = 10,
    Biweekly = 15,
    Monthly = 30,
}

/// <summary>
/// Los conceptos que toma un proceso (feature 024, fase 3), uno por parámetro <c>Causacion.Modo*</c>: el interés corriente (<c>InteresCausado</c>), el
/// de mora (<c>MoraCausada</c>), el seguro (<c>SeguroCausado</c>), la administración (<c>AdministracionCausada</c>) y los otros rubros (la corrección
/// monetaria UVR, <c>CorreccionMonetariaUvrCausada</c>). Dos procesos se cruzan si comparten algún concepto. (nuevo)
/// </summary>
[Flags]
public enum AccrualProcessConcepts
{
    None = 0,
    CurrentInterest = 1,
    DefaultInterest = 2,
    Insurance = 4,
    Administration = 8,
    OtherItems = 16,
}

/// <summary>Si un hecho o una línea del borrador entra en lo que se contabiliza o queda fuera con su motivo. (nuevo)</summary>
public enum AccrualProcessItemStatus
{
    Included = 1,
    Excluded = 2,
}

/// <summary>
/// Por qué un hecho (o una línea) queda fuera del proceso (feature 024, fase 3). El hecho es la unidad que se contabiliza: lo causado de un día, una
/// sucursal y un concepto va en un solo mensaje con todos sus créditos, y no se parte. Por eso, cuando un crédito no entra (fuera del filtro, del alcance
/// o con abonos posteriores al reprocesar), el hecho entero queda fuera y los demás créditos del hecho llevan <see cref="SharedFact"/>. (nuevo)
/// </summary>
public enum AccrualExclusionReason
{
    /// <summary>El mes contable de la fecha del hecho está cerrado.</summary>
    AccountingPeriodClosed = 1,

    /// <summary>El mes contable de la fecha del hecho no existe en Contabilidad.</summary>
    AccountingPeriodMissing = 2,

    /// <summary>La contabilidad no está iniciada.</summary>
    AccountingNotStarted = 3,

    /// <summary>Cartera ya cerró el mes del día causado.</summary>
    LendingMonthClosed = 4,

    /// <summary>Falta la regla contable y <c>Contabilidad.FaltaDeRegla</c> bloquea: el proceso no se pone en firme hasta corregir la matriz.</summary>
    PostingRuleMissing = 5,

    /// <summary>El hecho lleva créditos de oficinas o líneas fuera del alcance de quien opera.</summary>
    OutOfScope = 6,

    /// <summary>El hecho lleva créditos fuera del filtro de línea o de pagaduría.</summary>
    OutOfFilter = 7,

    /// <summary>Reproceso: el crédito tuvo abonos después de la puesta en firme del proceso que se reprocesa; queda intacto.</summary>
    LaterPayment = 8,

    /// <summary>El crédito va en un hecho que quedó fuera por otro crédito (fuera del filtro, del alcance o con abonos posteriores).</summary>
    SharedFact = 9,

    /// <summary>Contabilidad no respondió la validación previa y <c>Contabilidad.PoliticaSinRespuesta</c> bloquea.</summary>
    AccountingNoResponse = 10,
}
