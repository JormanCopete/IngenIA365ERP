using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Documents;

namespace IngenIA365ERP.Domain.Entities.Lending.Documents;

/// <summary>
/// <c>LND_IssuedDocuments</c> (feature 013, C.3; data-model §27.1; FR-131, FR-132, D21, D23; T80): un documento emitido con
/// número y código de verificación (plan de pagos, paz y salvo, certificado de saldo…). Hecho de sólo inserción
/// (<see cref="IHechoInmutable"/>): reimprimir es descargar el mismo PDF; el estado (<c>IssuedDocumentStatus</c>) se calcula con
/// sus eventos y <see cref="FinalityUntil"/>. La identificación del titular se copia tal como se emitió (I-1).
///
/// <para>US13 (T0776): el estado a una fecha lo da <see cref="EstadoA"/> (<see cref="EstadoDeDocumentoEmitido"/>); los largos de las copias selladas
/// están en las constantes, para que quien arma el documento recorte antes de guardar y no al revés.</para>
/// </summary>
public class IssuedLendingDocument : AuditableEntity, IHechoInmutable
{
    /// <summary>Largo del nombre del titular (<c>nvarchar(300)</c>).</summary>
    public const int LargoDelNombre = 300;

    /// <summary>Largo del tipo y del número de identificación copiados (<c>nvarchar(10)</c>, <c>nvarchar(30)</c>).</summary>
    public const int LargoDelTipoDeDocumento = 10;
    public const int LargoDelDocumento = 30;

    /// <summary>Largo de la nota de ventas fuera de Cartera (<c>nvarchar(1000)</c>).</summary>
    public const int LargoDeLaNota = 1000;

    /// <summary>Largo del nombre y del cargo del firmante (<c>nvarchar(150)</c>).</summary>
    public const int LargoDelFirmante = 150;

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

    /// <summary>
    /// Quien lo emitió. Feature 014, US13 (T194; research E168): nulo sólo en lo que emite el proceso diario sin persona —el título de la prórroga
    /// automática de un CDAT—; el evento <c>Issued</c> lo dice con <c>ActorKind = Process</c>.
    /// </summary>
    public int? IssuedByUserId { get; init; }

    public DateTime IssuedAt { get; init; }

    public DocumentDeliveryMode DeliveryMode { get; init; }

    /// <summary>El estado calculado a <paramref name="hoy"/> (data-model §27.1): anulado si <paramref name="anulado"/>, sujeto a firmeza mientras
    /// hoy &lt; <see cref="FinalityUntil"/>, vigente en otro caso.</summary>
    public IssuedDocumentStatus EstadoA(bool anulado, DateOnly hoy) => EstadoDeDocumentoEmitido.Calcular(anulado, FinalityUntil, hoy);

    /// <summary>Feature 014 (T194; documentos.md §1.2): el estado con los eventos de título (reemplazado, liquidado).</summary>
    public IssuedDocumentStatus EstadoA(EventosDeEstado eventos, DateOnly hoy) => EstadoDeDocumentoEmitido.Calcular(eventos, FinalityUntil, hoy);
}
