using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Origination;

/// <summary>
/// <c>LND_PrepaymentPreferences</c> (feature 013; data-model §2.14; FR-035, FR-049, FR-075, D24): preferencia del deudor para imputar
/// abonos extraordinarios a capital, de un crédito o de un cupo rotativo (exactamente uno, <c>CK_LND_PrepaymentPreferences_Scope</c>).
/// Hecho versionado de sólo inserción (<see cref="IHechoInmutable"/>): rige desde que se registra y se lee la última con
/// <c>RegisteredAt ≤ R</c> (T24).
/// </summary>
public class PrepaymentPreference : AuditableEntity, IHechoInmutable
{
    public int PersonId { get; init; }

    public int? LoanId { get; init; }

    public int? CreditLimitId { get; init; }

    /// <summary>Nulo = revocada: un abono sin interacción queda como saldo a favor (FR-075).</summary>
    public PrepaymentChoice? Choice { get; init; }

    public int VersionNumber { get; init; }

    public DateTime RegisteredAt { get; init; }

    public PreferenceOrigin Origin { get; init; }

    public int? ApplicationId { get; init; }

    /// <summary>El cambio en un crédito es una operación <c>PreferenceChange</c>.</summary>
    public long? OperationId { get; init; }

    /// <summary>Constancia de la instrucción (adjunto, sin FK).</summary>
    public Guid? EvidenceAttachmentPublicId { get; init; }

    public int? RecordedByUserId { get; init; }
}
