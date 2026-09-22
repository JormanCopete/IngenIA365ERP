using Amazon.S3;
using Amazon.S3.Model;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Storage.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Storage.Services;

/// <summary>
/// Adjuntos en S3 (2026-09-22). Reemplaza al disco del nodo en los tres ambientes del clúster:
/// `local-path` no tiene redundancia, no entra en los respaldos diarios (que cubren PostgreSQL y
/// MongoDB) y, siendo RWO, no se puede montar desde dos nodos.
///
/// <para>
/// La <b>clave del objeto</b> conserva la partición del store local —<c>{prefijo/}{tenant}/{yyyy}/{MM}/{guid}.bin</c>—
/// y el <see cref="BlobReference"/> guarda esa clave <b>sin el prefijo</b>, igual que el local guarda
/// la ruta relativa al root: así el prefijo (o el bucket) se puede mover sin invalidar lo ya escrito
/// en la base, y un adjunto subido con el backend local se lee con éste si algún día se copian los
/// archivos tal cual.
/// </para>
///
/// <para>
/// El contenido llega <b>ya cifrado</b> por <see cref="AttachmentEncryptionService"/> (AES-256-GCM
/// con clave por archivo): S3 sólo ve bytes opacos, y el cifrado del lado del servidor (SSE-S3) va
/// encima, no en lugar del nuestro. Las credenciales las resuelve el SDK por la cadena estándar; el
/// proceso no las lee ni las registra.
/// </para>
/// </summary>
public sealed class S3BlobStore : IBlobStore, IDisposable
{
    private const string Extension = ".bin";

    private readonly IAmazonS3 _s3;
    private readonly S3StorageSettings _opciones;
    private readonly ILogger<S3BlobStore> _log;
    private readonly bool _propio;

    public S3BlobStore(IOptions<AttachmentStorageSettings> settings, ILogger<S3BlobStore> log)
        : this(Crear(settings.Value.S3), settings, log, propio: true)
    {
    }

    /// <summary>Para pruebas y para quien ya tiene un cliente configurado.</summary>
    public S3BlobStore(IAmazonS3 s3, IOptions<AttachmentStorageSettings> settings, ILogger<S3BlobStore> log, bool propio = false)
    {
        _s3 = s3;
        _opciones = settings.Value.S3;
        _log = log;
        _propio = propio;
        if (string.IsNullOrWhiteSpace(_opciones.BucketName))
            throw new InvalidOperationException("Falta AttachmentStorage:S3:BucketName: con el proveedor S3 el bucket es obligatorio.");
    }

    public async Task<BlobReference> PutAsync(Stream content, BlobMetadata metadata, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(metadata);

        var ahora = DateTime.UtcNow;
        var referencia = $"{Sanear(metadata.TenantId)}/{ahora:yyyy}/{ahora:MM}/{Guid.NewGuid():N}{Extension}";
        var peticion = new PutObjectRequest
        {
            BucketName = _opciones.BucketName,
            Key = ClaveDe(referencia, _opciones.Prefix),
            InputStream = content,
            // Lo que se guarda son bytes cifrados; declararlos como el tipo original sería mentir y
            // además revelaría en los metadatos de S3 qué clase de archivo es.
            ContentType = "application/octet-stream",
            AutoCloseStream = false,
        };
        // Metadatos para poder auditar o reconstruir sin la base; el nombre original va codificado
        // porque S3 sólo admite US-ASCII en los encabezados de metadatos.
        peticion.Metadata.Add("tenant", Sanear(metadata.TenantId));
        peticion.Metadata.Add("owner-type", Sanear(metadata.OwnerEntityType));
        peticion.Metadata.Add("owner-id", metadata.OwnerEntityPublicId.ToString());
        peticion.Metadata.Add("sha256", metadata.Sha256Hex);
        peticion.Metadata.Add("nombre", Uri.EscapeDataString(metadata.OriginalFileName));
        if (!string.IsNullOrWhiteSpace(_opciones.ServerSideEncryption))
            peticion.ServerSideEncryptionMethod = ServerSideEncryptionMethod.FindValue(_opciones.ServerSideEncryption);

        await _s3.PutObjectAsync(peticion, ct);
        return new BlobReference(referencia);
    }

