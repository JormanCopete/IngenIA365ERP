using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;

/// <summary>
/// <c>LND_ContributionRequests</c> (feature 014, US3, T086; research E58; api.md §7; FR-042, FR-043, FR-044, FR-048, FR-080): la solicitud de
/// <b>devolución de aportes sin retiro</b> (el exceso sobre el límite de participación u otra causa decidida) o de <b>cesión</b> de aportes a otro asociado,
/// que una persona radica y otra aprueba (segregación radicar × decidir). Al aprobarse nace la operación que mueve el dinero —la devolución con su orden
/// de giro a Tesorería, la cesión como traslado entre las dos cuentas de aportes— y la solicitud la nombra. Documento con estado, no hecho: sólo pasa
/// de «en espera» a aprobada o negada, una vez. (nuevo)
/// </summary>
public class ContributionRequest : AuditableEntity
{
    public const int LargoDelMotivo = 500;

    public ContributionRequestKind Kind { get; set; }

    public ContributionRequestStatus Status { get; set; } = ContributionRequestStatus.PendingApproval;

    /// <summary>La cuenta de aportes de la que salen los aportes.</summary>
    public int MemberAccountId { get; set; }

    /// <summary>Cesión: el asociado que los recibe.</summary>
    public int? TargetPersonId { get; set; }

    /// <summary>Cesión: la cuenta de aportes viva del que los recibe, resuelta al aprobar.</summary>
    public int? TargetMemberAccountId { get; set; }

    public decimal Amount { get; set; }

    /// <summary>Devolución: la causa.</summary>
    public ContributionRefundCause? RefundCause { get; set; }

    /// <summary>El asunto del acta que la decidió, si lo hay (consejo o junta).</summary>
    public Guid? SessionItemPublicId { get; set; }

    public string Reason { get; set; } = string.Empty;

    public int RequestedByUserId { get; set; }

    public DateTime RequestedAt { get; set; }

    public int? DecidedByUserId { get; set; }

    public DateTime? DecidedAt { get; set; }

    public string? DecisionReason { get; set; }

    /// <summary>La operación que movió los aportes al aprobarse.</summary>
    public long? OperationId { get; set; }

    /// <summary>Devolución: la orden de giro a Tesorería.</summary>
    public int? PayoutOrderId { get; set; }

    /// <summary>Aprueba la solicitud con la operación (y la orden) que nacieron: una vez.</summary>
    public void Aprobar(int usuario, DateTime instante, string motivo, long operacion, int? orden)
    {
        if (Status != ContributionRequestStatus.PendingApproval)
            throw new InvalidOperationException($"La solicitud está «{Status}»: sólo se aprueba una en espera.");
        Status = ContributionRequestStatus.Approved;
        DecidedByUserId = usuario;
        DecidedAt = instante;
        DecisionReason = Recortar(motivo);
        OperationId = operacion;
        PayoutOrderId = orden;
    }

    /// <summary>Niega la solicitud: una vez, con motivo.</summary>
    public void Negar(int usuario, DateTime instante, string motivo)
    {
        if (Status != ContributionRequestStatus.PendingApproval)
            throw new InvalidOperationException($"La solicitud está «{Status}»: sólo se niega una en espera.");
        Status = ContributionRequestStatus.Rejected;
        DecidedByUserId = usuario;
        DecidedAt = instante;
        DecisionReason = Recortar(motivo);
    }

    public static string Recortar(string texto)
    {
        var t = (texto ?? string.Empty).Trim();
        return t.Length <= LargoDelMotivo ? t : t[..LargoDelMotivo];
    }
}
