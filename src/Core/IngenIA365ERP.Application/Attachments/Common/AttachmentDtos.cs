using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Application.Attachments.Common;

/// <summary>Metadata pública de un adjunto (US5).</summary>
/// <param name="CanDelete">
/// Feature 011 (FR-004): si quien consulta puede borrarlo —tiene <c>Attachments.Delete</c> y la regla del
/// dueño lo admite; el soporte de un comprobante contabilizado, por ejemplo, no—. La pantalla muestra el
/// botón según esto, no según lo que ella crea: hasta el 2026-09-23 sólo la pantalla lo decidía y la API
/// lo borraba igual.
/// </param>
/// <param name="Status">Feature 011 (§4): subiendo, disponible, rechazado o incompleto. Viaja como número.</param>
/// <param name="Format">El formato anterior (cifrado por la aplicación, se baja por la API) o el directo.</param>
/// <param name="NeedsConfirmation">
/// Una subida con la autorización vencida que nadie confirmó: la pantalla llama a <c>confirm</c> para
/// ella (R5). La consulta no escribe; el cambio de estado lo hace ese comando, que queda auditado.
/// </param>
public sealed record AttachmentDto(
    Guid PublicId,
    string OwnerEntityType,
    Guid OwnerEntityPublicId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256Hex,
    DateTime CreatedAt,
    string? CreatedBy,
    bool CanDelete,
    EstadoDeAdjunto Status = EstadoDeAdjunto.Available,
    FormatoDeAdjunto Format = FormatoDeAdjunto.AppEncrypted,
    string? RejectionReason = null,
    bool NeedsConfirmation = false);

/// <summary>
/// Resultado de la descarga: payload descifrado + metadata para el HTTP
/// response. Sin streaming — Fase 0 carga en memoria (≤ 25 MB por blob).
/// </summary>
public sealed record AttachmentDownload(
    byte[] Content,
    string FileName,
    string ContentType,
    string Sha256Hex);
