using System.Text.RegularExpressions;
using IngenIA365ERP.Architecture.Tests.Helpers;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 015, T0003 y T0476 (R-04; FR-161; data-model §24): la Tesorería heredada (<c>TRS_Checks</c>, <c>TRS_Concepts</c>, <c>TRS_Invoices</c>) se retiró en la fase 46
/// con la migración destructiva con guarda <c>RetiroDeLaTesoreriaHeredada</c>, y <b>no vuelve</b>: ni sus tablas ni sus entidades en el modelo EF de los dos motores, ni en
/// el snapshot vigente y los designers desde el retiro, ni sus espacios de handlers, ni sus cuatro rutas en <c>Endpoints/</c>, ni sus cuatro pantallas ni sus enlaces en
/// <c>Shared</c>. Lo nuevo, además, no nombra tablas <c>TRS_</c> (la regla de antes del retiro). Molde: <see cref="LaCarteraHeredadaNoVuelve"/>. Prueba escrita, sin correr.
/// </summary>
public class LaTesoreriaHeredadaNoVuelve
{
    /// <summary>Las tablas <c>TRS_</c> retiradas.</summary>
    private static readonly string[] Retiradas = ["TRS_Checks", "TRS_Concepts", "TRS_Invoices"];

    /// <summary>Sus entidades, que vivían en la raíz de <c>Domain.Entities.Treasury</c>.</summary>
    private static readonly string[] EntidadesRetiradas = ["Check", "TreasuryConcept", "TreasuryInvoice"];

    /// <summary>Las cuatro áreas de ruta retiradas sin alias, bajo <c>/api/treasury</c>.</summary>
    private static readonly string[] RutasRetiradas = ["checks", "concepts", "invoices", "cash-flow"];

    /// <summary>Las cuatro pantallas retiradas sin alias.</summary>
    private static readonly string[] PantallasRetiradas = ["/tesoreria/cheques", "/tesoreria/conceptos", "/tesoreria/facturas", "/tesoreria/flujo-caja"];

    private const string Retiro = "RetiroDeLaTesoreriaHeredada";

    /// <summary>
    /// Migraciones con marca posterior al retiro que nacieron en otra rama, sobre un modelo que todavía tenía la Tesorería heredada:
    /// su Designer es la foto congelada de ese modelo y no se regenera. <c>VersionDeClaveDeAnclaMasLarga</c> llegó de develop
    /// (arreglo del sellado de auditoría, 2026-10-26) al integrar la 015.
    /// </summary>
    private static readonly string[] DeOtraRama = ["VersionDeClaveDeAnclaMasLarga"];

