namespace IngenIA365ERP.Application.Common.Interfaces.Storage;

/// <summary>
/// Almacén de blobs abstracto. Dos implementaciones (se elige por configuración,
/// <c>AttachmentStorage:Provider</c>): <c>LocalEncryptedFileStore</c> (T107, US5), que escribe en
/// el sistema de archivos y es lo que se usa en desarrollo, y <c>S3BlobStore</c> (2026-09-22), que
/// escribe en S3 y es lo que corre en los tres ambientes del clúster —el disco del nodo no tiene
/// redundancia ni respaldo, y con más de un nodo un volumen RWO ni siquiera se puede montar dos
/// veces—. Vive en Application para que los handlers no se acoplen a Infrastructure.
///
/// <para>
/// El almacén es <b>transparente</b>: guarda los bytes que recibe. El contenido ya viene cifrado
/// por <see cref="IAttachmentCipher"/>, así que ningún backend ve el archivo original.
/// </para>
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

    /// <summary>
    /// Comprueba que el almacén responde y se puede escribir en él, y describe en una frase dónde
    /// está guardando (la ruta, el bucket). Lo usa el health check de la API: sin esto tendría que
    /// saber si el backend es disco o S3, que es justamente lo que esta interfaz esconde.
    /// </summary>
    Task<string> ProbarAsync(CancellationToken ct);
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
