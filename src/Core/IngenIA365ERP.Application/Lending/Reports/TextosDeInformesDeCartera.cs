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
        // 2026-11-16 (pedido del dueño): el interés de la cuota en curso que se cobra completo al cancelar antes de su vencimiento.
        LoanTransactionKind.UnaccruedInstallmentInterest => "Interés de la cuota en curso al cancelar",
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

    // 2026-11-16 (pedido del dueño): los textos de las secciones del estado de cuenta de la persona, los mismos que pintan las pantallas de Cartera
    // (TextosDeCobranza, TextosDeOriginacion, TextosDeNovedades y TextosDeLibranza de Shared).

    public static string CanalDeGestion(CollectionActionChannel canal) => canal switch
    {
        CollectionActionChannel.Phone => "Llamada",
        CollectionActionChannel.InPerson => "En persona",
        CollectionActionChannel.Email => "Correo",
        CollectionActionChannel.Sms => "SMS",
        CollectionActionChannel.WhatsApp => "WhatsApp",
        CollectionActionChannel.Letter => "Carta",
        CollectionActionChannel.Visit => "Visita",
        CollectionActionChannel.System => "Sistema",
        _ => canal.ToString(),
    };

    public static string ResultadoDeGestion(CollectionActionResult resultado) => resultado switch
    {
        CollectionActionResult.NoAnswer => "No contestó",
        CollectionActionResult.WrongNumber => "Número equivocado",
        CollectionActionResult.ContactedNoCommitment => "Contactado, sin compromiso",
        CollectionActionResult.PromiseToPay => "Promesa de pago",
        CollectionActionResult.Refused => "Se negó a pagar",
        CollectionActionResult.RequestsAgreement => "Pide un acuerdo de pago",
        CollectionActionResult.ReportsDeceased => "Informa que el deudor falleció",
        CollectionActionResult.Other => "Otro",
        CollectionActionResult.LeftCollection => "Salió de cobranza (al día)",
        _ => resultado.ToString(),
    };

    public static string RolDeContacto(ContactRole rol) => rol switch
    {
        ContactRole.Debtor => "Deudor",
        ContactRole.CoDebtor => "Codeudor",
        ContactRole.Paymaster => "Pagaduría",
        _ => rol.ToString(),
    };

    /// <summary>El estado de una promesa de pago; la de una gestión anulada sale anulada.</summary>
    public static string EstadoDePromesa(PaymentPromiseStatus? estado, bool anulada) => anulada
        ? "Anulada"
        : estado switch
        {
            PaymentPromiseStatus.Kept => "Cumplida",
            PaymentPromiseStatus.Broken => "Incumplida",
            _ => "Pendiente",
        };

    public static string EstadoDeAvisoPrevio(PriorNoticeStatus estado) => estado switch
    {
        PriorNoticeStatus.Pending => "Pendiente",
        PriorNoticeStatus.InProgress => "En curso",
        PriorNoticeStatus.Valid => "Válido para reportar",
        PriorNoticeStatus.Closed => "Cerrado",
        _ => estado.ToString(),
    };

    public static string EstadoDeSolicitud(CreditApplicationStatus estado) => estado switch
    {
        CreditApplicationStatus.Draft => "Borrador",
        CreditApplicationStatus.Submitted => "Radicada",
        CreditApplicationStatus.PendingDecision => "Pendiente de decisión",
        CreditApplicationStatus.Returned => "Devuelta",
        CreditApplicationStatus.Approved => "Aprobada",
        CreditApplicationStatus.Denied => "Negada",
        CreditApplicationStatus.Disbursed => "Desembolsada",
        CreditApplicationStatus.Granted => "Cupo otorgado",
        CreditApplicationStatus.Cancelled => "Anulada",
        _ => estado.ToString(),
    };

    public static string MecanismoDeNovedad(NoveltyMechanism mecanismo) => mecanismo switch
    {
        NoveltyMechanism.Extension => "Prórroga (más plazo)",
        NoveltyMechanism.PaymentDateChange => "Cambio de fecha de pago",
        NoveltyMechanism.RateChange => "Cambio de tasa",
        NoveltyMechanism.Novation => "Novación",
        NoveltyMechanism.Refinancing => "Refinanciación",
        NoveltyMechanism.BranchChange => "Cambio de oficina",
        _ => mecanismo.ToString(),
    };

    public static string CausaDeNovedad(NoveltyCause causa) => causa switch
    {
        NoveltyCause.Restructuring => "Reestructuración",
        NoveltyCause.Modification => "Modificación",
        _ => causa.ToString(),
    };

    public static string EstadoDeNovedad(LoanNoveltyStatus estado) => estado switch
    {
        LoanNoveltyStatus.Draft => "Borrador",
        LoanNoveltyStatus.Submitted => "Radicada",
        LoanNoveltyStatus.Approved => "Aprobada",
        LoanNoveltyStatus.Rejected => "Negada",
        LoanNoveltyStatus.Applied => "Aplicada",
        LoanNoveltyStatus.Cancelled => "Anulada",
        _ => estado.ToString(),
    };

    /// <summary>De dónde salió un no aplicado (<c>NonDeductionOrigin</c>, por su nombre como viaja en el estado de cuenta del crédito).</summary>
    public static string OrigenDeNoAplicado(string origen) => origen switch
    {
        nameof(NonDeductionOrigin.Contract) => "Nómina propia",
        nameof(NonDeductionOrigin.Activation) => "Puesta en marcha",
        nameof(NonDeductionOrigin.PaymasterResult) => "Respuesta de la pagaduría",
        _ => origen,
    };

    public static string ClaseDeCupo(CreditLimitKind clase) => clase switch
    {
        CreditLimitKind.Consolidated => "Consolidado",
        CreditLimitKind.ByLine => "Por línea",
        CreditLimitKind.Revolving => "Rotativo",
        _ => clase.ToString(),
    };

    public static string OrigenDeCupo(CreditLimitOrigin origen) => origen switch
    {
        CreditLimitOrigin.Calculated => "Calculado por aportes",
        CreditLimitOrigin.Assigned => "Asignado",
        CreditLimitOrigin.Migration => "Migrado",
        _ => origen.ToString(),
    };

    /// <summary>El estado calculado de un cupo (<c>EstadosDeCupo</c>).</summary>
    public static string EstadoDeCupo(string estado) => estado switch
    {
        "PendingDecision" => "Espera decisión",
        "Active" => "Vigente",
        "Blocked" => "Bloqueado",
        "Ended" => "Terminado",
        _ => estado,
    };

    public static string ClaseDeBloqueo(DebtorBlockReason clase) => clase switch
    {
        DebtorBlockReason.Manual => "Manual",
        DebtorBlockReason.Arrears => "Mora",
        DebtorBlockReason.ValidationFailed => "Validación fallida",
        DebtorBlockReason.WrittenOff => "Castigo",
        DebtorBlockReason.RestrictiveListMatch => "Coincidencia en listas",
        DebtorBlockReason.AssociateWithdrawal => "Retiro del asociado",
        DebtorBlockReason.Deceased => "Fallecimiento",
        DebtorBlockReason.Insolvency => "Insolvencia",
        DebtorBlockReason.Garnishment => "Embargo",
        DebtorBlockReason.LimitRevalidation => "Cupo por revalidar",
        _ => clase.ToString(),
    };

    /// <summary>La calificación como se lee: la categoría y, si la tiene, la subcategoría («A», «A · AA»).</summary>
    public static string Calificacion(RatingCategory categoria, RatingSubcategory subcategoria) =>
        subcategoria == RatingSubcategory.None ? categoria.ToString() : $"{categoria} · {subcategoria}";
}
