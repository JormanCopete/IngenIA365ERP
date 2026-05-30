namespace IngenIA365ERP.Application.Attachments.Common;

/// <summary>Metadata pública de un adjunto (US5).</summary>
public sealed record AttachmentDto(
    Guid PublicId,
    string OwnerEntityType,
    Guid OwnerEntityPublicId,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256Hex,
    DateTime CreatedAt,
    string? CreatedBy);

/// <summary>
/// Resultado de la descarga: payload descifrado + metadata para el HTTP
/// response. Sin streaming — Fase 0 carga en memoria (≤ 25 MB por blob).
/// </summary>
public sealed record AttachmentDownload(
    byte[] Content,
    string FileName,
    string ContentType,
    string Sha256Hex);
