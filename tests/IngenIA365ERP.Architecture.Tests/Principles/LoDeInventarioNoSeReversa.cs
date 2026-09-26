using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T002 (decisiones-transversales T29, T18, §2.18), FR-002 y FR-008: un documento de
/// inventario confirmado no se reversa ni se edita: se corrige con un documento <b>nuevo</b>
/// (anulación, nota crédito o débito, nota de ajuste, ajuste de inventario) que deja su propio
/// rastro. La reversa en espejo es de Contabilidad (FR-038 de la 009) y la hace el módulo dueño
/// del comprobante, no Inventario.
///
/// <para>
/// Esqueleto del Setup: <see cref="CarpetasDeInventario"/> (relativas a <c>src/</c>) empieza vacía y
/// la prueba afirma la regla sobre cada elemento; con la lista vacía pasa porque no hay nada que
/// violar, no por un <c>return</c> temprano. La llena la base de inventario (fase 3) con las
/// carpetas del módulo nuevo; una carpeta que todavía no existe cuenta como vacía.
/// </para>
/// </summary>
public class LoDeInventarioNoSeReversa
{
    /// <summary>Carpetas relativas a <c>src/</c> del módulo comercial. Las agrega la base de inventario.</summary>
    private static readonly string[] CarpetasDeInventario = [];

    /// <summary>Un identificador de reversa: comando, método o propiedad (no un comentario).</summary>
    private static readonly Regex Reversa = new(@"\bRevers\w*\s*[(<{=]|\b(class|record)\s+\w*Revers\w*", RegexOptions.Compiled);

    [Fact]
    public void El_modulo_comercial_no_reversa_documentos()
    {
        var root = RepoPath.FindRepoRoot();
        var infractores = new List<string>();

        foreach (var carpeta in CarpetasDeInventario)
        {
            var ruta = Path.Combine(root, "src", carpeta);
            if (!Directory.Exists(ruta)) continue; // carpeta aún no creada: no tiene nada que violar

            foreach (var archivo in Directory.EnumerateFiles(ruta, "*.cs", SearchOption.AllDirectories))
            {
                var lineas = File.ReadAllLines(archivo);
                for (var i = 0; i < lineas.Length; i++)
                {
                    var linea = lineas[i].TrimStart();
                    if (linea.StartsWith("//", StringComparison.Ordinal) || linea.StartsWith("*", StringComparison.Ordinal)) continue;
                    if (Reversa.IsMatch(linea))
                        infractores.Add($"{Path.GetRelativePath(root, archivo)}:{i + 1}: {linea}");
                }
            }
        }

        Assert.True(infractores.Count == 0,
            "Reversas en el módulo comercial: se corrige con un documento nuevo (FR-002, T29):\n  " + string.Join("\n  ", infractores));
    }

    /// <summary>
    /// Carpetas (relativas a <c>src/Core/IngenIA365ERP.Application</c>) que no pueden pedir la reversa contable: Inventario y el
    /// consumidor contable de sus mensajes (T445; contracts/contabilidad.md §6).
    /// </summary>
    private static readonly string[] CarpetasSinReversaContable = ["Inventory", Path.Combine("Accounting", "Inventory")];

    [Fact]
    public void Ni_Inventario_ni_su_consumidor_contable_piden_la_reversa_del_comprobante()
    {
        var root = RepoPath.FindRepoRoot();
        var llamada = new Regex(@"\bPrepareReversalAsync\b", RegexOptions.Compiled);
        var infractores = new List<string>();
        var revisados = 0;

        foreach (var carpeta in CarpetasSinReversaContable)
        {
            var ruta = Path.Combine(root, "src", "Core", "IngenIA365ERP.Application", carpeta);
            Assert.True(Directory.Exists(ruta), $"No existe {ruta}: si se movió, actualizá CarpetasSinReversaContable.");
            foreach (var archivo in Directory.EnumerateFiles(ruta, "*.cs", SearchOption.AllDirectories))
            {
                revisados++;
                if (llamada.IsMatch(FuenteSinComentarios.Leer(archivo)))
                    infractores.Add(Path.GetRelativePath(root, archivo));
            }
        }

        Assert.True(revisados > 0, "No se revisó ningún archivo: ¿cambió la estructura de carpetas?");
        Assert.True(infractores.Count == 0,
            "Inventario corrige con un comprobante nuevo, nunca con la reversa en espejo (T29, contabilidad.md §6):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void La_reversa_contable_rechaza_lo_de_Inventario()
    {
        // La otra punta (T29): aunque alguien la pidiera, AccountingPoster.PrepareReversalAsync responde
        // Accounting.Document.InventoryCorrectsWithNewVoucher para un original o una petición de origen INV, y lo hace antes
        // que cualquier otra validación.
        var root = RepoPath.FindRepoRoot();
        var archivo = Path.Combine(root, "src", "Core", "IngenIA365ERP.Application", "Accounting", "Posting", "AccountingPoster.cs");
        Assert.True(File.Exists(archivo), $"No existe {archivo}.");

        var texto = FuenteSinComentarios.Leer(archivo);
        var inicio = texto.IndexOf("Task<Result<AccountingDocument>> PrepareReversalAsync(", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "AccountingPoster ya no declara PrepareReversalAsync.");
        var cuerpo = texto[inicio..];
        var siguiente = Regex.Match(cuerpo, @"\n    (public|private|internal)\s", RegexOptions.None, TimeSpan.FromSeconds(5));
        if (siguiente.Success) cuerpo = cuerpo[..siguiente.Index];

        Assert.Contains("ModuloContable.Inventario", cuerpo, StringComparison.Ordinal);
        Assert.Contains("original.OriginModule", cuerpo, StringComparison.Ordinal);
        Assert.Contains("origin.Module", cuerpo, StringComparison.Ordinal);
        var guarda = cuerpo.IndexOf("AccountingErrors.DocumentInventoryCorrectsWithNewVoucher", StringComparison.Ordinal);
        Assert.True(guarda >= 0, "PrepareReversalAsync no responde Accounting.Document.InventoryCorrectsWithNewVoucher.");
        var motivo = cuerpo.IndexOf("AccountingErrors.ReasonRequired", StringComparison.Ordinal);
        Assert.True(motivo < 0 || guarda < motivo, "La guarda de Inventario va primera en PrepareReversalAsync.");
    }
}
