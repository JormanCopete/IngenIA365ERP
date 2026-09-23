namespace IngenIA365ERP.Application.Attachments.Common;

/// <summary>
/// Códigos canónicos de error del módulo Attachments (US5).
/// Convención <c>Modulo.Condicion</c> — `ErrorEnvelopeFilter` infiere
/// el HTTP status del prefijo.
/// </summary>
public static class AttachmentErrorCodes
{
    /// <summary>El MIME type no está en la allowlist del tenant.</summary>
    public const string Validation_MimeTypeNotAllowed = "Validation.Attachments.MimeTypeNotAllowed";

    /// <summary>El archivo excede el tamaño máximo permitido.</summary>
    public const string Validation_FileTooLarge = "Validation.Attachments.FileTooLarge";

    /// <summary>El archivo subido está vacío.</summary>
    public const string Validation_FileEmpty = "Validation.Attachments.FileEmpty";

    /// <summary>El SHA-256 recalculado tras descifrar NO coincide — blob corrupto o manipulado.</summary>
    public const string IntegrityFailed = "Attachments.IntegrityFailed";

    /// <summary>El blob existe en BD pero ha sido removido del store (state inconsistente).</summary>
    public const string BlobMissing = "Attachments.BlobMissing";

    /// <summary>
    /// El adjunto lo generó un módulo que lo declara inmutable (<see cref="AdjuntosDeModulo"/>): no se
    /// borra por la ruta genérica. 422.
    /// </summary>
    public const string OwnedByModule = "Attachments.OwnedByModule";

    // --- Feature 011: subida y descarga directas al almacén (contracts/api.md) ---

    /// <summary>
    /// El tipo de dueño no admite subidas de personas: lo genera un módulo (PILA, dispersión,
    /// definitiva) o todavía no está habilitado (FR-019). 422.
    /// </summary>
    public const string OwnerNotAllowed = "Attachments.OwnerNotAllowed";

    /// <summary>Es soporte de un comprobante contabilizado: se conserva, no se borra (FR-004). 422.</summary>
    public const string OwnerLocked = "Attachments.OwnerLocked";

    /// <summary>El estado del adjunto no admite lo pedido (no está disponible, o no se puede renovar). 409.</summary>
    public const string NotAvailable = "Attachments.NotAvailable";

    /// <summary>Es del formato directo: se baja con un enlace firmado, no a través de la API. 409.</summary>
    public const string UseDownloadLink = "Attachments.UseDownloadLink";

    /// <summary>Se excedió el límite de concurrencia de las rutas de adjuntos. 429.</summary>
    public const string Busy = "Attachments.Busy";
}

/// <summary>
/// Allowlist conservadora de MIME types aceptados en upload (US5). Vive en
/// código por ahora; en una iteración futura se moverá a <c>TenantSetting</c>
/// para que cada cooperativa pueda relajar/endurecer.
/// </summary>
public static class AttachmentPolicy
{
    public const long MaxBytes = 25L * 1024 * 1024; // 25 MB

    public static readonly IReadOnlySet<string> AllowedMimeTypes = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/png",
        "image/jpeg",
        "image/webp",
        "image/gif",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "text/plain",
        "text/csv"
    };
}
