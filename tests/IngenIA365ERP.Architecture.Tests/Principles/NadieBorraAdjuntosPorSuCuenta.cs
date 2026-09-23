using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 011 (FR-001, US3): ningún archivo de una cooperativa se borra solo. Retirar un objeto del
/// almacén es siempre consecuencia del acto de una persona —borrar un adjunto, descartar un borrador con
/// sus soportes— o el deshacer de una operación que no llegó a existir: la subida que la base no pudo
/// registrar, y la rechazada al confirmar (opción A: su objeto va a la papelera). Nada más.
///
/// <para>
/// Es una prueba sobre el fuente porque un trabajo de limpieza nuevo no rompe ninguna prueba de unidad:
/// sólo vuelve a abrir la puerta que esta feature cerró. Hasta el 2026-09-23 la documentación prometía un
/// «GC programado» que purgaría los adjuntos borrados; nunca existió, y esta prueba es la que impide que
/// alguien lo escriba sin pasar por la especificación.
/// </para>
/// </summary>
public class NadieBorraAdjuntosPorSuCuenta
{
    /// <summary>Quien puede retirar objetos del almacén, por nombre de archivo.</summary>
    private static readonly string[] Permitidos =
    [
        "DeleteAttachmentCommand.cs",          // una persona borra un adjunto (R8)
        "DocumentCommands.cs",                 // una persona descarta un borrador con sus soportes (R10)
        "UploadAttachmentCommand.cs",          // la base no registró la subida: el objeto no llegó a existir
        "ConfirmarSubidaDeAdjuntoCommand.cs",  // la subida no coincide con lo autorizado (FR-001, opción A)
    ];

    /// <summary>Un archivo que usa el almacén y llama a su DeleteAsync.</summary>
    private static readonly Regex UsaElAlmacen = new(@"\bIBlobStore\b", RegexOptions.Compiled);
    private static readonly Regex Retira = new(@"\.DeleteAsync\s*\(", RegexOptions.Compiled);

    /// <summary>Las implementaciones del almacén definen DeleteAsync; no lo llaman.</summary>
    private static string Implementaciones => Path.Combine("src", "Infrastructure", "IngenIA365ERP.Storage") + Path.DirectorySeparatorChar;

    /// <summary>La interfaz lo declara.</summary>
    private static string Contrato => Path.Combine("src", "Core", "IngenIA365ERP.Application", "Common", "Interfaces", "Storage") + Path.DirectorySeparatorChar;

    internal static bool RetiraObjetos(string fuente) => UsaElAlmacen.IsMatch(fuente) && Retira.IsMatch(fuente);

    [Fact]
    public void Solo_los_actos_de_una_persona_retiran_objetos_del_almacen()
    {
        var raiz = RepoPath.FindRepoRoot();
        var infractores = RepoPath.ProductionCSharpFiles()
            .Select(f => (Ruta: f, Relativa: Path.GetRelativePath(raiz, f)))
            .Where(x => !x.Relativa.StartsWith(Implementaciones, StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.Relativa.StartsWith(Contrato, StringComparison.OrdinalIgnoreCase))
            .Where(x => !Permitidos.Contains(Path.GetFileName(x.Ruta), StringComparer.OrdinalIgnoreCase))
            .Where(x => RetiraObjetos(File.ReadAllText(x.Ruta)))
            .Select(x => x.Relativa)
            .ToList();

        Assert.True(infractores.Count == 0,
            "Archivos que retiran objetos del almacén de adjuntos fuera de los actos permitidos (feature 011, " +
            "FR-001: nada se borra solo; la única purga automática es la del ciclo de vida del bucket sobre lo " +
            "que una persona ya borró). Si es un acto nuevo de una persona, especificalo y agregalo a la lista:\n  " +
            string.Join("\n  ", infractores));
    }

    [Fact]
    public void Los_permitidos_de_hoy_siguen_siendo_los_que_retiran()
    {
        // Si uno deja de llamar a DeleteAsync, la lista miente: sobra un permiso que nadie usa.
        var retiran = RepoPath.ProductionCSharpFiles()
            .Where(f => Permitidos.Contains(Path.GetFileName(f), StringComparer.OrdinalIgnoreCase))
            .Where(f => RetiraObjetos(File.ReadAllText(f)))
            .Select(Path.GetFileName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Si no, la prueba de arriba pasaría aunque el detector no viera nada.
        foreach (var archivo in new[] { "DeleteAttachmentCommand.cs", "DocumentCommands.cs", "UploadAttachmentCommand.cs" })
            Assert.True(retiran.Contains(archivo), $"{archivo} está en la lista pero el detector no lo ve retirar objetos.");
    }

    [Fact]
    public void El_detector_ve_un_llamador_nuevo()
    {
        const string limpiezaNocturna = """
            public sealed class PurgarAdjuntosViejosJob(IApplicationDbContext db, IBlobStore store)
            {
                public async Task RunAsync(CancellationToken ct)
                {
                    foreach (var a in db.Attachments.Where(x => x.IsDeleted))
                        await store.DeleteAsync(new BlobReference(a.StoragePath), ct);
                }
            }
            """;
        const string otroAlmacen = "public class Salir(IRefreshTokenStore s) { Task X() => s.DeleteAsync(\"h\", default); }";

        Assert.True(RetiraObjetos(limpiezaNocturna));
        Assert.False(RetiraObjetos(otroAlmacen), "borrar un token de sesión no es borrar un adjunto");
    }
}
