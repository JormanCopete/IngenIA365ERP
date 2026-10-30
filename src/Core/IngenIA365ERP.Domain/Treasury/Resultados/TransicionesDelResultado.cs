using IngenIA365ERP.Domain.Enums.Treasury;

namespace IngenIA365ERP.Domain.Treasury.Resultados;

/// <summary>Lo que dice la tabla de transiciones de un resultado pedido: si lo admite, el estado al que lleva la orden o el código del rechazo. (nuevo)</summary>
public sealed record TransicionDelResultado(bool Admite, PaymentOrderState? Hacia, string? Codigo)
{
    public static TransicionDelResultado A(PaymentOrderState estado) => new(true, estado, null);

    public static TransicionDelResultado No(string codigo) => new(false, null, codigo);
}

/// <summary>
/// Las transiciones fijas del resultado del giro (feature 015, US11, T0156; R-18; FR-215; data-model §19.9), puras:
/// <list type="bullet">
/// <item>una orden enviada por transferencia o pago a tercero admite «girada» o «rechazada»;</item>
/// <item>una enviada en cheque sólo admite «girada» (al entregarlo con constancia);</item>
/// <item>un cheque ya girado sólo admite «cheque no cobrado»; una transferencia girada no admite nada (el abono devuelto entra por Recaudo, FR-199);</item>
/// <item>«anulada» sólo responde a una anulación del origen sobre una orden recibida, pendiente, lista, en lote o aprobada sin enviar;</item>
/// <item>antes de enviar y sin anulación, nada (<c>Treasury.Result.NotSent</c>), salvo la orden que paga un archivo de Cartera, que admite «girada» o «rechazada»
/// estando lista (FR-108);</item>
/// <item>lo demás, <c>Treasury.Result.InvalidTransition</c>.</item>
/// </list>
/// Ningún proceso registra un resultado; repetir el mismo no cambia nada (lo resuelve quien llama con la UK <c>(PaymentOrderId, Outcome)</c>). (nuevo)
/// </summary>
public static class TransicionesDelResultado
{
    public const string CodigoNoEnviada = "Treasury.Result.NotSent";
    public const string CodigoTransicionInvalida = "Treasury.Result.InvalidTransition";

    private static readonly PaymentOrderState[] AntesDeEnviar =
        [PaymentOrderState.Received, PaymentOrderState.Pending, PaymentOrderState.Ready, PaymentOrderState.InBatch, PaymentOrderState.Approved];

    public static TransicionDelResultado Evaluar(PaymentOrderState estado, PaymentForm forma, bool pagadaPorArchivoDeCartera, bool anulacionDelOrigen,
        DisbursementOutcome resultado)
    {
        if (resultado == DisbursementOutcome.Voided)
            return anulacionDelOrigen && AntesDeEnviar.Contains(estado) ? TransicionDelResultado.A(PaymentOrderState.Voided) : TransicionDelResultado.No(CodigoTransicionInvalida);

        if (pagadaPorArchivoDeCartera && estado == PaymentOrderState.Ready)
            return resultado switch
            {
                DisbursementOutcome.Disbursed => TransicionDelResultado.A(PaymentOrderState.Disbursed),
                DisbursementOutcome.Rejected => TransicionDelResultado.A(PaymentOrderState.Rejected),
                _ => TransicionDelResultado.No(CodigoTransicionInvalida),
            };

        if (AntesDeEnviar.Contains(estado)) return TransicionDelResultado.No(CodigoNoEnviada);

        return (estado, forma, resultado) switch
        {
            (PaymentOrderState.Sent, PaymentForm.Check, DisbursementOutcome.Disbursed) => TransicionDelResultado.A(PaymentOrderState.Disbursed),
            (PaymentOrderState.Sent, PaymentForm.Check, _) => TransicionDelResultado.No(CodigoTransicionInvalida),
            (PaymentOrderState.Sent, _, DisbursementOutcome.Disbursed) => TransicionDelResultado.A(PaymentOrderState.Disbursed),
            (PaymentOrderState.Sent, _, DisbursementOutcome.Rejected) => TransicionDelResultado.A(PaymentOrderState.Rejected),
            (PaymentOrderState.Disbursed, PaymentForm.Check, DisbursementOutcome.CheckNotCashed) => TransicionDelResultado.A(PaymentOrderState.CheckNotCashed),
            _ => TransicionDelResultado.No(CodigoTransicionInvalida),
        };
    }
}
