using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Documents;

/// <summary>
/// <c>LND_IssuedDocuments</c> (feature 013, C.3; data-model §27.1; FR-131, FR-132, D21, D23; T80): un documento emitido con
/// número y código de verificación (plan de pagos, paz y salvo, certificado de saldo…). Hecho de sólo inserción
/// (<see cref="IHechoInmutable"/>): reimprimir es descargar el mismo PDF; el estado (<c>IssuedDocumentStatus</c>) se calcula con
/// sus eventos y <see cref="FinalityUntil"/>. La identificación del titular se copia tal como se emitió (I-1).
/// </summary>
public class IssuedLendingDocument : AuditableEntity, IHechoInmutable
{
    public IssuedDocumentKind Kind { get; init; }

    /// <summary>Prefijo por clase + <c>NextValue</c> de <c>LND_NumberSequences</c>; único.</summary>
    public string Number { get; init; } = string.Empty;

    /// <summary>Titular.</summary>
    public int PersonId { get; init; }

    public string HolderTaxIdType { get; init; } = string.Empty;

    public string HolderTaxId { get; init; } = string.Empty;

    public string HolderName { get; init; } = string.Empty;

    /// <summary>Corte (paz y salvo, certificado de saldo, plan).</summary>
    public DateOnly? AsOfDate { get; init; }

    public DateOnly? PeriodFrom { get; init; }

    public DateOnly? PeriodTo { get; init; }

    public short? TaxYear { get; init; }

    /// <summary>R de la liquidación que respalda el documento.</summary>
    public DateTime InformationCutoff { get; init; }

    /// <summary>Crockford base32 aleatorio (80 bits), 16 caracteres; único.</summary>
    public string VerificationCode { get; init; } = string.Empty;

    /// <summary>Huella del PDF guardado.</summary>
    public string ContentSha256 { get; init; } = string.Empty;

    /// <summary>Adjunto <c>LendingIssuedDocument</c>, no borrable.</summary>
    public Guid PdfAttachmentPublicId { get; init; }

    /// <summary>JSON con lo que muestra la verificación.</summary>
    public string EssentialDataJson { get; init; } = "{}";

    public string? SettlementSha256 { get; init; }

    /// <summary>Versión de reglas de la liquidación (T24).</summary>
    public string RulesVersion { get; init; } = string.Empty;

    /// <summary>Paz y salvo: fin del plazo de firmeza de la clase de medio del pago que canceló (D23).</summary>
    public DateOnly? FinalityUntil { get; init; }

    /// <summary>Paz y salvo general: lo que dijo <c>Documentos.PazYSalvoIncluyeCodeudas</c>.</summary>
    public bool? CoversCoDebts { get; init; }

    /// <summary>Ventas aceptadas sin migrar que el documento menciona como fuera de Cartera (FR-131).</summary>
    public string? OutsideLendingNote { get; init; }

    public string SignerName { get; init; } = string.Empty;

    public string SignerTitle { get; init; } = string.Empty;

    /// <summary>Lleva «AMBIENTE DE PRUEBAS – SIN VALIDEZ» (T73).</summary>
    public bool IsTestEnvironment { get; init; }

    public int IssuedByUserId { get; init; }

    public DateTime IssuedAt { get; init; }

    public DocumentDeliveryMode DeliveryMode { get; init; }
}
