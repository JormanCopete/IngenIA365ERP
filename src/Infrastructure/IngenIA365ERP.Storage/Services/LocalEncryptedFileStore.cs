using System.Security.Cryptography;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Storage.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Storage.Services;

/// <summary>
/// T107 — Backend local del <see cref="IBlobStore"/>, el de desarrollo. El store es <b>transparente</b>:
/// persiste los bytes que recibe tal cual. En el formato anterior esos bytes llegan cifrados por
/// <see cref="AttachmentEncryptionService"/> (de ahí el nombre); en el directo (feature 011) se guardan
/// como llegan, igual que en S3 los cifraría el almacén.
///
/// <para>
/// <b>Layout</b>: <c>{LocalRootPath}/{tenantId}/{yyyy}/{MM}/{guid}.bin</c>. El particionado por tenant
/// + año/mes mantiene los directorios chicos. <b>BlobReference.Uri</b> es la ruta <i>relativa</i> al
/// root — así un move del root path no invalida los Uris persistidos en BD.
/// </para>
///
/// <para>
/// <b>Imita a S3 para las autorizaciones</b> (research R16): firma tokens con DataProtection que llevan
/// la operación, la referencia, el tipo, el tamaño, la huella y el vencimiento, y los cobra en las rutas
/// <c>/api/attachments/local-blob/{token}</c>. Rechaza lo mismo que rechaza la política de S3, así que el
/// cliente usa en local exactamente el mismo flujo que en los ambientes, sin Docker ni MinIO.
/// </para>
/// </summary>
public sealed class LocalEncryptedFileStore : IBlobStore, IAlmacenLocal
{
    private const string FileExtension = ".bin";

    /// <summary>La ruta que las autorizaciones locales le dan al cliente; relativa a la base de la API.</summary>
    public const string RutaLocal = "/api/attachments/local-blob/";

    private readonly string _rootPath;
    private readonly IDataProtector _tokens;

    public LocalEncryptedFileStore(IOptions<AttachmentStorageSettings> settings, IDataProtectionProvider protectores)
    {
        _rootPath = Path.GetFullPath(settings.Value.LocalRootPath);
        _tokens = protectores.CreateProtector("IngenIA365ERP.Adjuntos.Local.v1");
    }

    public async Task<BlobReference> PutAsync(
        Stream content, BlobMetadata metadata, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(metadata);

        var relativePath = NuevaReferencia(metadata.TenantId);
        var absolutePath = ResolveAbsolutePath(new BlobReference(relativePath));
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);

        await using var fs = new FileStream(
            absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fs, ct);

