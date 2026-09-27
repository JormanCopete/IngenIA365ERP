using System.Reflection;
using System.Text.RegularExpressions;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Architecture.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T001 y T444 (decisiones-transversales T31, §2.18), FR-014: Inventario no lee ni escribe
/// datos de Contabilidad ni de Cartera. Lo que necesita de ellos lo pide por los puertos
/// <c>IContabilidadParaInventario</c> e <c>IConsultasDeCartera</c> (Application/Common/Integration),
/// que no viven en los espacios de nombres prohibidos; todo lo demás cruza por mensajes.
///
/// <para>
/// Cuatro comprobaciones (T31): (1) ArchUnit, Inventario no depende de ningún espacio de nombres
/// <c>*.Accounting*</c> ni <c>*.Lending*</c> salvo los puertos de <c>Application.Common.Integration</c>;
/// (2) sobre el fuente de <c>Application/Inventory</c> y <c>Domain/Entities/Inventory</c>, ni tablas
/// <c>ACC_</c>/<c>LND_</c>, ni <c>AccountingPoster</c>, ni los <c>DbSet</c> contables o de cartera;
/// (3) los puertos declaran exactamente sus métodos (la comprobación automática de FR-014);
/// (4) la inversa: el consumidor contable (<c>Application.Accounting.Inventory</c>) no depende de las
/// entidades de Inventario: lee lo que Inventario manda en los mensajes, no sus tablas.
/// </para>
/// </summary>
public class InventarioNoConoceContabilidadNiCartera
{
    /// <summary>Espacios de nombres del módulo comercial (con sus hijos).</summary>
    private static readonly string[] NamespacesDeInventario =
    [
        "IngenIA365ERP.Application.Inventory",
        "IngenIA365ERP.Domain.Entities.Inventory",
    ];

    /// <summary>
    /// Todo espacio de nombres con un segmento <c>Accounting</c> o <c>Lending</c>, salvo los puertos de la plataforma
    /// (<c>IngenIA365ERP.Application.Common.Integration.*</c>, donde vive <c>IContabilidadParaInventario</c>).
    /// </summary>
    private const string ProhibidoParaInventario =
        @"^IngenIA365ERP\.(?!Application\.Common\.Integration(\.|$))([\w.]+\.)?(Accounting|Lending)(\..+)?$";

    /// <summary>Carpetas (relativas a <c>src/Core</c>) cuyo fuente se revisa con las expresiones de (2).</summary>
    private static readonly string[] CarpetasDeInventario =
    [
        Path.Combine("IngenIA365ERP.Application", "Inventory"),
        Path.Combine("IngenIA365ERP.Domain", "Entities", "Inventory"),
    ];

    private static readonly Lazy<ArchUnitNET.Domain.Architecture> Arquitectura = new(() => new ArchLoader()
        .LoadAssemblies(
            typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly,
            typeof(IngenIA365ERP.Application.DependencyInjection).Assembly)
        .Build());

    private static string ConHijos(string ns) => $"^{Regex.Escape(ns)}(\\..+)?$";

    [Fact]
    public void Inventario_no_depende_de_Contabilidad_ni_de_Cartera()
    {
        foreach (var inventario in NamespacesDeInventario)
        {
            Types().That().ResideInNamespaceMatching(ConHijos(inventario))
                .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(ProhibidoParaInventario)
                .Because($"FR-014: {inventario} pide a Contabilidad y Cartera sólo por los puertos de Application.Common.Integration")
                .Check(Arquitectura.Value);
        }
    }

