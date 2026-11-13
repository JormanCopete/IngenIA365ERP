using IngenIA365ERP.Domain.Entities.ElectronicInvoicing;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Documents;

/// <summary>El estado del documento electrónico como lo ve una persona (feature 023; contracts/api.md §1; data-model §1). (nuevo)</summary>
public enum EstadoVisible
{
    PendingSend = 1,
    Sent = 2,
    Retrying = 3,
    Validated = 4,
    ValidatedWithNotices = 5,
    Rejected = 6,
    Dead = 7,
    Contingency = 8,
    Cancelled = 9,
}

/// <summary>
/// Deriva el estado visible de la fila (feature 023): no hay un valor nuevo en <see cref="ElectronicDocumentStatus"/> —la máquina de estados no
/// cambia—; «en reintento» y «muerto» salen de <c>AttemptCount</c>, <c>TransientFailures</c> y <c>DeadAt</c>. Lo comparten el cobro, la venta y la
/// bandeja para que digan lo mismo. (nuevo)
/// </summary>
public static class EstadoVisibleDelDocumento
{
    public static EstadoVisible De(ElectronicDocument e) => De(e.Status, e.AttemptCount, e.TransientFailures, e.DeadAt);

    public static EstadoVisible De(ElectronicDocumentStatus estado, int intentos, int transitorios, DateTime? muerto) =>
        muerto is not null ? EstadoVisible.Dead
        : estado switch
        {
            ElectronicDocumentStatus.Validated => EstadoVisible.Validated,
            ElectronicDocumentStatus.ValidatedWithNotices => EstadoVisible.ValidatedWithNotices,
            ElectronicDocumentStatus.Rejected => EstadoVisible.Rejected,
            ElectronicDocumentStatus.CancelledWithoutReplacement => EstadoVisible.Cancelled,
            ElectronicDocumentStatus.IssuerContingency or ElectronicDocumentStatus.DianContingency => EstadoVisible.Contingency,
            ElectronicDocumentStatus.Pending when intentos == 0 => EstadoVisible.PendingSend,
            ElectronicDocumentStatus.Sent when transitorios == 0 => EstadoVisible.Sent,
            _ => EstadoVisible.Retrying,
        };

    /// <summary>El texto para la pantalla.</summary>
    public static string Texto(EstadoVisible estado) => estado switch
    {
        EstadoVisible.PendingSend => "Pendiente de envío",
        EstadoVisible.Sent => "Enviado",
        EstadoVisible.Retrying => "En reintento",
        EstadoVisible.Validated => "Aceptado",
        EstadoVisible.ValidatedWithNotices => "Aceptado con notificaciones",
        EstadoVisible.Rejected => "Rechazado",
        EstadoVisible.Dead => "Muerto (espera reproceso)",
        EstadoVisible.Contingency => "En contingencia",
        EstadoVisible.Cancelled => "Cancelado",
        _ => estado.ToString(),
    };
}