    public async Task<Stream> GetAsync(BlobReference reference, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reference);
        try
        {
            var respuesta = await _s3.GetObjectAsync(_opciones.BucketName, ClaveDe(reference.Uri, _opciones.Prefix), ct);
            // El contrato dice que el consumidor es dueño del stream; se copia a memoria para poder
            // cerrar la respuesta de red aquí y no depender de que lo haga quien lo lea (un adjunto
            // pesa como mucho lo que admite AttachmentPolicy).
            var memoria = new MemoryStream();
            using (respuesta)
            await using (var origen = respuesta.ResponseStream)
            {
                await origen.CopyToAsync(memoria, ct);
            }
            memoria.Position = 0;
            return memoria;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException("Blob no encontrado.", reference.Uri, ex);
        }
    }

    public async Task DeleteAsync(BlobReference reference, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reference);
        try
        {
            await _s3.DeleteObjectAsync(_opciones.BucketName, ClaveDe(reference.Uri, _opciones.Prefix), ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Borrar lo que ya no está es el resultado que se pedía.
            _log.LogInformation("El adjunto {Referencia} ya no estaba en el bucket.", reference.Uri);
        }
    }

    /// <summary>
    /// Escribe y borra un objeto de prueba bajo <c>.healthcheck/</c>. Listar el bucket no alcanza:
    /// la credencial puede leer y no poder escribir, y eso se descubriría al subir el primer soporte.
    /// </summary>
    public async Task<string> ProbarAsync(CancellationToken ct)
    {
        var clave = ClaveDe($".healthcheck/{Guid.NewGuid():N}{Extension}", _opciones.Prefix);
        using var contenido = new MemoryStream("ok"u8.ToArray());
        var peticion = new PutObjectRequest
        {
            BucketName = _opciones.BucketName, Key = clave, InputStream = contenido, ContentType = "application/octet-stream", AutoCloseStream = false,
        };
        if (!string.IsNullOrWhiteSpace(_opciones.ServerSideEncryption))
            peticion.ServerSideEncryptionMethod = ServerSideEncryptionMethod.FindValue(_opciones.ServerSideEncryption);
        await _s3.PutObjectAsync(peticion, ct);
        await _s3.DeleteObjectAsync(_opciones.BucketName, clave, ct);
        return $"s3://{_opciones.BucketName}/{_opciones.Prefix}".TrimEnd('/');
    }

    /// <summary>La clave dentro del bucket: el prefijo del ambiente más la referencia guardada en la base.</summary>
    internal static string ClaveDe(string referencia, string? prefijo)
    {
        var limpia = referencia.Replace('\\', '/').TrimStart('/');
        if (limpia.Contains("..", StringComparison.Ordinal))
            throw new InvalidOperationException($"BlobReference inválida: '{referencia}'.");
        var raiz = (prefijo ?? string.Empty).Trim().Trim('/');
        return raiz.Length == 0 ? limpia : $"{raiz}/{limpia}";
    }

    /// <summary>Un segmento de clave sin sorpresas: sin barras, sin espacios y sin caracteres fuera de US-ASCII.</summary>
    private static string Sanear(string texto)
    {
        var limpio = new string((texto ?? string.Empty).Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-').ToArray()).Trim('-');
        return limpio.Length == 0 ? "sin-cooperativa" : limpio;
    }

    private static IAmazonS3 Crear(S3StorageSettings opciones)
    {
        var configuracion = new AmazonS3Config { ForcePathStyle = opciones.ForcePathStyle };
        if (!string.IsNullOrWhiteSpace(opciones.ServiceUrl)) configuracion.ServiceURL = opciones.ServiceUrl;
        else configuracion.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(opciones.Region);
        return new AmazonS3Client(configuracion);
    }

    public void Dispose()
    {
        if (_propio) _s3.Dispose();
    }
}
