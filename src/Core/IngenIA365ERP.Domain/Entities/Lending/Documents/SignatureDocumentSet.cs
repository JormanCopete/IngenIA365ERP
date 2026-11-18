using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Documents;

/// <summary>
/// <c>LND_SignatureDocumentSets</c> (2026-11-18, pedido del dueño): un juego de documentos para firma generado para una solicitud —un PDF con el
/// pagaré, la carta de instrucciones, la libranza si aplica y los demás de la cooperativa—. Se guarda tal como salió: el PDF (adjunto no borrable), su
/// SHA-256, las plantillas con su versión y los valores de los campos. Volver a generar deja éste <see cref="SignatureDocumentSetStatus.Superseded"/>.
/// Lo que se firma es el papel: la copia firmada se sube como hoy, en el pagaré o en los soportes de la solicitud. (nuevo)
/// </summary>
public class SignatureDocumentSet : AuditableEntity
{
    public int ApplicationId { get; set; }

    /// <summary>El pagaré de la solicitud al generar (el que el juego creó o el que ya existía), sin FK.</summary>
    public Guid? PromissoryNotePublicId { get; set; }

    public DateTime GeneratedAt { get; set; }

    public int? GeneratedByUserId { get; set; }

    public Guid AttachmentPublicId { get; set; }

    public string ContentSha256 { get; set; } = string.Empty;

    /// <summary>JSON: los documentos del juego (código, nombre, clase, versión y si estaba validado).</summary>
    public string DocumentsJson { get; set; } = "[]";

    /// <summary>JSON: los valores de los campos con que salió.</summary>
    public string FieldsJson { get; set; } = "{}";

    public SignatureDocumentSetStatus Status { get; set; } = SignatureDocumentSetStatus.Active;

    public DateTime? SupersededAt { get; set; }
}
