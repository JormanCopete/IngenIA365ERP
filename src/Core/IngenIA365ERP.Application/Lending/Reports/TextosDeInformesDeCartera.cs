using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Reports;

/// <summary>
/// Los textos en español con que las vistas del centro de informes de Cartera nombran las clases del libro (feature 013, US15, T0797): el
/// archivo exportado lo lee una persona, no un programa, así que no lleva los nombres de los enums. (nuevo)
/// </summary>
public static class TextosDeInformesDeCartera
{
    public static string Operacion(LoanOperationKind clase) => clase switch
    {
        LoanOperationKind.Disbursement => "Desembolso",
        LoanOperationKind.OpeningBalance => "Saldo inicial",
        LoanOperationKind.Payment => "Pago",
        LoanOperationKind.PaymentReversal => "Reverso de pago",
        LoanOperationKind.DisbursementReversal => "Reverso de desembolso",
        LoanOperationKind.RateReset => "Reajuste de tasa",
        LoanOperationKind.TermsChange => "Cambio de condiciones",
        LoanOperationKind.Charge => "Cargo",
        LoanOperationKind.Waiver => "Condonación",
        LoanOperationKind.CommercialAdjustment => "Ajuste de Comercial",
        LoanOperationKind.WriteOff => "Castigo",
        LoanOperationKind.PreferenceChange => "Cambio de preferencia",
        LoanOperationKind.CreditBalanceResolution => "Resolución de saldo a favor",
        LoanOperationKind.ProvisionalCollection => "Cobro provisional",
        _ => clase.ToString(),
    };

    public static string Transaccion(LoanTransactionKind clase) => clase switch
    {
        LoanTransactionKind.Disbursement => "Desembolso",
        LoanTransactionKind.OpeningBalance => "Saldo inicial",
        LoanTransactionKind.Accrual => "Causación",
        LoanTransactionKind.AccrualAdjustment => "Ajuste de causación",
        LoanTransactionKind.SuspenseReclassification => "Reclasificación a suspenso",
        LoanTransactionKind.PaymentAllocation => "Aplicación de pago",
        LoanTransactionKind.PaymentReallocation => "Reaplicación de pago",
        LoanTransactionKind.AdvanceReceived => "Anticipo recibido",
        LoanTransactionKind.AdvanceApplied => "Anticipo aplicado",
        LoanTransactionKind.CreditBalanceRecognized => "Saldo a favor reconocido",
        LoanTransactionKind.CreditBalanceSettled => "Saldo a favor resuelto",
        LoanTransactionKind.Charge => "Cargo",
        LoanTransactionKind.Waiver => "Condonación",
        LoanTransactionKind.CommercialAdjustment => "Ajuste de Comercial",
        LoanTransactionKind.WriteOff => "Castigo",
        LoanTransactionKind.Recovery => "Recuperación",
        _ => clase.ToString(),
    };

    public static string Concepto(LoanConcept concepto) => concepto switch
    {
        LoanConcept.Principal => "Capital",
        LoanConcept.CurrentInterest => "Interés corriente",
        LoanConcept.DefaultInterest => "Interés de mora",
        LoanConcept.CurrentInterestInSuspense => "Interés corriente en suspenso",
        LoanConcept.DefaultInterestInSuspense => "Interés de mora en suspenso",
        LoanConcept.Insurance => "Seguro",
        LoanConcept.CollectionFee => "Honorarios de cobranza",
        LoanConcept.OtherCharge => "Otro cargo",
        LoanConcept.Advance => "Anticipo",
        LoanConcept.CreditBalance => "Saldo a favor",
        _ => concepto.ToString(),
    };

    public static string Modalidad(PaymentModality? modalidad) => modalidad switch
    {
        PaymentModality.FullPayoff => "Cancelación total",
        PaymentModality.PastDue => "Lo vencido",
        PaymentModality.CycleInstallment => "Cuota del ciclo",
        PaymentModality.Advance => "Anticipo",
        PaymentModality.ExtraordinaryPrincipal => "Abono extraordinario a capital",
        PaymentModality.SpecificInstallment => "Cuota específica",
        PaymentModality.FreeAmount => "Valor libre",
        null => "Sin modalidad",
        _ => modalidad.ToString()!,
    };

    public static string Canal(MessageChannel canal) => canal switch
    {
        MessageChannel.Email => "Correo",
        MessageChannel.Sms => "SMS",
        MessageChannel.WhatsApp => "WhatsApp",
        MessageChannel.Letter => "Carta",
        _ => canal.ToString(),
    };

    public static string Proposito(DebtorMessagePurpose proposito) => proposito switch
    {
        DebtorMessagePurpose.Collection => "Cobranza",
        DebtorMessagePurpose.PriorNotice => "Aviso previo",
        DebtorMessagePurpose.Informative => "Informativo",
        DebtorMessagePurpose.DocumentDelivery => "Envío de documento",
        DebtorMessagePurpose.CollectionFeeNotice => "Aviso de honorarios de cobranza",
        _ => proposito.ToString(),
    };

    public static string EstadoDeMensaje(DebtorMessageEventKind estado) => estado switch
    {
        DebtorMessageEventKind.Queued => "En cola",
        DebtorMessageEventKind.Deferred => "Diferido",
        DebtorMessageEventKind.Excluded => "Excluido",
        DebtorMessageEventKind.Sent => "Enviado",
        DebtorMessageEventKind.Delivered => "Entregado",
        DebtorMessageEventKind.Failed => "Fallido",
        DebtorMessageEventKind.Simulated => "Simulado",
        DebtorMessageEventKind.Cancelled => "Cancelado",
        _ => estado.ToString(),
    };

    public static string Clasificacion(SaleReconciliationOutcome clasificacion) => clasificacion switch
    {
        SaleReconciliationOutcome.Migrated => "Migrada",
        SaleReconciliationOutcome.CancelledBeforeCutoff => "Cancelada antes del corte",
        SaleReconciliationOutcome.NotMigratedWithBalance => "No migrada con saldo",
        SaleReconciliationOutcome.OutsideLendingD29 => "Fuera de Cartera",
        _ => clasificacion.ToString(),
    };

    /// <summary>La calificación como se lee: la categoría y, si la tiene, la subcategoría («A», «A · AA»).</summary>
    public static string Calificacion(RatingCategory categoria, RatingSubcategory subcategoria) =>
        subcategoria == RatingSubcategory.None ? categoria.ToString() : $"{categoria} · {subcategoria}";
}
