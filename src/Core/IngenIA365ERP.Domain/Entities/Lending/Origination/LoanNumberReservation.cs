using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Origination;

/// <summary>
/// <c>LND_LoanNumberReservations</c> (feature 013; data-model §2.11; FR-057, FR-058, D3, D26, T21, T22): reserva del número de un
/// crédito ligada al pago de una venta de Comercial; lleva el valor y descuenta cupo mientras está <c>Reserved</c>. La escribe
/// <c>IReservasDeCartera</c> dentro de la unidad de trabajo de Comercial. El número nunca se reasigna. No es hecho.
/// </summary>
public class LoanNumberReservation : AuditableEntity
{
    public string Number { get; set; } = string.Empty;

    public int PersonId { get; set; }

    /// <summary>Descuenta cupo mientras la reserva está <c>Reserved</c>.</summary>
    public decimal Amount { get; set; }

    /// <summary>La línea pedida.</summary>
    public int? CreditLineId { get; set; }

    /// <summary>El cupo que consumió.</summary>
    public int? CreditLimitId { get; set; }

    /// <summary><c>INV:{paymentPublicId:N}:{monto}</c> (T21); única.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>Huella de la petición (<c>HuellaDeOperacion.Calcular</c>): misma clave con otra huella es <c>KeyReused</c>.</summary>
    public string RequestSha256 { get; set; } = string.Empty;

    /// <summary>Documento de venta de Comercial, como dato (sin FK).</summary>
    public Guid SalesDocumentPublicId { get; set; }

    /// <summary>Pago de la venta de Comercial, como dato (sin FK).</summary>
    public Guid PaymentPublicId { get; set; }

    /// <summary>El <c>EvidenceId</c> de la consulta de cupo que precedió a la reserva (C31).</summary>
    public string? ConsultationEvidence { get; set; }

    public LoanNumberReservationStatus Status { get; set; } = LoanNumberReservationStatus.Reserved;

    public DateTime ReservedAt { get; set; }

    /// <summary>El usuario de Comercial que confirma.</summary>
    public int ReservedByUserId { get; set; }

    /// <summary>Fecha de reserva + <c>Reservas.DiasDeVigencia</c>.</summary>
    public DateOnly ExpiresOn { get; set; }

    public DateTime? UsedAt { get; set; }

    public int? LoanId { get; set; }

    public DateTime? ReleasedAt { get; set; }

    public int? ReleasedByUserId { get; set; }

    public string? ReleaseReason { get; set; }

    public DateTime? ExpiredAt { get; set; }
}
