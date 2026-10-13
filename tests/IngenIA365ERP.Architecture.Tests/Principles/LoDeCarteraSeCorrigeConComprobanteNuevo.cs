using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, Principio XI (DT §2.25, T51, T55, T56; contracts/contabilidad.md §9 y §26): lo que Contabilidad registró por un hecho de
/// Cartera no se edita, no se borra y no se reversa en espejo: se corrige con un comprobante nuevo que nace de otro hecho (el reverso del
/// pago, del desembolso, del cierre). Molde: <see cref="LoDeInventarioNoSeReversa"/>.
///
/// <para>
/// Tres comprobaciones (T0607): (1) el lado contable de Cartera (<c>Application/Accounting/Lending</c>) y lo común del consumidor
/// (<c>Application/Accounting/Integration</c>) no deshacen un contabilizado ni borran movimientos o comprobantes; (2) ni Cartera ni su
/// consumidor contable llaman a <c>AccountingPoster.PrepareReversalAsync</c>; (3) la otra punta: <c>PrepareReversalAsync</c> responde
/// <c>Accounting.Document.LendingCorrectsWithNewVoucher</c> para un original o una petición de origen <c>CAR</c>, antes de <c>ReasonRequired</c>.
/// </para>
/// </summary>
public class LoDeCarteraSeCorrigeConComprobanteNuevo
{
    /// <summary>Carpetas del lado contable de Cartera (relativas a <c>src/</c>).</summary>
    private static readonly string[] Carpetas =
    [
        "Core/IngenIA365ERP.Application/Accounting/Lending",
        "Core/IngenIA365ERP.Application/Accounting/Integration",
    ];

    /// <summary>Archivos exentos (rutas relativas a la raíz, con barras normales).</summary>
    private static readonly string[] Exentos = [];

    private static readonly (Regex Patron, string Que)[] Prohibidos = [
        (new Regex(@"\.IsPosted\s*=\s*false", RegexOptions.Compiled), "deshace un contabilizado"),
        (new Regex(@"\bJournalEntries\.Remove(Range)?\(", RegexOptions.Compiled), "borra movimientos"),
        (new Regex(@"\bDocuments\.Remove(Range)?\(", RegexOptions.Compiled), "borra un comprobante"),
    ];

    [Fact]
    public void El_lado_contable_de_Cartera_no_edita_lo_registrado()
    {
        var archivos = ReglaSobreFuente.ArchivosEn(Carpetas).ToList();
        Assert.True(archivos.Count > 0, "No se encontró el lado contable de Cartera: si se movió, actualizá Carpetas.");
        var infracciones = ReglaSobreFuente.Infracciones(archivos, Prohibidos, relativo => Exentos.Contains(relativo));
        ReglaSobreFuente.SinInfracciones(infracciones, "Lo de Cartera se corrige editando (Principio XI)");
    }

    /// <summary>Carpetas (relativas a <c>src/Core/IngenIA365ERP.Application</c>) que no pueden pedir la reversa contable.</summary>
    private static readonly string[] CarpetasSinReversaContable = ["Lending", Path.Combine("Accounting", "Lending")];

    [Fact]
    public void Ni_Cartera_ni_su_consumidor_contable_piden_la_reversa_del_comprobante()
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
            "Cartera corrige con un comprobante nuevo, nunca con la reversa en espejo (T55, contabilidad.md §9):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void La_reversa_contable_rechaza_lo_de_Cartera()
    {
        var root = RepoPath.FindRepoRoot();
        var archivo = Path.Combine(root, "src", "Core", "IngenIA365ERP.Application", "Accounting", "Posting", "AccountingPoster.cs");
        Assert.True(File.Exists(archivo), $"No existe {archivo}.");

        var texto = FuenteSinComentarios.Leer(archivo);
        var inicio = texto.IndexOf("Task<Result<AccountingDocument>> PrepareReversalAsync(", StringComparison.Ordinal);
        Assert.True(inicio >= 0, "AccountingPoster ya no declara PrepareReversalAsync.");
        var cuerpo = texto[inicio..];
        var siguiente = Regex.Match(cuerpo, @"\n    (public|private|internal)\s", RegexOptions.None, TimeSpan.FromSeconds(5));
        if (siguiente.Success) cuerpo = cuerpo[..siguiente.Index];

        Assert.Contains("ModuloContable.Cartera", cuerpo, StringComparison.Ordinal);
        Assert.Contains("original.OriginModule", cuerpo, StringComparison.Ordinal);
        Assert.Contains("origin.Module", cuerpo, StringComparison.Ordinal);
        var guarda = cuerpo.IndexOf("AccountingErrors.DocumentLendingCorrectsWithNewVoucher", StringComparison.Ordinal);
        Assert.True(guarda >= 0, "PrepareReversalAsync no responde Accounting.Document.LendingCorrectsWithNewVoucher.");
        var motivo = cuerpo.IndexOf("AccountingErrors.ReasonRequired", StringComparison.Ordinal);
        Assert.True(motivo < 0 || guarda < motivo, "La guarda de Cartera va antes de ReasonRequired en PrepareReversalAsync.");
    }
}
