using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;

/// <summary>
/// <c>LND_NegotiatedRateRequests</c> (feature 014, K9, US6, T171; api.md §6; FR-127; research E134, <b>nueva en research</b>: data-model §2.4 no la nombra):
/// la solicitud de tasa negociada de un CDAT (o de una cuenta cuya línea la admite) cuando el margen pedido cabe en la regla pero <b>supera la atribución</b> de
/// quien lo pide. Guarda lo que se evaluó —la tasa de la tabla, el margen pedido, el máximo de la regla y la regla—, quién la pidió y quién la decidió y cómo
/// (aprobador presente con segundo factor o punto de acta). Documento con estado, no hecho: pasa de «en espera» a aprobada, negada, retirada o vencida, una
/// vez. Lo aprobado se sella en la versión de condiciones al fondear. (nuevo)
/// </summary>
public class NegotiatedRateRequest : AuditableEntity
{
    public const int LargoDelMotivo = 500;

    public int MemberAccountId { get; set; }

    public NegotiatedRateRequestStatus Status { get; set; } = NegotiatedRateRequestStatus.PendingDecision;

    /// <summary>La versión de la regla que se aplicó al evaluar.</summary>
    public int NegotiatedRateRuleId { get; set; }

    public decimal TableAnnualRate { get; set; }

    public decimal RequestedSpread { get; set; }

    /// <summary>El máximo de la regla al pedirlo.</summary>
    public decimal MaxSpread { get; set; }

    /// <summary>El monto del CDAT (o de la cuenta) que se evaluó.</summary>
    public decimal Amount { get; set; }

    public int? TermDays { get; set; }

    public string? Channel { get; set; }

    public string Reason { get; set; } = string.Empty;

    public int RequestedByUserId { get; set; }

    public DateTime RequestedAt { get; set; }

    public int? DecidedByUserId { get; set; }

    public DateTime? DecidedAt { get; set; }

    public string? DecisionReason { get; set; }

    /// <summary>La aprobación de la plataforma con el aprobador presente (segundo factor), si la decidió una persona.</summary>
    public Guid? ApprovalPublicId { get; set; }

    /// <summary>El punto de acta cerrado, si la decidió un órgano.</summary>
    public Guid? SessionItemPublicId { get; set; }

    /// <summary>¿Sigue en espera?</summary>
    public bool EnEspera => Status == NegotiatedRateRequestStatus.PendingDecision;

    /// <summary>Aprueba la solicitud: una vez.</summary>
    public void Aprobar(int usuario, DateTime instante, string motivo, Guid? aprobacion, Guid? puntoDeActa)
    {
        Decidir(NegotiatedRateRequestStatus.Approved, usuario, instante, motivo);
        ApprovalPublicId = aprobacion;
        SessionItemPublicId = puntoDeActa;
    }

    /// <summary>Niega la solicitud: una vez, con motivo.</summary>
    public void Negar(int usuario, DateTime instante, string motivo, Guid? puntoDeActa) =>
        Decidir(NegotiatedRateRequestStatus.Rejected, usuario, instante, motivo, puntoDeActa);

    /// <summary>La retira quien la pidió (o el titular desiste): una vez.</summary>
    public void Retirar(int usuario, DateTime instante, string motivo) => Decidir(NegotiatedRateRequestStatus.Withdrawn, usuario, instante, motivo);

    /// <summary>La vence el sistema: el CDAT se desistió o se fondeó con la tasa de la tabla sin decidirla.</summary>
    public void Vencer(DateTime instante, string motivo)
    {
        if (!EnEspera) return;
        Status = NegotiatedRateRequestStatus.Expired;
        DecidedAt = instante;
        DecisionReason = Recortar(motivo);
    }

    private void Decidir(NegotiatedRateRequestStatus estado, int usuario, DateTime instante, string motivo, Guid? puntoDeActa = null)
    {
        if (!EnEspera) throw new InvalidOperationException($"La solicitud está «{Status}»: sólo se decide una en espera.");
        Status = estado;
        DecidedByUserId = usuario;
        DecidedAt = instante;
        DecisionReason = Recortar(motivo);
        SessionItemPublicId ??= puntoDeActa;
    }

    public static string Recortar(string texto)
    {
        var t = (texto ?? string.Empty).Trim();
        return t.Length <= LargoDelMotivo ? t : t[..LargoDelMotivo];
    }
}
