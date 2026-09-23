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

        var referencia = NuevaReferencia(metadata.TenantId);
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
        catch (AmazonS3Exception ex) when (NoEsta(ex, reference.Uri))
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

    /// <summary>
    /// POST prefirmado (feature 011, research R1). La espiga S1 del 2026-09-23 contra el bucket real
    /// confirmó que así queda <b>inmutable desde que se autoriza</b>: un byte de más da
    /// <c>EntityTooLarge</c>, otro tipo u otra clave dan 403, el contenido alterado da <c>BadDigest</c>, y
    /// cambiar la huella por la del contenido alterado da 403, porque el SDK convierte cada campo de
    /// <see cref="CreatePresignedPostRequest.Fields"/> en una condición exacta de la política.
    /// </summary>
    public async Task<AutorizacionDeSubida> FirmarSubidaAsync(SolicitudDeSubida solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var m = solicitud.Metadata;
        var referencia = NuevaReferencia(m.TenantId);
        var campos = new Dictionary<string, string>
        {
            ["Content-Type"] = m.ContentType,
            // Sin estos dos, S3 acepta un contenido distinto del mismo tamaño (espiga S1, caso e).
            ["x-amz-checksum-algorithm"] = "SHA256",
            ["x-amz-checksum-sha256"] = solicitud.Sha256Base64,
            // Los mismos metadatos que PutAsync, para poder auditar o reconstruir sin la base.
            ["x-amz-meta-tenant"] = Sanear(m.TenantId),
            ["x-amz-meta-owner-type"] = Sanear(m.OwnerEntityType),
            ["x-amz-meta-owner-id"] = m.OwnerEntityPublicId.ToString(),
            ["x-amz-meta-sha256"] = m.Sha256Hex,
            ["x-amz-meta-nombre"] = Uri.EscapeDataString(m.OriginalFileName),
        };
        if (!string.IsNullOrWhiteSpace(_opciones.ServerSideEncryption))
            campos["x-amz-server-side-encryption"] = _opciones.ServerSideEncryption;

        var firma = await _s3.CreatePresignedPostAsync(new CreatePresignedPostRequest
        {
            BucketName = _opciones.BucketName,
            Key = ClaveDe(referencia, _opciones.Prefix),
            Expires = solicitud.VenceEn.UtcDateTime,
            Conditions = [new ContentLengthRangeCondition(m.SizeBytes, m.SizeBytes), new ExactMatchCondition("Content-Type", m.ContentType)],
            Fields = campos,
        });
        return new AutorizacionDeSubida(
            new BlobReference(referencia),
            firma.Url.ToString(),
            firma.Fields.ToList(),
            CampoDelArchivo: "file",
            solicitud.VenceEn);
    }

    /// <summary>
    /// GET prefirmado (research R2). Las cabeceras de la respuesta van firmadas: el navegador guarda el
    /// archivo con su nombre —nunca lo abre dentro de la página— y la PILA conserva su <c>charset</c>.
    /// </summary>
    public async Task<EnlaceDeDescarga> FirmarDescargaAsync(BlobReference reference, DescargaFirmada descarga, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(descarga);
        var pedido = new GetPreSignedUrlRequest
        {
            BucketName = _opciones.BucketName,
            Key = ClaveDe(reference.Uri, _opciones.Prefix),
            Verb = HttpVerb.GET,
            Expires = descarga.VenceEn.UtcDateTime,
            // El SDK firma con https salvo que se le diga otra cosa, aunque el cliente apunte a un
            // almacén compatible por http (AttachmentStorage:S3:ServiceUrl): el enlace no abría.
            Protocol = EsHttpPlano() ? Protocol.HTTP : Protocol.HTTPS,
        };
        pedido.ResponseHeaderOverrides.ContentDisposition = ComoAdjunto(descarga.NombreDeArchivo);
        pedido.ResponseHeaderOverrides.ContentType = descarga.ContentType;
        pedido.ResponseHeaderOverrides.CacheControl = "no-store";
        return new EnlaceDeDescarga(await _s3.GetPreSignedURLAsync(pedido), descarga.VenceEn);
    }

    public async Task<EstadoDelObjeto?> ConsultarAsync(BlobReference reference, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reference);
        try
        {
            var cabecera = await _s3.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _opciones.BucketName,
                Key = ClaveDe(reference.Uri, _opciones.Prefix),
                ChecksumMode = ChecksumMode.ENABLED,
            }, ct);
            return new EstadoDelObjeto(cabecera.ContentLength, string.IsNullOrEmpty(cabecera.ChecksumSHA256) ? null : cabecera.ChecksumSHA256);
        }
        catch (AmazonS3Exception ex) when (NoEsta(ex, reference.Uri))
        {
            return null;
        }
    }

    public async Task<byte[]> LeerInicioAsync(BlobReference reference, int bytes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentOutOfRangeException.ThrowIfLessThan(bytes, 1);
        try
        {
            using var respuesta = await _s3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _opciones.BucketName,
                Key = ClaveDe(reference.Uri, _opciones.Prefix),
                ByteRange = new ByteRange(0, bytes - 1),
            }, ct);
            var inicio = new byte[bytes];
            var leidos = 0;
            int n;
            while (leidos < bytes && (n = await respuesta.ResponseStream.ReadAsync(inicio.AsMemory(leidos, bytes - leidos), ct)) > 0)
                leidos += n;
            return leidos == bytes ? inicio : inicio[..leidos];
        }
        catch (AmazonS3Exception ex) when (NoEsta(ex, reference.Uri))
        {
            throw new FileNotFoundException("Blob no encontrado.", reference.Uri, ex);
        }
    }

    /// <summary>
    /// «No está» es 404, y también 403: sin permiso de listar el bucket —a propósito, research R4— S3
    /// responde 403 ante una clave que no existe, para no revelar qué hay. Eso mismo podría esconder un
    /// permiso mal configurado, así que cada conversión queda en el log en vez de pasar en silencio.
    /// </summary>
    private bool NoEsta(AmazonS3Exception ex, string referencia)
    {
        if (ex.StatusCode == System.Net.HttpStatusCode.NotFound) return true;
        if (ex.StatusCode != System.Net.HttpStatusCode.Forbidden) return false;
        _log.LogInformation(
            "El almacén respondió 403 para {Referencia}; sin permiso de listar es lo que responde ante un objeto que no está. " +
            "Si se repite con objetos que sí existen, revisar la política del rol.", referencia);
        return true;
    }

    /// <summary>Verdadero sólo si el cliente apunta a un almacén compatible servido por http (MinIO en local).</summary>
    private bool EsHttpPlano() =>
        _s3.Config?.ServiceURL?.StartsWith("http://", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>La referencia de un objeto nuevo: la misma partición que el almacén local.</summary>
    private static string NuevaReferencia(string tenantId)
    {
        var ahora = DateTime.UtcNow;
        return $"{Sanear(tenantId)}/{ahora:yyyy}/{ahora:MM}/{Guid.NewGuid():N}{Extension}";
    }

    /// <summary>
    /// <c>Content-Disposition</c> según RFC 6266, con el nombre en UTF-8 (RFC 5987) y una versión ASCII
    /// para clientes viejos: «Factura – Núñez.pdf» llega con sus tildes a cualquier navegador actual.
    /// </summary>
    internal static string ComoAdjunto(string nombre)
    {
        var ascii = new string(nombre.Select(c => c is >= ' ' and <= '~' and not '"' and not '\\' ? c : '_').ToArray());
        return $"attachment; filename=\"{ascii}\"; filename*=UTF-8''{Uri.EscapeDataString(nombre)}";
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
