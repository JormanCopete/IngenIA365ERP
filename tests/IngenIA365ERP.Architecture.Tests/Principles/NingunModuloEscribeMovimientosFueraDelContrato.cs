using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 009 (FR-036, FR-039): el contrato de contabilización es el <b>único</b> camino por el
/// que un movimiento entra al libro. Hasta la 009 había dieciséis escritores repartidos por
/// Nómina, Cartera, Inventario, Tesorería y CDT, doce de ellos con cabecera y sin líneas, cada
/// uno con su propia numeración y sin ninguna regla de cuenta. Es una prueba sobre el fuente
/// porque un <c>Add</c> nuevo fuera del contrato no rompe ninguna prueba de unidad: sólo abre
/// otra vez la puerta.
/// </summary>
public class NingunModuloEscribeMovimientosFueraDelContrato
{
    private static readonly Regex Escritura = new(
        @"\b(AccountingDocuments|JournalEntries)\s*\.\s*(Add|AddRange|AddAsync|AddRangeAsync)\s*\(|\bnew\s+(AccountingDocument|JournalEntry)\s*[({]",
        RegexOptions.Compiled);

    private static readonly Regex Numeracion = new(@"\.NextNumber\s*(=|\+\+|\+=)", RegexOptions.Compiled);

    private static string Contrato => Path.Combine("src", "Core", "IngenIA365ERP.Application", "Accounting", "Posting") + Path.DirectorySeparatorChar;

    /// <summary>Los seeders crean el tipo con su consecutivo inicial; no numeran documentos.</summary>
    private static string Semillas => Path.Combine("src", "Infrastructure", "IngenIA365ERP.Persistence", "Seeding") + Path.DirectorySeparatorChar;

    [Fact]
    public void Solo_el_contrato_agrega_documentos_y_lineas()
    {
        var raiz = RepoPath.FindRepoRoot();
        var infractores = RepoPath.ProductionCSharpFiles()
            .Select(f => (Ruta: f, Relativa: Path.GetRelativePath(raiz, f)))
            .Where(x => !x.Relativa.StartsWith(Contrato, StringComparison.OrdinalIgnoreCase))
            .Where(x => Escritura.IsMatch(File.ReadAllText(x.Ruta)))
            .Select(x => x.Relativa)
            .ToList();

        Assert.True(infractores.Count == 0,
            "Archivos que agregan documentos o líneas contables fuera de Application/Accounting/Posting " +
            "(feature 009, FR-036): usá AccountingPoster.PrepareAsync / PrepareReversalAsync.\n  " +
            string.Join("\n  ", infractores));
    }

    /// <summary>
    /// Feature 013 (T0126; T96, §2.1): los cinco escritores heredados que contabilizaban el crédito con origen <c>CAR</c> se
    /// retiraron con la cartera heredada (T0191) y no vuelven: la Cartera nueva contabiliza por el contrato
    /// (<c>IContabilidadParaCartera</c>), nunca por un comando propio que arme comprobantes.
    /// </summary>
    [Fact]
    public void Los_escritores_heredados_de_credito_no_vuelven()
    {
        var heredados = new Regex(@"\b(DisburseLoanCommand|RecaudoDeCredito|AccrueInterestCommand|ClassifyPortfolioCommand|ProcessPayrollDeductionCommand)\b",
            RegexOptions.Compiled);
        var raiz = RepoPath.FindRepoRoot();
        var infractores = RepoPath.ProductionCSharpFiles()
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}IngenIA365ERP.Persistence.Migrations.", StringComparison.OrdinalIgnoreCase))
            .Where(f => heredados.IsMatch(FuenteSinComentarios.Leer(f)))
            .Select(f => Path.GetRelativePath(raiz, f))
            .ToList();
        Assert.True(infractores.Count == 0, "Vuelve un escritor heredado del crédito (T96):\n  " + string.Join("\n  ", infractores));
    }

    /// <summary>Feature 013 (T0126, §2.1): el único consecutivo contable sigue siendo <c>VoucherType.NextNumber</c>.</summary>
    [Fact]
    public void Ninguna_propiedad_nueva_se_llama_NextNumber()
    {
        var declaracion = new Regex(@"\bpublic\s+[\w<>?]+\s+NextNumber\s*\{", RegexOptions.Compiled);
        var raiz = RepoPath.FindRepoRoot();
        var declaran = RepoPath.ProductionCSharpFiles()
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}IngenIA365ERP.Persistence.Migrations.", StringComparison.OrdinalIgnoreCase))
            .Where(f => declaracion.IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetFileName(f))
            .ToList();
        Assert.Equal(["VoucherType.cs"], declaran);
    }

    [Fact]
    public void Solo_el_contrato_numera()
    {
        var raiz = RepoPath.FindRepoRoot();
        var infractores = RepoPath.ProductionCSharpFiles()
            .Select(f => (Ruta: f, Relativa: Path.GetRelativePath(raiz, f)))
            .Where(x => !x.Relativa.StartsWith(Contrato, StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.Relativa.StartsWith(Semillas, StringComparison.OrdinalIgnoreCase))
            .Where(x => Numeracion.IsMatch(File.ReadAllText(x.Ruta)))
            .Select(x => x.Relativa)
            .ToList();

        Assert.True(infractores.Count == 0,
            "Archivos que tocan VoucherType.NextNumber fuera del contrato (R3: el consecutivo se toma " +
            "en la misma escritura que contabiliza, y su RowVersion convierte una carrera en reintento):\n  " +
            string.Join("\n  ", infractores));
    }

    /// <summary>
    /// Feature 013, US3 (T0428): el desembolso de Cartera no arma comprobantes ni toma el consecutivo contable: emite
    /// <c>CreditoDesembolsado</c> y Contabilidad contabiliza; sus números (<c>CR-</c>, <c>DS-</c>, <c>OG-</c>, <c>ARD-</c>) los da
    /// <c>NumeradorDeCartera</c>. La carpeta existe: la regla no vigila un fantasma.
    /// </summary>
    [Fact]
    public void El_desembolso_de_Cartera_no_numera_ni_escribe_comprobantes()
    {
        var carpeta = Path.Combine(RepoPath.FindRepoRoot(), "src", "Core", "IngenIA365ERP.Application", "Lending", "Disbursements");
        Assert.True(Directory.Exists(carpeta), "No existe Application/Lending/Disbursements (US3).");
        var infractores = Directory.EnumerateFiles(carpeta, "*.cs", SearchOption.AllDirectories)
            .Where(f => { var t = File.ReadAllText(f); return Numeracion.IsMatch(t) || Escritura.IsMatch(t); })
            .Select(Path.GetFileName)
            .ToList();
        Assert.True(infractores.Count == 0, "El desembolso toca NextNumber o arma comprobantes:\n  " + string.Join("\n  ", infractores));
    }
}
