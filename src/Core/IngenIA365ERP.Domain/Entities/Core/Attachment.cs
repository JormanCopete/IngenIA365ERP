using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// T105 — Adjunto cifrado en reposo (FR-030/FR-031). Mapea a
/// <c>[dbo].[COR_Attachments]</c>. Sigue el patrón <see cref="AuditableEntityLong"/>
/// porque los adjuntos pueden crecer fuerte (BIGINT PK).
///
/// <para>
/// El blob real vive en <see cref="StoragePath"/> (cifrado AES-256-GCM).
/// La clave DEK por blob se envuelve con la KEK derivada de DataProtection
/// y se guarda en <see cref="EncryptedDek"/>. Verificación de integridad
/// post-descifrado vía <see cref="Sha256Hex"/> del payload original.
/// </para>
///
/// <para>
/// <b>Ownership</b>: cada adjunto pertenece a una entidad (User, Loan,
/// Transaction, etc.). Se identifica por <see cref="OwnerEntityType"/> +
/// <see cref="OwnerEntityPublicId"/> (Guid, no int — Principio VI). El
/// permiso para descargar se evalúa contra la entidad propietaria.
/// </para>
/// </summary>
public class Attachment : AuditableEntityLong
{
    public int TenantId { get; set; }

    /// <summary>Nombre canónico del tipo dueño (ej. "User", "Loan").</summary>
    [MaxLength(100)]
    public string OwnerEntityType { get; set; } = string.Empty;

    /// <summary>PublicId (Guid) de la entidad dueña — Principio VI.</summary>
    public Guid OwnerEntityPublicId { get; set; }

    /// <summary>Nombre original del archivo subido por el usuario.</summary>
    [MaxLength(500)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>MIME type validado contra una allowlist por tenant.</summary>
    [MaxLength(200)]
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Bytes del archivo original (antes de cifrar).</summary>
    public long SizeBytes { get; set; }

    /// <summary>SHA-256 hex del payload original para verificación post-descifrado.</summary>
    [MaxLength(64)]
    public string Sha256Hex { get; set; } = string.Empty;

    /// <summary>Ruta lógica del blob cifrado en el store (relativa al backend).</summary>
    [MaxLength(2000)]
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>Backend: "Local", "S3", "Azure" — sin acoplar a SDKs.</summary>
    [MaxLength(50)]
    public string StorageProvider { get; set; } = "Local";

    /// <summary>
    /// DEK envuelta por la KEK de DataProtection (Base64). Cada blob tiene
    /// su propia DEK random; la KEK rota sin re-cifrar blobs.
    /// </summary>
    [MaxLength(1000)]
    public string EncryptedDek { get; set; } = string.Empty;
}
