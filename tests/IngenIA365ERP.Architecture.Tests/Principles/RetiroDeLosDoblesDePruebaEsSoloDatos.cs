using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 015, T0310 (FR-223; data-model §25 #2): el par <c>RetiroDeLosDoblesDePrueba</c> es <b>sólo datos</b>. En los dos motores su <c>Up()</c> no tiene
/// operaciones de esquema —sólo <c>migrationBuilder.Sql</c>—, ese SQL sólo hace <c>UPDATE</c> de <c>ValidTo</c> sobre <c>COR_IntegrationParticipants</c> y
/// <c>COR_IntegrationParticipantAdoptions</c>, nunca <c>DELETE</c>, <c>DROP</c>, <c>ALTER</c> ni <c>INSERT</c>, cierra sólo lo vigente (<c>ValidTo IS NULL</c>:
/// idempotente) y sólo lo que es un doble (<c>IsTestDouble</c> o la clase <c>Test</c>); y su <c>Down()</c> no reabre nada. Que ningún par congelado cambió lo
/// vigila <see cref="LasMigracionesCongeladasNoSeRegeneran"/> y la paridad, <c>check-migration-parity.ps1</c>.
/// </summary>
public class RetiroDeLosDoblesDePruebaEsSoloDatos
{
    private static IReadOnlyList<string> Pares() =>
        RepoPath.ProductionCSharpFiles()
            .Where(f => f.EndsWith("_RetiroDeLosDoblesDePrueba.cs", StringComparison.Ordinal))
            .ToList();

    private static string Metodo(string fuente, string nombre)
    {
        var inicio = fuente.IndexOf($"protected override void {nombre}(", StringComparison.Ordinal);
        Assert.True(inicio >= 0, $"falta {nombre}()");
        var siguiente = fuente.IndexOf("protected override void ", inicio + 10, StringComparison.Ordinal);
        return siguiente < 0 ? fuente[inicio..] : fuente[inicio..siguiente];
    }

    [Fact]
    public void El_par_existe_en_los_dos_motores()
    {
        var pares = Pares();
        Assert.Equal(2, pares.Count);
        Assert.Contains(pares, p => p.Replace('\\', '/').Contains("Migrations.PostgreSql/", StringComparison.Ordinal));
        Assert.Contains(pares, p => p.Replace('\\', '/').Contains("Migrations.SqlServer/", StringComparison.Ordinal));
    }

    [Fact]
    public void Up_solo_cierra_las_vigencias_de_los_dobles_y_Down_no_reabre_nada()
    {
        foreach (var par in Pares())
        {
            var fuente = File.ReadAllText(par);
            var up = Metodo(fuente, "Up");
            var sinSql = Regex.Replace(up, @"migrationBuilder\.Sql\(@""(?:[^""]|"""")*""\);", string.Empty, RegexOptions.Singleline);
            Assert.DoesNotMatch(@"migrationBuilder\.(?!Sql\b)[A-Z]\w*\(", sinSql);

            var sql = string.Join("\n", Regex.Matches(up, @"migrationBuilder\.Sql\(@""((?:[^""]|"""")*)""\);", RegexOptions.Singleline).Select(m => m.Groups[1].Value));
            Assert.False(string.IsNullOrWhiteSpace(sql), $"{Path.GetFileName(par)}: el Up() no tiene el UPDATE");
            Assert.DoesNotMatch(@"(?i)\b(DELETE|DROP|ALTER|INSERT|TRUNCATE|CREATE)\b", sql);
            var actualizaciones = Regex.Matches(sql, @"(?i)\bUPDATE\b").Count;
            Assert.Equal(2, actualizaciones);
            Assert.Matches(@"(?i)COR_IntegrationParticipants", sql);
            Assert.Matches(@"(?i)COR_IntegrationParticipantAdoptions", sql);
            // La cláusula SET (hasta FROM o WHERE) sólo escribe ValidTo.
            Assert.DoesNotMatch(@"(?i)\bSET\s+(?:(?!\b(?:FROM|WHERE)\b)[^;])*\b(IsEnabled|Reason|IsDeleted|IsTestDouble|Class|ValidFrom)\b\s*=", sql);
            Assert.True(Regex.Matches(sql, @"(?i)ValidTo[\]""]*\s+IS\s+NULL").Count >= 3, "cierra sólo lo vigente: idempotente");
            Assert.True(Regex.Matches(sql, @"(?i)IsTestDouble").Count >= 2, "sólo lo que es un doble");

            var down = Metodo(fuente, "Down");
            Assert.DoesNotMatch(@"migrationBuilder\.", down);
        }
    }
}
