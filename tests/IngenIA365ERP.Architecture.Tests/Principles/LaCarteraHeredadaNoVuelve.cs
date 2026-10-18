using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, FR-144 (DT §2.25, T2, R124): ninguna de las 55 tablas de crédito heredadas vuelve, ni por nombre de tabla
/// ni por su entidad, en el modelo de <c>ApplicationDbContext</c> de los dos motores, en los snapshots de migraciones posteriores a
/// <c>RetiroDeLaCarteraHeredada</c> ni en el fuente; y ninguna tabla nueva reutiliza uno de los nombres heredados (T0122;
/// data-model parte 3 §13.1).
///
/// <para>
/// Feature 014 (T009, R28, R29): ya no hay conservado. Las 45 que retira <c>RetiroDeCaptacionesHeredadas</c> —las 38 que la 013
/// conservó y las 7 <c>CDT_*</c>— tampoco vuelven: ni en el modelo de los dos motores, ni en los snapshots desde ese retiro, ni en el
/// fuente; ni sus espacios <c>Domain.Entities.Savings</c> y <c>Domain.Entities.CDT</c>, ni <c>Application.Savings</c> y
/// <c>Application.CDT</c>, ni las rutas <c>/api/savings</c>, <c>/api/cdt</c> y <c>/api/reports/savings</c>. Esta prueba absorbe la
/// antigua <c>ElEspacioLendingEsSoloCartera</c>: con todo retirado, el espacio <c>Lending</c> es sólo de la Cartera (captaciones
/// incluidas, en <c>Lending.Deposits</c>).
/// </para>
/// </summary>
public class LaCarteraHeredadaNoVuelve
{
    private static readonly HashSet<string> TablasRetiradas = CarteraHeredada.Retiradas.Select(r => r.Tabla).ToHashSet(StringComparer.Ordinal);
    private static readonly HashSet<string> EntidadesRetiradas = CarteraHeredada.Retiradas.Select(r => r.Entidad).ToHashSet(StringComparer.Ordinal);
    private static readonly HashSet<string> TablasRetiradasPorCaptaciones = CarteraHeredada.RetiradasPorCaptaciones.Select(r => r.Tabla).ToHashSet(StringComparer.Ordinal);

    /// <summary>Carpetas del fuente revisadas (relativas a <c>src/</c>); las migraciones históricas se miran aparte (snapshots).</summary>
    private static readonly string[] Carpetas = [
        "Core",
        "Infrastructure",
        "Presentation",
    ];

