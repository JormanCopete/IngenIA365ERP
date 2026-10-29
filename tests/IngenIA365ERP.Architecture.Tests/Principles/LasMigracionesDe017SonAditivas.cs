using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 017 (T023): las dos migraciones de la feature, en los dos motores, son aditivas y su reversa sólo
/// retira lo que ellas mismas agregaron. Un <c>Down</c> que borrara una columna o una tabla anterior al
/// despliegue destruiría datos de antes de la 017 —el correo, la cédula de una persona— justo cuando se está
/// volviendo atrás por un problema, que es el peor momento para perderlos. El límite conocido de la reversa
/// (CC y CE del mismo número ya conviviendo) lo cuida la propia migración y lo prueba la e2e T091.
/// </summary>
public class LasMigracionesDe017SonAditivas
{
    private static readonly string[] Nombres = ["_IdentidadConDocumento.cs", "_UsuarioDeLaPersona.cs"];

    private static readonly Regex Columna = new(
        @"\b(?<op>AddColumn<[^>]+>|DropColumn)\(\s*name:\s*""(?<col>[^""]+)""[\s\S]*?table:\s*""(?<tabla>[^""]+)""",
        RegexOptions.Compiled);

    private static readonly Regex Tabla = new(
        @"\b(?<op>CreateTable|DropTable)\(\s*name:\s*""(?<tabla>[^""]+)""", RegexOptions.Compiled);

    private static IEnumerable<string> Migraciones()
    {
        var raiz = Path.Combine(RepoPath.FindRepoRoot(), "src", "Infrastructure");
        return Directory.EnumerateFiles(raiz, "*.cs", SearchOption.AllDirectories)
            .Where(f => f.Contains("Persistence.Migrations.", StringComparison.Ordinal))
            .Where(f => Nombres.Any(n => f.EndsWith(n, StringComparison.Ordinal)))
            .Where(f => !f.EndsWith(".Designer.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void Hay_las_dos_migraciones_en_los_dos_motores() =>
        Assert.Equal(4, Migraciones().Count());

    [Fact]
    public void El_Down_solo_retira_lo_que_agrego_el_Up()
    {
        var problemas = new List<string>();
        foreach (var archivo in Migraciones())
        {
            var texto = File.ReadAllText(archivo);
            var corte = texto.IndexOf("protected override void Down", StringComparison.Ordinal);
            Assert.True(corte > 0, $"{Path.GetFileName(archivo)} no tiene Down.");
            var up = texto[..corte];
            var down = texto[corte..];

            var columnasAgregadas = Columna.Matches(up).Where(m => m.Groups["op"].Value.StartsWith("AddColumn", StringComparison.Ordinal))
                .Select(m => (m.Groups["tabla"].Value, m.Groups["col"].Value)).ToHashSet();
            var tablasCreadas = Tabla.Matches(up).Where(m => m.Groups["op"].Value == "CreateTable")
                .Select(m => m.Groups["tabla"].Value).ToHashSet();

            foreach (Match m in Columna.Matches(up).Where(m => m.Groups["op"].Value == "DropColumn"))
                problemas.Add($"{Path.GetFileName(archivo)}: el Up borra {m.Groups["tabla"].Value}.{m.Groups["col"].Value}");
            foreach (Match m in Tabla.Matches(up).Where(m => m.Groups["op"].Value == "DropTable"))
                problemas.Add($"{Path.GetFileName(archivo)}: el Up borra la tabla {m.Groups["tabla"].Value}");

            foreach (Match m in Columna.Matches(down).Where(m => m.Groups["op"].Value == "DropColumn"))
            {
                var c = (m.Groups["tabla"].Value, m.Groups["col"].Value);
                if (!columnasAgregadas.Contains(c))
                    problemas.Add($"{Path.GetFileName(archivo)}: el Down borra {c.Item1}.{c.Item2}, que no agregó el Up");
            }
            foreach (Match m in Tabla.Matches(down).Where(m => m.Groups["op"].Value == "DropTable"))
                if (!tablasCreadas.Contains(m.Groups["tabla"].Value))
                    problemas.Add($"{Path.GetFileName(archivo)}: el Down borra la tabla {m.Groups["tabla"].Value}, que no creó el Up");
        }

        Assert.True(problemas.Count == 0, "Las migraciones de la 017 deben ser aditivas:\n  " + string.Join("\n  ", problemas));
    }
}
