using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T002 (decisiones-transversales T16, §2.18), FR-038: los consecutivos son por tipo de
/// documento y prefijo y los asigna <b>un solo mecanismo</b>, el numerador, que incrementa la fila
/// de numeración (<c>INV_DocumentSequences.NextValue</c> o la resolución DIAN
/// <c>LastIssuedNumber</c>) dentro de la transacción de confirmación. Otro código que la toque
/// produce saltos o números repetidos.
///
/// <para>
/// La base de inventario (fase 3, T117) llenó <see cref="PropiedadesDeNumeracion"/> con <c>NextValue</c> y
/// <c>LastIssuedNumber</c> (la de I4 aún no existe: si alguien la escribe antes, falla igual) y agregó las reglas
/// del número del documento y del nombre <c>NextNumber</c>; <see cref="NumeradoresAutorizados"/> nombra los archivos
/// que sí pueden escribirlas.
/// </para>
/// </summary>
public class SoloElNumeradorNumera
{
    /// <summary>Propiedades de las filas de numeración. Las agrega el bloque que crea cada una.</summary>
    private static readonly string[] PropiedadesDeNumeracion =
    [
        // Base de inventario, fase 3 (T134, T139): el consecutivo de INV_DocumentSequences.
        "NextValue",
        // Entrega I4 (US8): COR_DianNumberingResolutions.
        "LastIssuedNumber",
    ];

    /// <summary>Nombres de archivo (sin ruta) que pueden escribirlas.</summary>
    private static readonly string[] NumeradoresAutorizados = ["Numerador.cs", "NumeradorFiscal.cs"];

    /// <summary>
    /// Carpetas (fragmentos de ruta) del comercio donde <c>Number</c> es el número de un documento de inventario y sólo lo
    /// asigna el numerador. Fuera de ellas <c>Number</c> es de otros módulos (el comprobante contable lo numera su
    /// propio <c>AccountingPoster</c>, vigilado por <c>NingunModuloEscribeMovimientosFueraDelContrato</c>).
    /// </summary>
    private static readonly string[] CarpetasDelComercio =
    [
        "/Inventory/", "/Sales/", "/ElectronicInvoicing/", "/Purchasing/", "/Pos/",
    ];

    private static IEnumerable<string> ArchivosDelComercio() =>
        RepoPath.ProductionCSharpFiles()
            .Where(f => CarpetasDelComercio.Any(c => f.Replace(Path.DirectorySeparatorChar, '/').Contains(c, StringComparison.Ordinal)));

    [Fact]
    public void En_el_comercio_solo_el_numerador_asigna_el_numero_del_documento()
    {
        var root = RepoPath.FindRepoRoot();
        var escritura = new Regex(@"\.Number\s*(=(?!=)|\+=|-=|\+\+|--)", RegexOptions.Compiled);
        var infractores = ArchivosDelComercio()
            .Where(f => !NumeradoresAutorizados.Contains(Path.GetFileName(f), StringComparer.Ordinal))
            .Where(f => escritura.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(root, f))
            .ToList();

        Assert.True(infractores.Count == 0,
            "Número de documento del comercio asignado fuera del numerador (FR-038, T16):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Ninguna_propiedad_del_comercio_se_llama_NextNumber()
    {
        var root = RepoPath.FindRepoRoot();
        var declaracion = new Regex(@"\bNextNumber\s*\{", RegexOptions.Compiled);
        var infractores = ArchivosDelComercio()
            .Where(f => declaracion.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(root, f))
            .ToList();

        Assert.True(infractores.Count == 0,
            "Propiedades NextNumber en el comercio: se llaman NextValue o LastIssuedNumber (§2.1):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void Solo_el_numerador_escribe_los_consecutivos()
    {
        var root = RepoPath.FindRepoRoot();
        var fuentes = RepoPath.ProductionCSharpFiles().Select(f => (Archivo: f, Texto: File.ReadAllText(f))).ToList();
        var infractores = new List<string>();

        foreach (var propiedad in PropiedadesDeNumeracion)
        {
            // Asignación, suma compuesta o incremento sobre la propiedad (no su declaración ni su lectura).
            var escritura = new Regex($@"\.{Regex.Escape(propiedad)}\s*(=(?!=)|\+=|-=|\+\+|--)|(\+\+|--)\s*\w+\.{Regex.Escape(propiedad)}\b", RegexOptions.Compiled);
            foreach (var (archivo, texto) in fuentes)
            {
                if (NumeradoresAutorizados.Contains(Path.GetFileName(archivo), StringComparer.Ordinal)) continue;
                if (escritura.IsMatch(texto))
                    infractores.Add($"{Path.GetRelativePath(root, archivo)}: escribe {propiedad} fuera del numerador");
            }
        }

        Assert.True(infractores.Count == 0,
            "Consecutivos escritos fuera del numerador (FR-038, T16):\n  " + string.Join("\n  ", infractores));
    }
}