    private static ApplicationDbContext Contexto(bool postgres)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>().ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        builder = postgres
            ? builder.UseNpgsql("Host=modelo-sin-conexion;Database=x;Username=y;Password=z")
            : builder.UseSqlServer("Server=modelo-sin-conexion;Database=x;User Id=y;Password=z;TrustServerCertificate=True");
        return new ApplicationDbContext(builder.Options);
    }

    [Fact]
    public void La_lista_del_diseno_tiene_55_retiradas_por_la_013_y_45_por_la_014_sin_repetir()
    {
        Assert.Equal(55, TablasRetiradas.Count);
        Assert.Equal(38, CarteraHeredada.Conservadas.Select(c => c.Tabla).Distinct().Count());
        Assert.Equal(45, TablasRetiradasPorCaptaciones.Count);
        Assert.Equal(100, CarteraHeredada.NombresHeredados.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void El_modelo_no_tiene_ninguna_tabla_ni_entidad_retirada(bool postgres)
    {
        using var db = Contexto(postgres);
        var violaciones = db.Model.GetEntityTypes()
            .Where(t => TablasRetiradas.Contains(t.GetTableName() ?? string.Empty) || EntidadesRetiradas.Contains(t.ClrType.Name))
            .Select(t => $"{t.ClrType.FullName} → {t.GetTableName()}")
            .ToList();
        Assert.True(violaciones.Count == 0, "El modelo vuelve a tener cartera heredada (FR-144): " + string.Join(", ", violaciones));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Ninguna_tabla_nueva_reutiliza_un_nombre_heredado(bool postgres)
    {
        // Feature 014 (R28): ya no hay conservado, así que ningún tipo del modelo ocupa uno de los 100 nombres heredados ni vive en
        // los espacios de entidades retirados (por eso la cuenta de captaciones se llama MemberAccount, research §0.2).
        var heredados = CarteraHeredada.NombresHeredados.ToHashSet(StringComparer.Ordinal);
        using var db = Contexto(postgres);
        var reutilizados = db.Model.GetEntityTypes()
            .Where(t => heredados.Contains(t.GetTableName() ?? string.Empty)
                        || CarteraHeredada.EspaciosRetiradosPorCaptaciones.Contains(t.ClrType.Namespace))
            .Select(t => $"{t.ClrType.FullName} → {t.GetTableName()}")
            .ToList();
        Assert.True(reutilizados.Count == 0, "Una tabla reutiliza un nombre heredado (T2, R124; 014 R28): " + string.Join(", ", reutilizados));
    }

    [Theory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public void El_snapshot_posterior_al_retiro_de_captaciones_no_conoce_lo_heredado(string proveedor)
    {
        // Feature 014 (T006, T009): el snapshot vigente y los designers de RetiroDeCaptacionesHeredadas y de toda migración posterior.
        var carpeta = Path.Combine(RepoPath.FindRepoRoot(), "src", "Infrastructure", $"IngenIA365ERP.Persistence.Migrations.{proveedor}", "Application");
        var retiro = Directory.EnumerateFiles(carpeta, "*_RetiroDeCaptacionesHeredadas.Designer.cs").SingleOrDefault();
        Assert.True(retiro is not null, $"No existe RetiroDeCaptacionesHeredadas en {proveedor} (014, T006).");

        var marca = Path.GetFileName(retiro)![..14];
        var archivos = Directory.EnumerateFiles(carpeta, "*.Designer.cs")
            .Where(f => string.CompareOrdinal(Path.GetFileName(f)[..14], marca) >= 0)
            .Append(Path.Combine(carpeta, "ApplicationDbContextModelSnapshot.cs"))
            .ToList();
        var prohibidos = TablasRetiradasPorCaptaciones
            .Select(n => (new Regex($"\"{Regex.Escape(n)}\"", RegexOptions.Compiled), $"conoce la tabla retirada {n}"))
            .Concat(CarteraHeredada.EspaciosRetiradosPorCaptaciones.Select(ns => (new Regex($@"{Regex.Escape(ns)}\.", RegexOptions.Compiled), $"conoce el espacio retirado {ns}")))
            .ToList();
        var infracciones = ReglaSobreFuente.Infracciones(archivos, prohibidos);
        ReglaSobreFuente.SinInfracciones(infracciones, $"El modelo de migraciones de {proveedor} vuelve a tener las captaciones heredadas (014, R28)");
    }

    [Theory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public void El_snapshot_posterior_al_retiro_no_conoce_la_cartera_retirada(string proveedor)
    {
        var carpeta = Path.Combine(RepoPath.FindRepoRoot(), "src", "Infrastructure", $"IngenIA365ERP.Persistence.Migrations.{proveedor}", "Application");
        var retiro = Directory.EnumerateFiles(carpeta, "*_RetiroDeLaCarteraHeredada.Designer.cs").SingleOrDefault();
        Assert.True(retiro is not null, $"No existe RetiroDeLaCarteraHeredada en {proveedor} (T0197).");

        // El snapshot vigente y los designers del retiro y de toda migración posterior describen el modelo sin la cartera heredada.
        var marca = Path.GetFileName(retiro)![..14];
        var archivos = Directory.EnumerateFiles(carpeta, "*.Designer.cs")
            .Where(f => string.CompareOrdinal(Path.GetFileName(f)[..14], marca) >= 0)
            .Append(Path.Combine(carpeta, "ApplicationDbContextModelSnapshot.cs"))
            .ToList();
        var prohibidos = TablasRetiradas
            .Select(n => (new Regex($"\"{Regex.Escape(n)}\"", RegexOptions.Compiled), $"conoce la tabla retirada {n}"))
            .Concat(EntidadesRetiradas.Select(n => (new Regex($@"Domain\.Entities\.\w+\.{Regex.Escape(n)}""", RegexOptions.Compiled), $"conoce la entidad retirada {n}")))
            .ToList();
        var infracciones = ReglaSobreFuente.Infracciones(archivos, prohibidos);
        ReglaSobreFuente.SinInfracciones(infracciones, $"El modelo de migraciones de {proveedor} vuelve a tener la cartera heredada (FR-144)");
    }

    [Fact]
    public void Ninguna_tabla_retirada_aparece_en_el_fuente()
    {
        // Feature 014 (T009): también las 45 de captaciones, sus espacios de entidades y de handlers, y sus rutas.
        var prohibidos = TablasRetiradas.Concat(TablasRetiradasPorCaptaciones)
            .Select(n => (new Regex($@"\b{Regex.Escape(n)}\b", RegexOptions.Compiled), $"nombra la tabla retirada {n}"))
            .Append((new Regex(@"IngenIA365ERP\.(Domain\.Entities|Application)\.(Savings|CDT)\b", RegexOptions.Compiled), "usa un espacio de captaciones heredadas"))
            .Append((new Regex(@"""/api/(savings|cdt|reports/savings)\b", RegexOptions.Compiled), "publica o llama una ruta de captaciones heredadas"))
            .ToList();
        // Las migraciones históricas crearon y retiran estas tablas: son historia y se revisan por sus snapshots (arriba).
        var infracciones = ReglaSobreFuente.Infracciones(ReglaSobreFuente.ArchivosEn(Carpetas), prohibidos,
            exento: r => r.Contains("/IngenIA365ERP.Persistence.Migrations.", StringComparison.Ordinal));
        ReglaSobreFuente.SinInfracciones(infracciones, "La cartera heredada vuelve a aparecer (FR-144)");
    }

    [Fact]
    public void Las_carpetas_de_lo_heredado_no_vuelven()
    {
        // Feature 014 (T007, T009): con todo retirado, ni las carpetas de entidades, configuraciones, handlers, rutas y pantallas
        // heredadas ni el informe de /api/reports/savings vuelven. Las captaciones viven bajo Lending/Deposits y Cartera/Captaciones.
        var src = Path.Combine(RepoPath.FindRepoRoot(), "src");
        string[] carpetas =
        [
            "Core/IngenIA365ERP.Domain/Entities/Savings", "Core/IngenIA365ERP.Domain/Entities/CDT",
            "Core/IngenIA365ERP.Application/Savings", "Core/IngenIA365ERP.Application/CDT",
            "Infrastructure/IngenIA365ERP.Persistence/Configurations/Savings", "Infrastructure/IngenIA365ERP.Persistence/Configurations/CDT",
            "Presentation/IngenIA365ERP.API/Endpoints/Savings", "Presentation/IngenIA365ERP.API/Endpoints/CDT",
            "Presentation/IngenIA365ERP.Shared/Pages/AhorrosYAportes", "Presentation/IngenIA365ERP.Shared/Pages/CDT",
        ];
        var vueltas = carpetas.Where(c => Directory.Exists(Path.Combine(src, c.Replace('/', Path.DirectorySeparatorChar)))).ToList();
        Assert.True(vueltas.Count == 0, "Volvieron carpetas de las captaciones heredadas (014, R28): " + string.Join(", ", vueltas));
        Assert.False(File.Exists(Path.Combine(src, "Presentation", "IngenIA365ERP.API", "Endpoints", "Reports", "SavingsReportsEndpoints.cs")));
    }
}