        return new BlobReference(relativePath);
    }

    public Task<Stream> GetAsync(BlobReference reference, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var absolutePath = ResolveAbsolutePath(reference);
        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException("Blob no encontrado.", absolutePath);
        }
        Stream stream = new FileStream(
            absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(BlobReference reference, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var absolutePath = ResolveAbsolutePath(reference);
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }
        return Task.CompletedTask;
    }

    /// <summary>Escribe y borra un archivo de prueba: que el directorio exista no dice que se pueda escribir en él.</summary>
    public async Task<string> ProbarAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(_rootPath);
        var prueba = Path.Combine(_rootPath, $".healthcheck-{Guid.NewGuid():N}.tmp");
        await File.WriteAllTextAsync(prueba, "ok", ct);
        File.Delete(prueba);
        return _rootPath;
    }

    public Task<AutorizacionDeSubida> FirmarSubidaAsync(SolicitudDeSubida solicitud, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        var m = solicitud.Metadata;
        var referencia = NuevaReferencia(m.TenantId);
        var token = Proteger(new Pase("subir", referencia, m.ContentType, m.SizeBytes, solicitud.Sha256Base64, null, solicitud.VenceEn));
        // Los mismos campos que pide la política de S3, para que el cliente no distinga los almacenes.
        IReadOnlyList<KeyValuePair<string, string>> campos =
        [
            new("Content-Type", m.ContentType),
            new("x-amz-checksum-algorithm", "SHA256"),
            new("x-amz-checksum-sha256", solicitud.Sha256Base64),
        ];
        return Task.FromResult(new AutorizacionDeSubida(new BlobReference(referencia), RutaLocal + token, campos, "file", solicitud.VenceEn));
    }

    public Task<EnlaceDeDescarga> FirmarDescargaAsync(BlobReference reference, DescargaFirmada descarga, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(descarga);
        var token = Proteger(new Pase("bajar", reference.Uri, descarga.ContentType, 0, null, descarga.NombreDeArchivo, descarga.VenceEn));
        return Task.FromResult(new EnlaceDeDescarga(RutaLocal + token, descarga.VenceEn));
    }

    public async Task<EstadoDelObjeto?> ConsultarAsync(BlobReference reference, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var ruta = ResolveAbsolutePath(reference);
        if (!File.Exists(ruta)) return null;
        await using var archivo = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read);
        var huella = await SHA256.HashDataAsync(archivo, ct);
        return new EstadoDelObjeto(archivo.Length, Convert.ToBase64String(huella));
    }

    public async Task<byte[]> LeerInicioAsync(BlobReference reference, int bytes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentOutOfRangeException.ThrowIfLessThan(bytes, 1);
        var ruta = ResolveAbsolutePath(reference);
        if (!File.Exists(ruta)) throw new FileNotFoundException("Blob no encontrado.", ruta);
        await using var archivo = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read);
        var inicio = new byte[Math.Min(bytes, archivo.Length)];
        await archivo.ReadExactlyAsync(inicio, ct);
        return inicio;
    }

    /// <summary>
    /// Cobra una autorización de subida. Rechaza lo mismo que la política de S3 (research R1, espiga S1):
    /// token vencido o ajeno, otro tipo u otra huella en los campos, otro tamaño, un contenido que no da la
    /// huella firmada, y una segunda subida sobre la misma autorización.
    /// </summary>
    public async Task<Result> RecibirAsync(string token, IReadOnlyDictionary<string, string> campos, Stream archivo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(campos);
        ArgumentNullException.ThrowIfNull(archivo);
        if (Leer(token) is not { Operacion: "subir" } pase)
            return Result.Failure(Error.Forbidden);
        if (!campos.TryGetValue("Content-Type", out var tipo) || tipo != pase.ContentType
            || !campos.TryGetValue("x-amz-checksum-sha256", out var huella) || huella != pase.Sha256Base64)
            return Result.Failure(Error.Forbidden);

        var destino = ResolveAbsolutePath(new BlobReference(pase.Referencia));
        if (File.Exists(destino))
            return Result.Failure(Error.Forbidden);
        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);

        var temporal = destino + ".subiendo";
        try
        {
            using var calculo = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long total = 0;
            await using (var salida = new FileStream(temporal, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bufer = new byte[81920];
                int n;
                while ((n = await archivo.ReadAsync(bufer, ct)) > 0)
                {
                    total += n;
                    if (total > pase.Tamano)
                        return Result.Failure("Attachments.LocalUploadRejected", "El archivo es más grande que lo autorizado.");
                    calculo.AppendData(bufer, 0, n);
                    await salida.WriteAsync(bufer.AsMemory(0, n), ct);
                }
            }
            if (total != pase.Tamano)
                return Result.Failure("Attachments.LocalUploadRejected", "El archivo no tiene el tamaño autorizado.");
            if (Convert.ToBase64String(calculo.GetHashAndReset()) != pase.Sha256Base64)
                return Result.Failure("Attachments.LocalUploadRejected", "El contenido no corresponde a la huella autorizada.");

            File.Move(temporal, destino);
            return Result.Success();
        }
        finally
        {
            if (File.Exists(temporal)) File.Delete(temporal);
        }
    }

    public Task<Result<ArchivoLocal>> LeerAsync(string token, CancellationToken ct)
    {
        if (Leer(token) is not { Operacion: "bajar" } pase)
            return Task.FromResult(Result.Failure<ArchivoLocal>(Error.Forbidden));
        var ruta = ResolveAbsolutePath(new BlobReference(pase.Referencia));
        if (!File.Exists(ruta))
            return Task.FromResult(Result.Failure<ArchivoLocal>(Error.NotFound));
        Stream contenido = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(Result.Success(new ArchivoLocal(contenido, pase.NombreDeArchivo ?? Path.GetFileName(ruta), pase.ContentType)));
    }

    /// <summary>Lo que viaja dentro del token. Una sola forma para subir y bajar; la operación los separa.</summary>
    private sealed record Pase(
        string Operacion, string Referencia, string ContentType, long Tamano, string? Sha256Base64, string? NombreDeArchivo, DateTimeOffset Vence);

    private string Proteger(Pase pase) => _tokens.Protect(JsonSerializer.Serialize(pase));

    /// <summary>El pase del token, o nulo si no lo firmó este almacén, está alterado o venció.</summary>
    private Pase? Leer(string token)
    {
        try
        {
            var pase = JsonSerializer.Deserialize<Pase>(_tokens.Unprotect(token));
            return pase is not null && pase.Vence > DateTimeOffset.UtcNow ? pase : null;
        }
        catch (CryptographicException)
        {
            // Token alterado, de otro llavero o mal formado: se trata igual que uno vencido (403).
            return null;
        }
    }

    /// <summary>La referencia de un objeto nuevo, con separador <c>/</c> para que sea portable entre sistemas.</summary>
    private static string NuevaReferencia(string tenantId)
    {
        var ahora = DateTime.UtcNow;
        return $"{tenantId}/{ahora:yyyy}/{ahora:MM}/{Guid.NewGuid():N}{FileExtension}";
    }

    private string ResolveAbsolutePath(BlobReference reference)
    {
        // Defensa contra path traversal: el Uri persistido se resuelve
        // contra _rootPath y se valida que esté dentro de él.
        var normalized = reference.Uri.Replace('/', Path.DirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(_rootPath, normalized));
        if (!candidate.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"BlobReference fuera del root: '{reference.Uri}'.");
        }
        return candidate;
    }
}

/// <summary>
/// Con el proveedor S3 no hay rutas locales, pero los manejadores de Application piden la abstracción
/// igual; y en DEV (<c>ASPNETCORE_ENVIRONMENT=Development</c>) el contenedor valida al arrancar que toda
/// dependencia se pueda resolver. Esta implementación responde siempre «no disponible».
/// </summary>
public sealed class AlmacenLocalNoDisponible : IAlmacenLocal
{
    public Task<Result> RecibirAsync(string token, IReadOnlyDictionary<string, string> campos, Stream archivo, CancellationToken ct) =>
        Task.FromResult(Result.Failure(Error.NotFound));

    public Task<Result<ArchivoLocal>> LeerAsync(string token, CancellationToken ct) =>
        Task.FromResult(Result.Failure<ArchivoLocal>(Error.NotFound));
}
