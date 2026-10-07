using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 013, FR-144 (DT §2.25, T2, R124): ninguna de las 55 tablas de crédito heredadas vuelve, ni por nombre de tabla
/// ni por su entidad, en el modelo de <c>ApplicationDbContext</c> de los dos motores, en los snapshots de migraciones posteriores a
/// <c>RetiroDeLaCarteraHeredada</c> ni en el fuente; y ninguna tabla nueva reutiliza uno de los 93 nombres heredados —las 38
/// conservadas siguen con su nombre y su entidad mudada a <c>Savings</c> (T0122; data-model parte 3 §13.1).
/// </summary>
public class LaCarteraHeredadaNoVuelve
{
    private static readonly HashSet<string> TablasRetiradas = CarteraHeredada.Retiradas.Select(r => r.Tabla).ToHashSet(StringComparer.Ordinal);
    private static readonly HashSet<string> EntidadesRetiradas = CarteraHeredada.Retiradas.Select(r => r.Entidad).ToHashSet(StringComparer.Ordinal);

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
    public void La_lista_del_diseno_tiene_55_retiradas_y_38_conservadas_sin_repetir()
    {
        Assert.Equal(55, TablasRetiradas.Count);
        Assert.Equal(38, CarteraHeredada.Conservadas.Select(c => c.Tabla).Distinct().Count());
        Assert.Equal(93, CarteraHeredada.NombresHeredados.Distinct(StringComparer.Ordinal).Count());
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
        // Las 38 conservadas mantienen su nombre (sin cambio de ToTable) y su entidad, ahora en Domain.Entities.Savings; ningún otro
        // tipo puede ocupar uno de los 93 nombres.
        var conservadas = CarteraHeredada.Conservadas.ToDictionary(c => c.Tabla, c => c.Entidad, StringComparer.Ordinal);
        var heredados = CarteraHeredada.NombresHeredados.ToHashSet(StringComparer.Ordinal);
        using var db = Contexto(postgres);
        var tipos = db.Model.GetEntityTypes().Where(t => heredados.Contains(t.GetTableName() ?? string.Empty)).ToList();

        var reutilizados = tipos
            .Where(t => !conservadas.TryGetValue(t.GetTableName()!, out var entidad)
                        || t.ClrType.Name != entidad
                        || t.ClrType.Namespace != "IngenIA365ERP.Domain.Entities.Savings")
            .Select(t => $"{t.ClrType.FullName} → {t.GetTableName()}")
            .ToList();
        Assert.True(reutilizados.Count == 0, "Una tabla reutiliza un nombre heredado (T2, R124): " + string.Join(", ", reutilizados));
        Assert.Equal(38, tipos.Select(t => t.GetTableName()).Distinct().Count());
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
        var prohibidos = TablasRetiradas
            .Select(n => (new Regex($@"\b{Regex.Escape(n)}\b", RegexOptions.Compiled), $"nombra la tabla retirada {n}"))
            .ToList();
        // Las migraciones históricas crearon y retiran estas tablas: son historia y se revisan por sus snapshots (arriba).
        var infracciones = ReglaSobreFuente.Infracciones(ReglaSobreFuente.ArchivosEn(Carpetas), prohibidos,
            exento: r => r.Contains("/IngenIA365ERP.Persistence.Migrations.", StringComparison.Ordinal));
        ReglaSobreFuente.SinInfracciones(infracciones, "La cartera heredada vuelve a aparecer (FR-144)");
    }
}
