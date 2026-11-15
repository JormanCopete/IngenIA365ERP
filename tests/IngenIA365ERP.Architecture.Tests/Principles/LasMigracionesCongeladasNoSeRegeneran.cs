using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using IngenIA365ERP.Architecture.Tests.Helpers;
using Microsoft.EntityFrameworkCore.Migrations;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Lo que ya llegó a una base no se regenera: todo cambio de esquema es una migración nueva.
///
/// <para>
/// Historia. Desde T0802 (013, 2026-10-15) y T225 (014, 2026-10-19), y con la 015 el 2026-10-27, esta prueba guardaba la
/// huella de 37 pares de migraciones del árbol operativo. La <b>consolidación del 2026-11-15</b> absorbió esas 37 y las otras
/// 53 en <c>EsquemaBase</c>, una por motor: sus archivos salieron del compilado y siguen en git (la lista está en
/// <c>EsquemaBase.Absorbidas.cs</c>). Desde entonces lo congelado es la base: la migración, su Designer, sus complementos
/// (el SQL a mano que el modelo no conoce) y la lista de absorbidas, que es lo que reconoce a una base vieja para marcarla.
/// Cambiar cualquiera de los cuatro después de desplegarla haría que dos bases con la misma fila de historial tuvieran
/// esquemas distintos.
/// </para>
///
/// <para>La huella es SHA-256 del texto sin BOM, con saltos de línea normalizados a LF y sin blancos al final, para que no
/// dependa de <c>core.autocrlf</c>. Una corrección legítima antes de promoverla fuera de local exige actualizar la huella
/// aquí, a propósito y con su anotación.</para>
/// </summary>
public class LasMigracionesCongeladasNoSeRegeneran
{
    private static readonly IReadOnlyDictionary<string, string> Huellas = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Consolidación de migraciones (2026-11-15, día 0): la base de cada motor y lo que la acompaña.
        ["PostgreSql/20261115190150_EsquemaBase.Designer"] = "9baaf9f25b731d464784c76e1abd12cc6ff5455d977607c81d8c07aeb18bafd2",
        ["PostgreSql/20261115190150_EsquemaBase"] = "9ad26534a7dc4aff11b0602a70d6a2873f15a97544440162bff06ac6432b35ea",
        ["PostgreSql/EsquemaBase.Absorbidas"] = "0c55fdbb79bb80424e027729d547195bfe506cb21db6bc05285a56ec58cfa8ac",
        ["PostgreSql/EsquemaBase.Complementos"] = "a2141eec5667f8bb8fac72619be0075a8d0f8b92d8eb1521c8baff2117527262",
        ["SqlServer/20261115190204_EsquemaBase.Designer"] = "9c03ec61ccb1d005ac718a617db5d60aa0f5b307f9c59a031ca595d325e8770d",
        ["SqlServer/20261115190204_EsquemaBase"] = "a3228999fdad87a4d367c2e18f54781547dbebc03dd10bd1469a990383086445",
        ["SqlServer/EsquemaBase.Absorbidas"] = "ad810695d0e08962dd2ef39f2f36f1462bfbe60c5abe25c78311db33a13a7824",
        ["SqlServer/EsquemaBase.Complementos"] = "0fb25e66a7d92d865163c6b39a3180cb6fa4923e6b9eaea9bb6d077f717e45d5",
    };

    private static readonly Assembly PostgreSql =
        typeof(Persistence.Migrations.PostgreSql.Application.MigracionesAbsorbidasPorEsquemaBase).Assembly;

    private static readonly Assembly SqlServer =
        typeof(Persistence.Migrations.SqlServer.Application.MigracionesAbsorbidasPorEsquemaBase).Assembly;

    public static TheoryData<string> Congeladas()
    {
        var datos = new TheoryData<string>();
        foreach (var clave in Huellas.Keys.Order(StringComparer.Ordinal)) datos.Add(clave);
        return datos;
    }

    [Theory]
    [MemberData(nameof(Congeladas))]
    public void La_migracion_congelada_no_cambio(string clave)
    {
        var (motor, nombre) = (clave[..clave.IndexOf('/')], clave[(clave.IndexOf('/') + 1)..]);
        var archivo = Path.Combine(RepoPath.FindRepoRoot(), "src", "Infrastructure",
            $"IngenIA365ERP.Persistence.Migrations.{motor}", "Application", nombre + ".cs");

        Assert.True(File.Exists(archivo), $"El archivo congelado {clave} desapareció: una congelada no se borra ni se regenera con otro sello.");

        var texto = File.ReadAllText(archivo, Encoding.UTF8).Replace("\r\n", "\n").TrimEnd();
        var huella = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));

        Assert.True(Huellas[clave] == huella,
            $"El archivo congelado {clave} cambió. Todo cambio de esquema es una migración nueva, nunca una regeneración de la base.");
    }

    [Fact]
    public void Ambos_motores_congelan_lo_mismo()
    {
        static IEnumerable<string> Logicas(string motor) => Huellas.Keys
            .Where(k => k.StartsWith(motor + "/", StringComparison.Ordinal))
            .Select(k => k[(k.IndexOf('/') + 1)..])
            .Select(n => n.Length > 15 && char.IsAsciiDigit(n[0]) && n[14] == '_' ? n[15..] : n)
            .Order(StringComparer.Ordinal);

        Assert.Equal(Logicas("PostgreSql"), Logicas("SqlServer"));
        Assert.Contains("EsquemaBase", Logicas("PostgreSql"));
    }

    /// <summary>Las dos listas de absorbidas nombran las mismas 90 migraciones lógicas, en el mismo orden.</summary>
    [Fact]
    public void Las_absorbidas_son_las_mismas_en_los_dos_motores()
    {
        var pg = new Persistence.Migrations.PostgreSql.Application.MigracionesAbsorbidasPorEsquemaBase().Absorbidas;
        var ss = new Persistence.Migrations.SqlServer.Application.MigracionesAbsorbidasPorEsquemaBase().Absorbidas;

        Assert.Equal(90, pg.Count);
        Assert.Equal(pg.Select(m => m[15..]), ss.Select(m => m[15..]));
        Assert.Equal(pg.Order(StringComparer.Ordinal), pg);
        Assert.Equal(ss.Order(StringComparer.Ordinal), ss);
    }

    /// <summary>
    /// Ninguna absorbida vuelve al compilado, y ninguna migración del árbol operativo se ordena antes de la base. EF aplica
    /// las pendientes por orden de identificador: una migración fechada antes que la base —una rama que nació antes de la
    /// consolidación y se integra después— correría primero en una cooperativa nueva, sobre una base sin tablas. Se regenera
    /// con marca de hoy antes de integrarla.
    /// </summary>
    [Theory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public void Ninguna_migracion_operativa_se_ordena_antes_de_la_base(string motor)
    {
        var ensamblado = motor == "PostgreSql" ? PostgreSql : SqlServer;
        var consolidacion = motor == "PostgreSql"
            ? (Persistence.Initialization.MigracionesAbsorbidas)new Persistence.Migrations.PostgreSql.Application.MigracionesAbsorbidasPorEsquemaBase()
            : new Persistence.Migrations.SqlServer.Application.MigracionesAbsorbidasPorEsquemaBase();

        var operativas = ensamblado.GetTypes()
            .Where(t => typeof(Migration).IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => t.GetCustomAttribute<Microsoft.EntityFrameworkCore.Infrastructure.DbContextAttribute>()?.ContextType
                        == typeof(Persistence.DbContext.ApplicationDbContext))
            .Select(t => t.GetCustomAttribute<MigrationAttribute>()!.Id)
            .ToList();

        Assert.Contains(consolidacion.IdDeLaBase, operativas);
        Assert.Empty(operativas.Intersect(consolidacion.Absorbidas));
        Assert.Empty(operativas.Where(id => string.CompareOrdinal(id, consolidacion.IdDeLaBase) < 0));
    }
}
