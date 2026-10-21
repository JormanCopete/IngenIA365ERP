using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// <c>COR_AssociateInboundMessages</c> (feature 014, US10, T098; contracts/mensajes.md §5.1 y §8, research C16; enmienda E-04): el recibo de Asociados de
/// un <c>RetiroDeAsociadoAprobado</c> de la Cartera. Una fila por mensaje procesado (único por <c>MessagePublicId</c>): repetir el mismo mensaje responde
/// lo ya procesado sin un segundo efecto. Guarda qué asociado retiró, con qué fecha y causa y de qué trámite; sin FK a <c>LND_</c>: el trámite es un
/// <c>PublicId</c> y un número (FR-008). Hecho de sólo inserción. (nuevo)
/// </summary>
public class AssociateInboundMessage : AuditableEntity, IHechoInmutable
{
    public Guid MessagePublicId { get; init; }

    public string MessageType { get; init; } = string.Empty;

    public short MessageVersion { get; init; }

    public int AssociateId { get; init; }

    /// <summary>El trámite de retiro de la Cartera (<c>LND_MembershipWithdrawals.PublicId</c>).</summary>
    public Guid WithdrawalPublicId { get; init; }

    public string WithdrawalNumber { get; init; } = string.Empty;

    /// <summary>La fecha del retiro que quedó en el asociado.</summary>
    public DateOnly EffectiveDate { get; init; }

    /// <summary>Valor de <c>MembershipWithdrawalCause</c> por nombre.</summary>
    public string Cause { get; init; } = string.Empty;

    /// <summary>Verdadero si el asociado ya estaba retirado con la misma fecha (no cambió nada).</summary>
    public bool AlreadyWithdrawn { get; init; }

    public DateTime ReceivedAt { get; init; }
}
