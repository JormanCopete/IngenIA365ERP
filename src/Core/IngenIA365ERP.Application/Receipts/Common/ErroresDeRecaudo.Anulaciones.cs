using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Receipts.Common;

// Feature 015, US5 (T0175 a T0177; data-model §21; DT §2.7): los errores de la anulación de recibos, los reversos de los módulos y los saldos a favor, y los de la
// activación mínima (T0183b). Los que data-model §21 no nombraba son (nuevo).
public static partial class ErroresDeRecaudo
{
    public const string CodigoAnulacionYaHecha = "Receipts.Voiding.AlreadyVoided";
    public const string CodigoAnulacionAutoaprobada = "Receipts.Voiding.SelfApproval";
    public const string CodigoAnulacionRechazadaPorContrato = "Receipts.Voiding.ContractRejected";
    public const string CodigoDeNomina = "Receipts.Voiding.PayrollOwned";
    public const string CodigoAplicacionDesconocida = "Receipts.Reversal.UnknownApplication";
    public const string CodigoDeOtroRecaudador = "Receipts.Reversal.NotOurs";
    public const string CodigoRequisitosDeActivacion = "Receipts.Activation.RequirementsMissing";

    public static Error AnulacionYaHecha(string recibo) => new ErrorConDatos(CodigoAnulacionYaHecha,
        $"El recibo {recibo} ya está anulado o tiene una anulación pendiente.", new { receipt = recibo });

    public static Error AnulacionAutoaprobada() => new(CodigoAnulacionAutoaprobada,
        "Usted pidió esta anulación: la aprueba otra persona con el permiso «Aprobar la anulación de un recibo».");

    public static Error AnulacionNoPendiente(string estado) => new ErrorConDatos("Receipts.Voiding.NotPending",
        $"La anulación está «{estado}»: sólo se decide una anulación pendiente.", new { state = estado });

    public static Error AnulacionRechazadaPorContrato(IReadOnlyList<object> rechazos) => new ErrorConDatos(CodigoAnulacionRechazadaPorContrato,
        "Un módulo no aceptó el reverso de una aplicación del recibo: no se anuló nada. Revise el motivo de cada una.", new { items = rechazos });

    public static Error DeNomina() => new(CodigoDeNomina,
        "Lo que recauda Nómina se anula o reversa en Nómina, no desde Recaudo.");

    public static Error AplicacionDesconocida(string clave) => new ErrorConDatos(CodigoAplicacionDesconocida,
        "El módulo avisó el reverso de una aplicación que Recaudo no conoce.", new { idempotencyKey = clave });

    public static Error DeOtroRecaudador(string recaudador) => new ErrorConDatos(CodigoDeOtroRecaudador,
        $"El reverso es de un pago del recaudador {recaudador}, no de Recaudo.", new { collector = recaudador });

    public static Error CausaYaConfirmada() => new("Receipts.Reversal.CauseAlreadyConfirmed",
        "La causa del dinero de este reverso ya se confirmó.");

    public static Error CausaInvalida() => new("Receipts.Reversal.InvalidCause",
        "La causa es «dinero recibido» (MoneyReceived) o «dinero no recibido» (MoneyNotReceived).");

    public static Error SaldoInsuficiente(decimal saldo) => new ErrorConDatos("Receipts.CreditBalance.InsufficientBalance",
        $"El saldo a favor disponible es {saldo:N2}: no alcanza para lo pedido.", new { balance = saldo });

    public static Error SaldoResuelto() => new("Receipts.CreditBalance.Resolved", "El saldo a favor ya está resuelto.");

    public static Error DevolucionDeCarteraRechazada(string? codigo, string? mensaje) => new ErrorConDatos("Receipts.CreditBalance.LendingRefundRejected",
        mensaje ?? "Cartera no aceptó devolver el saldo a favor por el recaudador.", new { code = codigo });

    public static Error ReciboAjeno() => new("Receipts.CreditBalance.ReceiptNotOfPayer",
        "El recibo que aplica el saldo a favor no es del mismo pagador o no está vigente.");

    public static Error RequisitosDeActivacion(IReadOnlyList<string> faltantes) => new ErrorConDatos(CodigoRequisitosDeActivacion,
        "Faltan requisitos para activar Recaudo: complételos y vuelva a intentarlo.", new { missing = faltantes });

    public static Error YaActivado() => new("Receipts.Activation.AlreadyActive", "Recaudo ya está activado.");
}
