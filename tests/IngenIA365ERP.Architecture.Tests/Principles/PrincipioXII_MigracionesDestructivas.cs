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
    /// <summary>
    /// Operaciones destructivas por la API de migraciones.
    /// </summary>
    private static readonly Regex OperacionDestructiva = new(
        @"\b(DropTable|DropColumn|DropForeignKey)\s*\(",
        RegexOptions.Compiled);

    /// <summary>
    /// Lo mismo escrito como SQL crudo dentro de <c>migrationBuilder.Sql(...)</c>,
    /// que la API de arriba no ve.
    ///
    /// <para>
    /// Deliberadamente NO incluye <c>UPDATE</c>: una migración de datos legítima
    /// actualiza filas, y marcarlas todas convertiría la guarda en ruido que la
    /// gente aprende a saltarse. Queda el hueco de un
    /// <c>UPDATE … SET columna = NULL</c> que vacíe una columna a propósito; eso
    /// se atrapa en revisión, no aquí.
    /// </para>
    /// </summary>
    private static readonly Regex SqlDestructivo = new(
        @"\b(DELETE\s+FROM|TRUNCATE\s+TABLE|TRUNCATE\s+|DROP\s+TABLE|DROP\s+COLUMN)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

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
    /// Feature 013 (T0125; T87, data-model parte 3 §13.3): <c>RetiroDeLaCarteraHeredada</c> es la única migración destructiva de la
    /// feature (las de la 013 nacen desde el 2026-10-06: <c>ValoresNacionales</c>, <c>PlataformaParaCartera</c>,
    /// <c>NominaRecaudaPorElContrato</c>, <c>ComercioConCartera</c>, el retiro y las que vengan). Su <c>Up()</c> lleva la cabecera de
    /// aprobación con el diagnóstico, el respaldo y el segundo revisor —que dicen «pendiente» hasta la fase 24 (T1083, T1084)— y su
    /// guarda se niega con <c>THROW 50013</c> en SQL Server y <c>RAISE EXCEPTION</c> en PostgreSQL salvo la fila
    /// <c>LND.RetiroHeredado.Aprobado</c>.
    /// </summary>
    [Fact]
    public void El_retiro_de_la_cartera_heredada_es_la_unica_destructiva_de_la_013_y_lleva_su_guarda()
    {
        // Feature 014: la 013 termina donde empieza la 014 (sus migraciones nacen desde el 2026-10-18).
        Assert.Equal(["RetiroDeLaCarteraHeredada.cs"], DestructivasEntre("20261006000000", Inicio014));
        ComprobarRetiroConGuarda("RetiroDeLaCarteraHeredada", "LND.RetiroHeredado.Aprobado", "THROW 50013");
    }

    /// <summary>Feature 014: la primera marca de tiempo de sus migraciones.</summary>
    private const string Inicio014 = "20261018000000";

    /// <summary>
    /// Feature 014 (T006, R28, C19; molde de la 013 T0125): <c>RetiroDeCaptacionesHeredadas</c> —que borra las 38 tablas <c>LND_</c>
    /// conservadas por la 013 y las 7 <c>CDT_*</c>— es la única migración destructiva de la feature. Su <c>Up()</c> lleva la cabecera de
    /// aprobación con el diagnóstico, el respaldo y el segundo revisor, y su guarda se niega con <c>THROW 50014</c> en SQL Server y
    /// <c>RAISE EXCEPTION</c> en PostgreSQL salvo la fila <c>LND.CaptacionesHeredadas.RetiroAprobado</c>.
    /// </summary>
    [Fact]
    public void El_retiro_de_captaciones_heredadas_es_la_unica_destructiva_de_la_014_y_lleva_su_guarda()
    {
        Assert.Equal(["RetiroDeCaptacionesHeredadas.cs"], DestructivasEntre(Inicio014, "99999999999999"));
        ComprobarRetiroConGuarda("RetiroDeCaptacionesHeredadas", "LND.CaptacionesHeredadas.RetiroAprobado", "THROW 50014");
    }

    /// <summary>Los nombres (sin marca de tiempo) de las migraciones con algo destructivo en su <c>Up()</c>, desde/hasta esas marcas.</summary>
    private static List<string> DestructivasEntre(string desde, string hasta) =>
        ArchivosDeMigracion()
            .Where(f => string.CompareOrdinal(Path.GetFileName(f)[..Math.Min(14, Path.GetFileName(f).Length)], desde) >= 0
                        && string.CompareOrdinal(Path.GetFileName(f)[..Math.Min(14, Path.GetFileName(f).Length)], hasta) < 0)
            .Where(f => OperacionesDestructivasEnUp(File.ReadAllText(f)).Count > 0)
            .Select(Path.GetFileName)
            .Select(n => n![15..])
            .Distinct()
            .ToList();

    /// <summary>Las dos (una por motor) llevan la cabecera de aprobación y la guarda antes del primer <c>Drop*</c>.</summary>
    private static void ComprobarRetiroConGuarda(string nombre, string aprobacion, string throwSqlServer)
    {
        var retiros = ArchivosDeMigracion().Where(f => f.EndsWith($"_{nombre}.cs", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, retiros.Count);
        foreach (var archivo in retiros)
        {
            var texto = File.ReadAllText(archivo);
            var inicio = texto.IndexOf("void Up(", StringComparison.Ordinal);
            var fin = texto.IndexOf("void Down(", inicio, StringComparison.Ordinal);
            var up = texto[inicio..fin];
            Assert.Contains(MarcadorDeAprobacion, up, StringComparison.Ordinal);
            Assert.Matches(@"//\s+Diagnóstico: ", up);
            Assert.Matches(@"//\s+Respaldo: ", up);
            Assert.Matches(@"//\s+Segundo revisor: ", up);
            Assert.Contains(aprobacion, up, StringComparison.Ordinal);
            if (archivo.Contains($"{Path.DirectorySeparatorChar}IngenIA365ERP.Persistence.Migrations.SqlServer{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                Assert.Contains(throwSqlServer, up, StringComparison.Ordinal);
                Assert.DoesNotContain("RAISE EXCEPTION", up, StringComparison.Ordinal);
            }
            else
            {
                Assert.Contains("RAISE EXCEPTION", up, StringComparison.Ordinal);
                Assert.DoesNotContain("THROW 500", up, StringComparison.Ordinal);
            }
            // La guarda va antes que cualquier operación destructiva.
            var guarda = up.IndexOf(aprobacion, StringComparison.Ordinal);
            var primera = OperacionDestructiva.Match(up);
            Assert.True(primera.Success && guarda < primera.Index, "La guarda tiene que ir antes del primer Drop*.");
        }
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
            .Concat(SqlDestructivo.Matches(cuerpo)
                .Select(m => $"SQL crudo: {m.Groups[1].Value.ToUpperInvariant()}"))
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
