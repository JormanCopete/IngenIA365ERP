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
}
