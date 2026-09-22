using IngenIA365ERP.Application.Common.Interfaces.Storage;
using IngenIA365ERP.Storage.Configuration;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Storage.Services;

/// <summary>
/// T107 — Backend local del <see cref="IBlobStore"/> para adjuntos
/// cifrados. El store es <b>transparente</b>: persiste los bytes que recibe
/// tal cual. El cifrado/descifrado lo hace el handler con
/// <see cref="AttachmentEncryptionService"/>. El nombre "Encrypted" describe
/// el contenido del store (todos los blobs allí dentro están cifrados), no
/// la responsabilidad del store.
///
/// <para>
/// <b>Layout</b>: <c>{LocalRootPath}/{tenantId}/{yyyy}/{MM}/{guid}.bin</c>.
/// El particionado por tenant + año/mes mantiene los directorios chicos
/// (menos de 10k archivos por carpeta en tenants normales) y simplifica
/// retention/backup por rango de fechas.
/// </para>
///
/// <para>
/// <b>BlobReference.Uri</b> es la ruta <i>relativa</i> al root — así un
/// move del root path no invalida los Uris persistidos en BD.
/// </para>
/// </summary>
public sealed class LocalEncryptedFileStore : IBlobStore
{
    private const string FileExtension = ".bin";

    private readonly string _rootPath;

    public LocalEncryptedFileStore(IOptions<AttachmentStorageSettings> settings)
    {
        _rootPath = Path.GetFullPath(settings.Value.LocalRootPath);
    }

    public async Task<BlobReference> PutAsync(
        Stream content, BlobMetadata metadata, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(metadata);

        var now = DateTime.UtcNow;
        var relativeDir = Path.Combine(
            metadata.TenantId,
            now.Year.ToString("D4"),
            now.Month.ToString("D2"));
        var fileName = Guid.NewGuid().ToString("N") + FileExtension;
        var relativePath = Path.Combine(relativeDir, fileName);
        var absoluteDir = Path.Combine(_rootPath, relativeDir);
        var absolutePath = Path.Combine(_rootPath, relativePath);

        Directory.CreateDirectory(absoluteDir);

        await using var fs = new FileStream(
            absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fs, ct);

        // Usar separador forward para que el Uri persistido sea portable
        // entre Windows / Linux (lectura via Path.Combine restaura el correcto).
        return new BlobReference(relativePath.Replace('\\', '/'));
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
