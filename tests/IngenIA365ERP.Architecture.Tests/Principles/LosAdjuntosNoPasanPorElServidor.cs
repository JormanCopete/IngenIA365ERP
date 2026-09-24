using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 011 (US1, T039): el archivo de una persona va del navegador al almacén y del almacén al
/// navegador. Las rutas de adjuntos sólo firman, revisan y listan; ninguna recibe el archivo. Hasta el
/// 2026-09-23 la subida pasaba entera por la API —tres copias en memoria por operación, en pods de
/// 1 GiB— y dejaba colgar un archivo de cualquier dueño.
///
/// <para>
/// La única excepción es <c>LocalBlobEndpoints</c>: imita al almacén en desarrollo, y sólo existe con
/// <c>AttachmentStorage:Provider = Local</c> y fuera de Production. Es una prueba sobre el fuente porque
/// una ruta nueva que reciba un <c>IFormFile</c> no rompe ninguna prueba de unidad: sólo vuelve a meter
/// los archivos en la memoria del servidor.
/// </para>
/// </summary>
public class LosAdjuntosNoPasanPorElServidor
{
    private static readonly Regex RecibeArchivos = new(
        @"\bIFormFile(Collection)?\b|\bReadFormAsync\b|\bRequest\.Body\b|\bRequest\.BodyReader\b|\[FromForm\]|\bStream\s+\w+\s*[,)]",
        RegexOptions.Compiled);

    private static IEnumerable<string> RutasDeAdjuntos()
    {
        var endpoints = Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.API", "Endpoints");
        yield return Path.Combine(endpoints, "AttachmentsModule.cs");
        foreach (var archivo in Directory.EnumerateFiles(Path.Combine(endpoints, "Attachments"), "*.cs"))
            if (!Path.GetFileName(archivo).Equals("LocalBlobEndpoints.cs", StringComparison.OrdinalIgnoreCase))
                yield return archivo;
    }

    [Fact]
    public void Ninguna_ruta_de_adjuntos_recibe_el_archivo()
    {
        var infractores = RutasDeAdjuntos()
            .Where(f => File.Exists(f) && RecibeArchivos.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetFileName(f))
            .ToList();

        Assert.True(infractores.Count == 0,
            "Rutas de adjuntos que reciben el archivo (feature 011, research R14): las personas suben directo al " +
            "almacén con una autorización firmada (POST /api/attachments/uploads). " + string.Join(", ", infractores));
    }

    [Fact]
    public void La_unica_que_lo_recibe_es_la_que_imita_al_almacen_en_desarrollo()
    {
        var local = Path.Combine(RepoPath.FindRepoRoot(), "src", "Presentation", "IngenIA365ERP.API", "Endpoints", "Attachments", "LocalBlobEndpoints.cs");

        // Si el detector no la viera, la prueba de arriba pasaría aunque no detectara nada.
        Assert.Matches(RecibeArchivos, File.ReadAllText(local));
        Assert.Contains("DebenExistir", File.ReadAllText(local));
    }
}