    private static ApplicationDbContext Contexto(bool postgres)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>().ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        builder = postgres
            ? builder.UseNpgsql("Host=modelo-sin-conexion;Database=x;Username=y;Password=z")
            : builder.UseSqlServer("Server=modelo-sin-conexion;Database=x;User Id=y;Password=z;TrustServerCertificate=True");
        return new ApplicationDbContext(builder.Options);
    }

    [Fact]
    public void Lo_nuevo_no_usa_las_tablas_heredadas()
    {
        var root = RepoPath.FindRepoRoot();
        var trs = new Regex("\"TRS_", RegexOptions.Compiled);
        var infracciones = RecaudoYTesoreria.ArchivosDeRecaudo().Concat(RecaudoYTesoreria.ArchivosDeTesoreria())
            .Where(a => trs.IsMatch(FuenteSinComentarios.Leer(a)))
            .Select(a => ReglaSobreFuente.Relativo(root, a))
            .ToList();
        ReglaSobreFuente.SinInfracciones(infracciones, "Recaudo o la Tesorería usan una tabla TRS_ heredada (R-04)");
    }

    [Fact]
    public void Ninguna_configuracion_mapea_una_tabla_retirada()
    {
        var root = RepoPath.FindRepoRoot();
        var infracciones = new List<string>();
        foreach (var tabla in Retiradas)
        {
            var mapea = new Regex($@"ToTable\(\s*""{Regex.Escape(tabla)}""", RegexOptions.Compiled);
            infracciones.AddRange(ReglaSobreFuente.ArchivosEn(["Infrastructure/IngenIA365ERP.Persistence/Configurations"])
                .Where(a => mapea.IsMatch(FuenteSinComentarios.Leer(a)))
                .Select(a => $"{ReglaSobreFuente.Relativo(root, a)}: mapea {tabla}"));
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Una tabla de la Tesorería heredada volvió al modelo (FR-161)");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void El_modelo_no_tiene_ninguna_tabla_ni_entidad_retirada(bool postgres)
    {
        using var db = Contexto(postgres);
        var violaciones = db.Model.GetEntityTypes()
            .Where(t => Retiradas.Contains(t.GetTableName() ?? string.Empty, StringComparer.Ordinal)
                        || t.ClrType.Namespace == "IngenIA365ERP.Domain.Entities.Treasury" && EntidadesRetiradas.Contains(t.ClrType.Name, StringComparer.Ordinal))
            .Select(t => $"{t.ClrType.FullName} → {t.GetTableName()}")
            .ToList();
        Assert.True(violaciones.Count == 0, "El modelo vuelve a tener la Tesorería heredada (FR-161): " + string.Join(", ", violaciones));
    }

    [Fact]
    public void Ningun_tipo_vive_en_los_espacios_heredados()
    {
        var espacios = new[] { "IngenIA365ERP.Application.Treasury.Checks", "IngenIA365ERP.Application.Treasury.Invoices", "IngenIA365ERP.Application.Treasury.TreasuryConcepts" };
        var tipos = new[] { typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly, typeof(IngenIA365ERP.Application.DependencyInjection).Assembly }
            .SelectMany(a => a.GetTypes())
            .Where(t => t.Namespace == "IngenIA365ERP.Domain.Entities.Treasury"
                        || espacios.Any(e => t.Namespace == e || (t.Namespace?.StartsWith(e + ".", StringComparison.Ordinal) ?? false)))
            .Select(t => t.FullName)
            .ToList();
        Assert.True(tipos.Count == 0, "Vuelve un tipo de la Tesorería heredada: " + string.Join(", ", tipos));
    }

    [Theory]
    [InlineData("PostgreSql")]
    [InlineData("SqlServer")]
    public void El_snapshot_posterior_al_retiro_no_conoce_lo_heredado(string proveedor)
    {
        var carpeta = Path.Combine(RepoPath.FindRepoRoot(), "src", "Infrastructure", $"IngenIA365ERP.Persistence.Migrations.{proveedor}", "Application");
        var retiro = Directory.EnumerateFiles(carpeta, $"*_{Retiro}.Designer.cs").SingleOrDefault();
        Assert.True(retiro is not null, $"No existe {Retiro} en {proveedor} (fase 46, T0474).");
        var marca = Path.GetFileName(retiro)![..14];
        var archivos = Directory.EnumerateFiles(carpeta, "*.Designer.cs")
            .Where(f => string.CompareOrdinal(Path.GetFileName(f)[..14], marca) >= 0)
            .Where(f => !DeOtraRama.Any(n => Path.GetFileName(f).EndsWith($"_{n}.Designer.cs", StringComparison.Ordinal)))
            .Append(Path.Combine(carpeta, "ApplicationDbContextModelSnapshot.cs"))
            .ToList();
        var prohibidos = Retiradas
            .Select(n => (new Regex($"\"{Regex.Escape(n)}\"", RegexOptions.Compiled), $"conoce la tabla retirada {n}"))
            .Concat(EntidadesRetiradas.Select(n => (new Regex($@"Domain\.Entities\.Treasury\.{Regex.Escape(n)}""", RegexOptions.Compiled), $"conoce la entidad retirada {n}")))
            .ToList();
        ReglaSobreFuente.SinInfracciones(ReglaSobreFuente.Infracciones(archivos, prohibidos),
            $"El modelo de migraciones de {proveedor} vuelve a tener la Tesorería heredada (FR-161)");
    }

    [Fact]
    public void Las_rutas_retiradas_no_vuelven_ni_con_alias()
    {
        var area = string.Join('|', RutasRetiradas.Select(Regex.Escape));
        var prohibidos = new List<(Regex, string)>
        {
            (new Regex($@"""/api/treasury/({area})(?![\w-])", RegexOptions.Compiled), "publica o llama una ruta de la Tesorería heredada"),
            (new Regex($@"Grupo\(\s*app\s*,\s*""/({area})(?![\w-])", RegexOptions.Compiled), "abre un grupo en un área de la Tesorería heredada"),
        };
        var infracciones = ReglaSobreFuente.Infracciones(
            ReglaSobreFuente.ArchivosEn(["Presentation/IngenIA365ERP.API/Endpoints", "Presentation/IngenIA365ERP.Shared"]), prohibidos);
        ReglaSobreFuente.SinInfracciones(infracciones, "Vuelve una ruta de la Tesorería heredada (FR-161: se retiraron sin alias)");
    }

    [Fact]
    public void Las_pantallas_retiradas_no_vuelven_ni_en_el_menu()
    {
        var rutas = string.Join('|', PantallasRetiradas.Select(Regex.Escape));
        var prohibidos = new List<(Regex, string)>
        {
            (new Regex($@"@page\s+""({rutas})""", RegexOptions.Compiled), "publica una pantalla de la Tesorería heredada"),
            (new Regex($@"href=""({rutas})""", RegexOptions.Compiled), "enlaza una pantalla de la Tesorería heredada"),
            (new Regex($@"""({rutas})""", RegexOptions.Compiled), "nombra una pantalla de la Tesorería heredada"),
        };
        var archivos = ReglaSobreFuente.ArchivosEn(["Presentation/IngenIA365ERP.Shared"], "*.razor")
            .Concat(ReglaSobreFuente.ArchivosEn(["Presentation/IngenIA365ERP.Shared"]));
        ReglaSobreFuente.SinInfracciones(ReglaSobreFuente.Infracciones(archivos, prohibidos),
            "Vuelve una pantalla de la Tesorería heredada (FR-161: se retiraron sin alias)");
    }
}
