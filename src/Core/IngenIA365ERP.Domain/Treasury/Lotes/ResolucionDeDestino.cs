using IngenIA365ERP.Domain.Enums.Treasury;

namespace IngenIA365ERP.Domain.Treasury.Lotes;

/// <summary>Cómo resuelve Tesorería una orden que llegó sin cuenta destino (D-a, decisión del dueño del 2026-10-27; FR-229). (nuevo)</summary>
public enum ResolucionDeDestinoPedida
{
    /// <summary>Completar la cuenta destino con una cuenta registrada y activa del beneficiario.</summary>
    CompletarCuenta = 1,

    /// <summary>Girarla por cheque, por el flujo de cheques de siempre (lote de forma «cheques»).</summary>
    PagarConCheque = 2,
}

/// <summary>
/// Cuándo puede Tesorería resolver ella misma una orden de transferencia que llegó sin cuenta destino (<c>Treasury.Order.DestinationMissing</c>), pura (feature 015,
/// D-a, decisión del dueño del 2026-10-27; FR-229). La orden tiene que estar recibida, pendiente o lista (la validación todavía la puede mover), no la paga un archivo
/// de Cartera, el origen no pidió su anulación, no es un cheque ya y su último motivo de pendiente incluye <c>DestinationMissing</c>. Cualquier origen (Captaciones,
/// Cartera, Recaudo, manual) se resuelve igual. Devuelve el código del error, o nulo si se puede. (nuevo)
/// </summary>
public static class ResolucionDeDestino
{
    public const string MotivoSinCuentaDestino = "Treasury.Order.DestinationMissing";

    public const string NoResoluble = "Treasury.Order.DestinationNotResolvable";
    public const string NoLeFaltaDestino = "Treasury.Order.DestinationNotMissing";

    public static string? Evaluar(PaymentOrderState estado, PaymentForm forma, bool pagadaPorArchivoDeCartera, bool anulacionPedida, IReadOnlyCollection<string> motivos)
    {
        ArgumentNullException.ThrowIfNull(motivos);
        if (estado is not (PaymentOrderState.Received or PaymentOrderState.Pending or PaymentOrderState.Ready) || pagadaPorArchivoDeCartera || anulacionPedida)
            return NoResoluble;
        if (forma == PaymentForm.Check || !motivos.Contains(MotivoSinCuentaDestino)) return NoLeFaltaDestino;
        return null;
    }
}
