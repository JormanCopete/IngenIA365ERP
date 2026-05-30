namespace IngenIA365ERP.Application.Common.Interfaces.Storage;

/// <summary>
/// Almacén de blobs abstracto. La implementación por defecto en Fase 0 es
/// <c>LocalEncryptedFileStore</c> (T107, US5) que persiste blobs cifrados
/// en filesystem; backends compatibles con S3 quedan disponibles vía
/// implementación alternativa sin tocar el dominio. Vive en Application
/// para que los handlers no se acoplen a Infrastructure.
/// </summary>
public interface IBlobStore
{
    /// <summary>
    /// Persiste el contenido del stream y devuelve la referencia opaca con
    /// la que se puede recuperar más adelante. La referencia debe ser
    /// idempotente (no contiene info del cliente que no se pueda reconstruir).
    /// </summary>
    Task<BlobReference> PutAsync(Stream content, BlobMetadata metadata, CancellationToken ct);

    /// <summary>
    /// Recupera el contenido del blob. El stream devuelto es propiedad del
    /// consumidor (debe disponerlo).
    /// </summary>
    Task<Stream> GetAsync(BlobReference reference, CancellationToken ct);

    /// <summary>
    /// Elimina el blob. Las implementaciones son libres de implementar
    /// "soft-delete" interno; la semántica para el dominio es: tras esta
    /// llamada, <see cref="GetAsync"/> debe fallar con
    /// <see cref="FileNotFoundException"/>.
    /// </summary>
    Task DeleteAsync(BlobReference reference, CancellationToken ct);
}

public sealed record BlobReference(string Uri);

public sealed record BlobMetadata(
    string TenantId,
    string OwnerEntityType,
    Guid OwnerEntityPublicId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256Hex);
