using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Operations;

/// <summary>
/// <c>LND_LoanNoveltyConsents</c> (feature 013, K3, T0871; data-model §25.3; FR-092): el consentimiento de un codeudor, avalista o propietario
/// de la garantía a una novedad. Nace <c>Pending</c> por cada parte del crédito al radicar; se acepta con la aceptación firmada (adjunto
/// <c>LendingNoveltyConsent</c>, obligatorio) o se rechaza. Ante un rechazo el aprobador decide con motivo: no aplicar la novedad o aplicarla
/// con esa garantía extinguida, con la cobertura restante y, si hizo falta, la excepción aprobada. Único por novedad, persona y garantía. No
/// es hecho. (nuevo)
/// </summary>
public class LoanNoveltyConsent : AuditableEntity
{
    public const string CodigoNoPendiente = "Lending.Novelty.ConsentNotPending";
    public const string CodigoSinAdjunto = "Lending.Novelty.ConsentAttachmentRequired";
    public const string CodigoNoRechazado = "Lending.Novelty.ConsentNotRefused";

    public int LoanNoveltyId { get; init; }

    /// <summary>Codeudor, avalista o propietario de la garantía.</summary>
    public int PersonId { get; init; }

    /// <summary>La garantía que respalda, si consiente como su propietario.</summary>
    public int? CollateralId { get; init; }

    public ConsentPartyRole PartyRole { get; init; }

    public ConsentStatus Status { get; private set; } = ConsentStatus.Pending;

    /// <summary>Aceptación firmada (<c>LendingNoveltyConsent</c>); obligatoria con <c>Accepted</c>.</summary>
    public Guid? SignedAttachmentPublicId { get; private set; }

    public int? RecordedByUserId { get; private set; }

    public DateTime? RecordedAt { get; private set; }

    /// <summary>Con <c>Refused</c>: no aplicar o aplicar con la garantía extinguida.</summary>
    public ConsentRefusalResolution? RefusalResolution { get; private set; }

    public string? ResolutionReason { get; private set; }

    /// <summary>La cobertura restante frente a la línea al extinguir la garantía.</summary>
    public string? CoverageAfterJson { get; private set; }

    /// <summary>La excepción aprobada si la cobertura restante no cumple la línea.</summary>
    public int? ExceptionDecisionId { get; private set; }

    // ------------------------------------------------------------------------------------------- comportamiento (T0871) --

    /// <summary>¿Resuelto para aplicar? Aceptado, o rechazado con su resolución.</summary>
    public bool Resuelto => Status == ConsentStatus.Accepted || (Status == ConsentStatus.Refused && RefusalResolution is not null);

    /// <summary><c>Pending</c> → <c>Accepted</c> con la aceptación firmada.</summary>
    public void Aceptar(Guid adjuntoFirmado, int usuarioId, DateTime instante)
    {
        ExigirPendiente();
        if (adjuntoFirmado == Guid.Empty)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoSinAdjunto,
                "La aceptación del codeudor o garante va con su documento firmado.", "attachmentPublicId", null));
        Status = ConsentStatus.Accepted;
        SignedAttachmentPublicId = adjuntoFirmado;
        RecordedByUserId = usuarioId;
        RecordedAt = instante;
    }

    /// <summary><c>Pending</c> → <c>Refused</c>: la parte no consiente; el aprobador resuelve después.</summary>
    public void Rehusar(int usuarioId, DateTime instante, Guid? soporte = null)
    {
        ExigirPendiente();
        Status = ConsentStatus.Refused;
        SignedAttachmentPublicId = soporte;
        RecordedByUserId = usuarioId;
        RecordedAt = instante;
    }

    /// <summary>
    /// La decisión ante un rechazo (FR-092), sólo con <c>Refused</c>: no aplicar, o aplicar con la garantía extinguida con la cobertura que
    /// queda y, si no cumple la línea, la excepción aprobada.
    /// </summary>
    public void ResolverRechazo(ConsentRefusalResolution resolucion, string motivo, string? coberturaJson = null, int? excepcionId = null)
    {
        if (Status != ConsentStatus.Refused)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoNoRechazado,
                "Sólo se resuelve el rechazo de una parte que no consintió.", "status", Status.ToString()));
        ArgumentException.ThrowIfNullOrWhiteSpace(motivo);
        if (resolucion == ConsentRefusalResolution.ApplyWithGuaranteeExtinguished && string.IsNullOrWhiteSpace(coberturaJson))
            throw new ArgumentException("Aplicar con la garantía extinguida deja la cobertura restante.", nameof(coberturaJson));
        RefusalResolution = resolucion;
        ResolutionReason = motivo.Trim();
        CoverageAfterJson = resolucion == ConsentRefusalResolution.ApplyWithGuaranteeExtinguished ? coberturaJson : null;
        ExceptionDecisionId = resolucion == ConsentRefusalResolution.ApplyWithGuaranteeExtinguished ? excepcionId : null;
    }

    private void ExigirPendiente()
    {
        if (Status != ConsentStatus.Pending)
            throw new ReglaDeCarteraException(ProblemaDeCartera.Con(CodigoNoPendiente,
                "El consentimiento ya se registró.", "status", Status.ToString()));
    }
}
