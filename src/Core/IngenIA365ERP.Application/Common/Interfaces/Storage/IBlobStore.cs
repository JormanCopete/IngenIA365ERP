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
/// El almacén es <b>transparente</b>: guarda los bytes que recibe. En el formato anterior el contenido
/// llega cifrado por <see cref="IAttachmentCipher"/>; en el directo (feature 011) lo cifra el propio
/// almacén en reposo, y el archivo no pasa por el servidor: el cliente lo sube y lo baja con las
/// autorizaciones que firman <see cref="FirmarSubidaAsync"/> y <see cref="FirmarDescargaAsync"/>.
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

    /// <summary>
    /// Reserva la ubicación de un archivo que el cliente va a subir directo al almacén y firma la
    /// autorización: sirve para <b>esa</b> clave, ese tipo, ese tamaño exacto y esa huella, hasta
    /// <see cref="SolicitudDeSubida.VenceEn"/> (research R1). Lo que no coincida lo rechaza el almacén
    /// en la entrada, sin intervención del ERP. No escribe nada: sólo firma.
    /// </summary>
    Task<AutorizacionDeSubida> FirmarSubidaAsync(SolicitudDeSubida solicitud, CancellationToken ct);

    /// <summary>
    /// Firma un enlace de descarga directa que fuerza el nombre y el tipo originales y vence en
    /// <see cref="DescargaFirmada.VenceEn"/> (research R2). No comprueba que el objeto exista.
    /// </summary>
    Task<EnlaceDeDescarga> FirmarDescargaAsync(BlobReference reference, DescargaFirmada descarga, CancellationToken ct);

    /// <summary>Si el objeto existe, cuánto mide y la huella que guardó el almacén; nulo si no está.</summary>
    Task<EstadoDelObjeto?> ConsultarAsync(BlobReference reference, CancellationToken ct);

    /// <summary>
    /// Los primeros <paramref name="bytes"/> del objeto, o menos si es más chico. Sirve para validar la
    /// firma del contenido sin traer el archivo entero a la memoria del servidor (research R6).
    /// </summary>
    Task<byte[]> LeerInicioAsync(BlobReference reference, int bytes, CancellationToken ct);
}

public sealed record BlobReference(string Uri);

/// <summary>
/// Lo que el ERP autoriza a subir. La huella va en base64, que es como la exige el almacén; en la
/// base se guarda en hexadecimal (<c>Sha256Hex</c>).
/// </summary>
public sealed record SolicitudDeSubida(BlobMetadata Metadata, string Sha256Base64, DateTimeOffset VenceEn);

/// <summary>
/// Una autorización de subida: el cliente arma un formulario con <see cref="Campos"/> —todos, en ese
/// orden— y el archivo <b>al final</b>, en <see cref="CampoDelArchivo"/>, y lo envía a <see cref="Url"/>
/// por POST. <see cref="Referencia"/> es lo que se guarda en la base (sin el prefijo del ambiente).
/// </summary>
public sealed record AutorizacionDeSubida(
    BlobReference Referencia,
    string Url,
    IReadOnlyList<KeyValuePair<string, string>> Campos,
    string CampoDelArchivo,
    DateTimeOffset VenceEn);

/// <summary>Cómo tiene que llegar el archivo al navegador. El tipo puede llevar <c>charset</c> (la PILA).</summary>
public sealed record DescargaFirmada(string NombreDeArchivo, string ContentType, DateTimeOffset VenceEn);

public sealed record EnlaceDeDescarga(string Url, DateTimeOffset VenceEn);

/// <summary>Lo que el almacén sabe del objeto. La huella es nula si el almacén no la guardó.</summary>
public sealed record EstadoDelObjeto(long Tamano, string? Sha256Base64);

public sealed record BlobMetadata(
    string TenantId,
    string OwnerEntityType,
    Guid OwnerEntityPublicId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256Hex);