    [Fact]
    public void El_fuente_de_Inventario_no_nombra_tablas_ni_escritores_contables()
    {
        var root = RepoPath.FindRepoRoot();
        var prohibidos = new List<(Regex Patron, string Que)>
        {
            (new Regex("\"ACC_", RegexOptions.Compiled), "una tabla ACC_"),
            (new Regex("\"LND_", RegexOptions.Compiled), "una tabla LND_"),
            (new Regex(@"\bAccountingPoster\b", RegexOptions.Compiled), "AccountingPoster"),
        };
        foreach (var dbSet in DbSetsDe("IngenIA365ERP.Domain.Entities.Accounting", "IngenIA365ERP.Domain.Entities.Lending"))
            prohibidos.Add((new Regex($@"\b(db|_db|context|contexto|Db)\s*\.\s*{Regex.Escape(dbSet)}\b", RegexOptions.Compiled), $"el DbSet {dbSet}"));

        var infractores = new List<string>();
        var revisados = 0;
        foreach (var carpeta in CarpetasDeInventario)
        {
            var ruta = Path.Combine(root, "src", "Core", carpeta);
            Assert.True(Directory.Exists(ruta), $"No existe {ruta}: si el módulo se movió, actualizá CarpetasDeInventario.");
            foreach (var archivo in Directory.EnumerateFiles(ruta, "*.cs", SearchOption.AllDirectories))
            {
                revisados++;
                var texto = FuenteSinComentarios.Leer(archivo);
                foreach (var (patron, que) in prohibidos)
                {
                    if (patron.IsMatch(texto))
                        infractores.Add($"{Path.GetRelativePath(root, archivo)}: nombra {que}");
                }
            }
        }

        Assert.True(revisados > 0, "No se revisó ningún archivo de Inventario: ¿cambió la estructura de carpetas?");
        Assert.True(infractores.Count == 0,
            "Inventario toca Contabilidad o Cartera fuera de sus puertos (FR-014, T31):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void El_puerto_de_Contabilidad_declara_exactamente_sus_cinco_consultas()
    {
        // La quinta, SinIniciarAsync, llegó con la decisión del dueño del 2026-09-26: sin contabilidad iniciada el modo de paso
        // por defecto es «no pasa».
        var puerto = typeof(IngenIA365ERP.Application.Common.Integration.Accounting.IContabilidadParaInventario);
        Assert.Equal(
            ["CompletitudAsync", "EvaluarAsync", "PrevisualizarLoteAsync", "SaldosDeCuentasMapeadasAsync", "SinIniciarAsync"],
            MetodosDe(puerto));
    }

    [Fact]
    public void El_puerto_de_Cartera_declara_exactamente_sus_dos_consultas()
    {
        // IConsultasDeCartera nace con Cartera (IC, D-02). Mientras no exista no hay nada que violar; en cuanto aparezca,
        // tiene que vivir en los puertos de la plataforma y declarar sus dos métodos, ni uno más.
        var puertos = typeof(IngenIA365ERP.Application.DependencyInjection).Assembly.GetTypes()
            .Where(t => t.IsInterface && t.Name == "IConsultasDeCartera")
            .ToList();
        Assert.True(puertos.Count <= 1, "Hay más de un IConsultasDeCartera.");
        foreach (var puerto in puertos)
        {
            Assert.StartsWith("IngenIA365ERP.Application.Common.Integration", puerto.Namespace, StringComparison.Ordinal);
            Assert.Equal(2, MetodosDe(puerto).Count);
        }
    }

    [Fact]
    public void El_consumidor_contable_no_depende_de_las_entidades_de_Inventario()
    {
        Types().That().ResideInNamespaceMatching(ConHijos("IngenIA365ERP.Application.Accounting.Inventory"))
            .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(ConHijos("IngenIA365ERP.Domain.Entities.Inventory"))
            .Because("T31: Contabilidad lee lo que Inventario manda en los mensajes y por IDimensionesDeInventario, no sus tablas")
            .Check(Arquitectura.Value);
    }

    private static List<string> MetodosDe(System.Type puerto) => puerto
        .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Where(m => !m.IsSpecialName)
        .Select(m => m.Name)
        .Order(StringComparer.Ordinal)
        .ToList();

    /// <summary>Los nombres de los <c>DbSet</c> de <see cref="IApplicationDbContext"/> cuyas entidades viven en esos espacios de nombres.</summary>
    private static IEnumerable<string> DbSetsDe(params string[] espacios) => typeof(IApplicationDbContext)
        .GetProperties()
        .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
        .Where(p => p.PropertyType.GetGenericArguments()[0].Namespace is { } ns
                    && espacios.Any(e => ns == e || ns.StartsWith(e + ".", StringComparison.Ordinal)))
        .Select(p => p.Name);
}
