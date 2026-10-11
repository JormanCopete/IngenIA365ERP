using System.Globalization;

namespace IngenIA365ERP.Shared.Services.Cartera;

/// <summary>
/// Los textos en español de los créditos, el estado de cuenta, la liquidación, el libro y las corridas (feature 013, US4, T0508–T0510), con los
/// nombres de <c>Domain/Enums/Lending</c> que viajan como texto. Ninguno muestra un código técnico (FR-013); un valor que no conoce se muestra tal
/// cual en vez de inventarle un nombre. (nuevo)
/// </summary>
public static class TextosDeCreditos
{
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    public static string Pesos(decimal valor) => valor.ToString("C2", Colombia);

    public static string Fecha(DateOnly? fecha) => fecha?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "—";

    public static string Instante(DateTime? instante) => instante is { } i
        ? DateTime.SpecifyKind(i, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
        : "—";

    /// <summary>Una tasa como fracción (0,245) en porcentaje con dos decimales.</summary>
    public static string Porcentaje(decimal? fraccion) => fraccion is { } f ? (f * 100m).ToString("N2", Colombia) + " %" : "—";

    public static IReadOnlyList<(string Valor, string Texto)> EstadosDeCredito { get; } =
    [
        ("Active", "Vigente"), ("PendingDisbursement", "Pendiente de giro"), ("PaidOff", "Cancelado"), ("WrittenOff", "Castigado"),
        ("Voided", "Anulado"), ("Novated", "Novado"),
    ];

    public static string EstadoDeCredito(string? v) => EstadosDeCredito.FirstOrDefault(e => e.Valor == v).Texto ?? v ?? "—";

    public static string Origen(string? v) => v switch
    {
        "Origination" => "Originado en Cartera",
        "Commercial" => "Venta a crédito",
        "Migration" => "Saldo inicial",
        _ => v ?? "—",
    };

    public static string Concepto(string? v) => v switch
    {
        "Principal" => "Capital",
        "CurrentInterest" => "Interés corriente",
        "DefaultInterest" => "Interés de mora",
        "CurrentInterestInSuspense" => "Interés corriente en suspenso",
        "DefaultInterestInSuspense" => "Interés de mora en suspenso",
        "Insurance" => "Seguro",
        "CollectionFee" => "Honorarios de cobranza",
        "OtherCharge" => "Otros cargos",
        "Advance" => "Anticipo",
        "CreditBalance" => "Saldo a favor",
        _ => v ?? "—",
    };

    /// <summary>Los conceptos en el orden en que se muestran los saldos.</summary>
    public static IReadOnlyList<string> OrdenDeConceptos { get; } =
    [
        "Principal", "CurrentInterest", "DefaultInterest", "CurrentInterestInSuspense", "DefaultInterestInSuspense", "Insurance", "CollectionFee",
        "OtherCharge",
    ];

    public static string ClaseDeOperacion(string? v) => v switch
    {
        "Disbursement" => "Desembolso",
        "OpeningBalance" => "Saldo inicial",
        "Payment" => "Pago",
        "PaymentReversal" => "Reverso de pago",
        "DisbursementReversal" => "Reverso de desembolso",
        "RateReset" => "Reajuste de tasa",
        "TermsChange" => "Cambio de condiciones",
        "Charge" => "Cargo",
        "Waiver" => "Condonación",
        "CommercialAdjustment" => "Ajuste comercial",
        "WriteOff" => "Castigo",
        "PreferenceChange" => "Cambio de preferencia de abono",
        "CreditBalanceResolution" => "Resolución de saldo a favor",
        "ProvisionalCollection" => "Recaudo provisional",
        _ => v ?? "—",
    };

    public static string ClaseDeMovimiento(string? v) => v switch
    {
        "Disbursement" => "Desembolso",
        "OpeningBalance" => "Saldo inicial",
        "Accrual" => "Causación",
        "AccrualAdjustment" => "Ajuste de causación",
        "SuspenseReclassification" => "Reclasificación a suspenso",
        "PaymentAllocation" => "Aplicación de pago",
        "PaymentReallocation" => "Reaplicación de pago",
        "AdvanceReceived" => "Anticipo recibido",
        "AdvanceApplied" => "Anticipo aplicado",
        "CreditBalanceRecognized" => "Saldo a favor reconocido",
        "CreditBalanceSettled" => "Saldo a favor resuelto",
        "Charge" => "Cargo",
        "Waiver" => "Condonación",
        "CommercialAdjustment" => "Ajuste comercial",
        "WriteOff" => "Castigo",
        "Recovery" => "Recuperación",
        _ => v ?? "—",
    };

    public static string EstadoDeCuota(string? v) => v switch
    {
        "Paid" => "Pagada",
        "PastDue" => "Vencida",
        "Covered" => "Cubierta por anticipo",
        "Upcoming" => "Por vencer",
        _ => v ?? "—",
    };

    public static string Marca(string? v) => v switch
    {
        "Suspended" => "Causación suspendida",
        "UsuryCapped" => "Topado en la usura",
        "Restructured" => "Reestructurado",
        "ValidationFailed" => "Validación fallida",
        _ => v ?? "—",
    };

    /// <summary>La causa de un bloqueo del deudor (<c>DebtorBlockReason</c> por su nombre).</summary>
    public static string CausaDeBloqueo(string? v) => v switch
    {
        "Manual" => "Manual",
        "Arrears" => "Mora",
        "ValidationFailed" => "Validación fallida",
        "WrittenOff" => "Castigo",
        "RestrictiveListMatch" => "Coincidencia en listas",
        "AssociateWithdrawal" => "Retiro del asociado",
        "Deceased" => "Fallecimiento",
        "Insolvency" => "Insolvencia",
        "Garnishment" => "Embargo",
        "LimitRevalidation" => "Cupo por revalidar",
        _ => v ?? "—",
    };

    public static string ClaseDeTasa(string? v) => v switch { "Fixed" => "Fija", "Variable" => "Variable", _ => v ?? "—" };

    public static string CausaDelPlan(string? v) => v switch
    {
        "Origination" => "Desembolso",
        "CommercialSale" => "Venta a crédito",
        "Migration" => "Saldo inicial",
        "RateReset" => "Reajuste de tasa",
        "Restructuring" => "Reestructuración",
        "Modification" => "Modificación",
        "Extension" => "Prórroga",
        "PaymentDateChange" => "Cambio de fecha de pago",
        "RateChange" => "Cambio de tasa",
        "Novation" => "Novación",
        _ => v ?? "—",
    };

    public static string EstadoDeMensaje(string? v) => v switch
    {
        "Pending" => "Pendiente",
        "InBatch" => "En lote",
        "Delivered" => "Entregado",
        "Rejected" => "Rechazado",
        "NotApplicable" => "No aplica",
        _ => v ?? "—",
    };

    // ------------------------------------------------------------------------------------------------- corridas --

    public static string ClaseDeCorrida(string? v) => v switch
    {
        "DailyCycle" => "Ciclo diario",
        "MonthlyClose" => "Cierre mensual",
        "CloseReopen" => "Reapertura de cierre",
        "NationalValueCorrection" => "Corrección por valor nacional",
        "ScheduledTask" => "Tarea diaria",
        _ => v ?? "—",
    };

    public static IReadOnlyList<(string Valor, string Texto)> EstadosDeCorrida { get; } =
    [
        ("Requested", "Pedida"), ("Running", "En curso"), ("Completed", "Terminada"), ("CompletedWithErrors", "Terminada con créditos sin causar"),
        ("Failed", "Fallida"),
    ];

    public static string EstadoDeCorrida(string? v) => EstadosDeCorrida.FirstOrDefault(e => e.Valor == v).Texto ?? v ?? "—";

    public static string PasoDelCiclo(string? v) => v switch
    {
        "Replica" => "Valores nacionales",
        "Reajuste" => "Reajuste de tasa",
        "Causacion" => "Causación",
        "Suspension" => "Suspensión",
        "Anticipos" => "Anticipos",
        "Bloqueos" => "Bloqueos",
        "Cobranza" => "Cobranza",
        "Verificacion" => "Verificación del libro",
        _ => v ?? "—",
    };

    public static string QuienPidio(CorridaEnListaDto c) => c.RequestedBy.Kind == "Person" ? c.RequestedBy.Name ?? "Una persona" : "Proceso automático";
}
