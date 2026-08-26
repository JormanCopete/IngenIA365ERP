using IngenIA365ERP.Architecture.Tests.Helpers;
using System.Text.RegularExpressions;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Principio XII — una migración EF que borra datos no puede colarse sin que
/// alguien lo haya decidido.
///
/// <para>
/// <c>dotnet ef migrations add</c> no mira la base de datos: compara el modelo
/// compilado contra <c>ApplicationDbContextModelSnapshot.cs</c>. Todo lo que
/// esté en el snapshot y ya no en el modelo se convierte en un <c>Drop*</c>,
/// en silencio y sin marcar la migración como destructiva. Basta con que
/// alguien retire una entidad que creía muerta para que la <b>siguiente</b>
/// migración —la que sea, con cualquier nombre y para cualquier fin— se lleve
/// por delante una tabla en la base de <b>cada cooperativa</b>, porque
/// <c>DbMigrator</c> las aplica a todas.
/// </para>
///
/// <para>
/// Esto ya pasó una vez: <c>20260823000420_SacarTablasAdminDelModeloOperativo</c>
/// lleva dentro 12 <c>DropTable</c> y 5 <c>DropForeignKey</c> generados
/// exactamente así. Ahí fue deliberado. El problema es que, leyendo el archivo,
/// nada distingue «deliberado» de «se coló».
/// </para>
///
/// <para>
/// La prueba sólo mira el <c>Up()</c>. El <c>Down()</c> de toda migración
/// deshace lo que su <c>Up()</c> creó, así que ahí los <c>Drop*</c> son
/// normales y buscarlos daría falsos positivos en todas.
/// </para>
///
/// <para>
/// Para aprobar una migración destructiva, poner en su cabecera el marcador
/// <c>MIGRACION-DESTRUCTIVA-APROBADA</c> con la referencia del backup y del
/// segundo revisor que exige la constitución. El marcador no valida nada por
/// sí solo: obliga a que alguien lo escriba a mano y lo defienda en revisión.
/// </para>
/// </summary>
public class PrincipioXII_MigracionesDestructivas
{
    private static readonly Regex OperacionDestructiva = new(
        @"\b(DropTable|DropColumn|DropForeignKey)\s*\(",
        RegexOptions.Compiled);

    private const string MarcadorDeAprobacion = "MIGRACION-DESTRUCTIVA-APROBADA";

    /// <summary>
    /// Migraciones destructivas anteriores a esta guarda. Se aprobaron en su
    /// momento y ya están aplicadas: exigirles el marcador retroactivamente
    /// sería reescribir historia sin ganar nada. Esta lista NO crece: una
    /// migración destructiva nueva lleva el marcador.
    /// </summary>
    private static readonly HashSet<string> AprobadasAntesDeLaGuarda = new(StringComparer.OrdinalIgnoreCase)
    {
        "20260823000420_SacarTablasAdminDelModeloOperativo.cs",
        "20260823000431_SacarTablasAdminDelModeloOperativo.cs",
    };

    [Fact]
    public void Ninguna_migracion_borra_datos_sin_aprobacion_explicita()
    {
        var infractoras = ArchivosDeMigracion()
            .Where(f => !AprobadasAntesDeLaGuarda.Contains(Path.GetFileName(f)))
            .Select(f => new { Ruta = f, Texto = File.ReadAllText(f) })
            .Where(x => !x.Texto.Contains(MarcadorDeAprobacion, StringComparison.OrdinalIgnoreCase))
            .Select(x => new { x.Ruta, Destructivas = OperacionesDestructivasEnUp(x.Texto) })
            .Where(x => x.Destructivas.Count > 0)
            .Select(x => $"{Path.GetFileName(x.Ruta)} → {string.Join(", ", x.Destructivas.Distinct())}")
            .ToList();

        Assert.True(infractoras.Count == 0,
            "Migraciones que borran datos en Up() sin el marcador " +
            $"'{MarcadorDeAprobacion}' en la cabecera:\n  " +
            string.Join("\n  ", infractoras) +
            "\n\nSi es deliberado, añadí el marcador con la referencia del backup y del " +
            "segundo revisor (Principio XII). Si NO lo es, alguien sacó una entidad del " +
            "modelo EF y esto se va a ejecutar contra la base de cada cooperativa.");
    }

    /// <summary>
    /// Recorta el cuerpo de <c>Up()</c> —desde su firma hasta la de
    /// <c>Down()</c>— y devuelve las operaciones destructivas que encuentra.
    /// Si la migración no declara <c>Down()</c>, se mira desde <c>Up()</c>
    /// hasta el final del archivo.
    /// </summary>
    private static List<string> OperacionesDestructivasEnUp(string texto)
    {
        var inicio = texto.IndexOf("void Up(", StringComparison.Ordinal);
        if (inicio < 0) return [];

        var fin = texto.IndexOf("void Down(", inicio, StringComparison.Ordinal);
        var cuerpo = fin < 0 ? texto[inicio..] : texto[inicio..fin];

        return OperacionDestructiva.Matches(cuerpo)
            .Select(m => m.Groups[1].Value)
            .ToList();
    }

    private static IEnumerable<string> ArchivosDeMigracion()
    {
        var raiz = RepoPath.FindRepoRoot();

        foreach (var proveedor in new[] { "SqlServer", "PostgreSql" })
        {
            var carpeta = Path.Combine(
                raiz, "src", "Infrastructure", $"IngenIA365ERP.Persistence.Migrations.{proveedor}");
            if (!Directory.Exists(carpeta)) continue;

            foreach (var archivo in Directory.EnumerateFiles(carpeta, "*.cs", SearchOption.AllDirectories))
            {
                if (archivo.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)) continue;
                if (archivo.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)) continue;
                // El .Designer y el snapshot describen el modelo, no operaciones.
                if (archivo.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)) continue;
                if (archivo.EndsWith("ModelSnapshot.cs", StringComparison.OrdinalIgnoreCase)) continue;

                yield return archivo;
            }
        }
    }
}
