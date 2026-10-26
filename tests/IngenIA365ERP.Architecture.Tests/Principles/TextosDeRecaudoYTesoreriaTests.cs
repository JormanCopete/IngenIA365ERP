using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 015, T0004 (FR-162; CH14; memoria del dueño «sin el nombre SOLIDO en el producto»): ningún texto visible de Recaudo ni de la
/// Tesorería nueva nombra al sistema anterior —se dice «lo heredado» y «cifras de referencia»— ni muestra un código técnico
/// (<c>Receipts.Session.NotOpen</c>) como texto. Molde: <see cref="TextosDeCarteraTests"/>.
///
/// <para>
/// Árbol que recorre: las pantallas de <c>Shared/Pages/Recaudo</c> y <c>Shared/Pages/Tesoreria</c> (sin las cuatro heredadas, R-04), los
/// informes <c>API/Reports/DocumentosDe{Recaudo,Tesoreria}Report.cs</c> y los manuales <c>docs/manual/{recaudo,tesoreria}-*.md</c>, todos los
/// que existan. <see cref="Archivos"/>, los archivos de textos de Shared (<c>Services/Recaudo/Textos*.cs</c>), nace vacía en el Setup y la
/// llena cada historia que los cree.
/// </para>
/// </summary>
public class TextosDeRecaudoYTesoreriaTests
{
    /// <summary>Archivos de textos de los dos módulos (relativos a <c>src/</c>).</summary>
    private static readonly string[] Archivos =
    [
        // Fase 2 (T0032): la pantalla de plataforma de las cuentas bancarias de la persona.
        "Presentation/IngenIA365ERP.Shared/Pages/Maestros/CuentasBancariasDePersona.razor",
    ];

    private static readonly string[] Informes =
    [
        "Presentation/IngenIA365ERP.API/Reports/DocumentosDeRecaudoReport.cs",
        "Presentation/IngenIA365ERP.API/Reports/DocumentosDeTesoreriaReport.cs",
    ];

    private static readonly (Regex Patron, string Que)[] Prohibidos =
    [
        (new Regex("SOLIDO", RegexOptions.Compiled | RegexOptions.IgnoreCase), "nombra el sistema anterior"),
        (new Regex("\"(Receipts|Treasury)\\.[A-Z]\\w*\\.[A-Z]\\w*\"", RegexOptions.Compiled), "muestra un código de error como texto"),
    ];

    [Fact]
    public void Las_pantallas_y_los_textos_no_nombran_el_sistema_anterior_ni_muestran_codigos()
    {
        var root = RepoPath.FindRepoRoot();
        var pantallas = ReglaSobreFuente.ArchivosEn(["Presentation/IngenIA365ERP.Shared/Pages/Recaudo", "Presentation/IngenIA365ERP.Shared/Pages/Tesoreria"], "*.razor");
        var informes = Informes.Select(i => Path.Combine(root, "src", i.Replace('/', Path.DirectorySeparatorChar))).Where(File.Exists);
        var archivos = ReglaSobreFuente.ArchivosNombrados(Archivos).Concat(pantallas).Concat(informes).ToList();

        // Las pantallas nombran permisos en sus PermissionGate (Required="Receipts.Session.Open"): eso no es texto visible.
        var infracciones = ReglaSobreFuente.Infracciones(archivos, Prohibidos)
            .Where(i => !i.Contains("Required=", StringComparison.Ordinal))
            .ToList();
        ReglaSobreFuente.SinInfracciones(infracciones, "Un texto de Recaudo o Tesorería nombra el sistema anterior o muestra un código (FR-162)");
    }

    [Fact]
    public void Los_manuales_no_nombran_el_sistema_anterior()
    {
        var root = RepoPath.FindRepoRoot();
        var manual = Path.Combine(root, "docs", "manual");
        var manuales = Directory.Exists(manual)
            ? Directory.EnumerateFiles(manual, "*.md").Where(m => Path.GetFileName(m) is var n
                && (n.StartsWith("recaudo-", StringComparison.Ordinal) || n.StartsWith("tesoreria-", StringComparison.Ordinal)))
            : [];
        var infracciones = manuales.Where(m => Regex.IsMatch(File.ReadAllText(m), "SOLIDO", RegexOptions.IgnoreCase))
            .Select(m => ReglaSobreFuente.Relativo(root, m))
            .ToList();
        ReglaSobreFuente.SinInfracciones(infracciones, "Un manual de Recaudo o Tesorería nombra el sistema anterior (FR-162)");
    }
}
