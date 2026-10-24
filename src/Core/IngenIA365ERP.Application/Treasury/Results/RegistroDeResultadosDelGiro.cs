using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Treasury;

namespace IngenIA365ERP.Application.Treasury.Results;

/// <summary>
/// El único que registra el resultado del giro de una orden de pago (feature 015, US10 y US11, T0140, T0158; FR-115, FR-118, FR-215). En US10 sólo deja
/// <see cref="PaymentOrderState.Voided"/> la orden que su origen anuló en la bandeja o en un lote no enviado; US11 le suma el registro del resultado
/// (<c>TES_DisbursementResults</c>), las transiciones fijas (<c>TransicionesDelResultado</c>) y la emisión de <c>ResultadoDelGiro</c> v1 a nombre de la persona.
/// Escribe en la unidad de trabajo de quien llama, sin guardar. (nuevo)
/// </summary>
public sealed partial class RegistroDeResultadosDelGiro(IApplicationDbContext db, IDateTimeService reloj)
{
    /// <summary>
    /// La anulación del origen sobre una orden recibida, pendiente, lista o en un lote no enviado (mensajes.md §5.2): la orden queda anulada. El resultado
    /// «anulada» responde a la anulación de una persona del origen y la nombra a ella (<paramref name="quien"/>), nunca a un proceso (FR-215).
    /// </summary>
    public Task AnuladaPorElOrigenAsync(PaymentOrder orden, UserRefV1 quien, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(orden);
        ArgumentNullException.ThrowIfNull(quien);
        orden.Status = PaymentOrderState.Voided;
        _ = db;
        _ = reloj;
        _ = ct;
        return Task.CompletedTask;
    }
}
