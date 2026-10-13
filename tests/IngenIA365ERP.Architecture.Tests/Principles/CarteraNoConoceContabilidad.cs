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
/// Feature 013, FR-126 y E-01 (DT §2.25, T51, T59, C34; contracts/contabilidad.md §1.2 y §26): Cartera nunca conoce una cuenta ni escribe
/// asientos; lo que cruza a Contabilidad cruza por mensajes, y lo que Cartera le pregunta, por el puerto <c>IContabilidadParaCartera</c>
/// (<c>Application.Common.Integration.Accounting</c>). Molde: <see cref="InventarioNoConoceContabilidadNiCartera"/>.
///
/// <para>
/// Cuatro comprobaciones (T0605): (1) ArchUnit, <c>Application.Lending*</c>, <c>Domain.Lending*</c> y <c>Domain.Entities.Lending*</c> no
/// dependen de ningún espacio con segmento <c>Accounting</c> salvo <c>Application.Common.Integration.Accounting</c>; (2) sobre el fuente de
/// <c>Application/Lending</c>, <c>Domain/Lending</c> y <c>Domain/Entities/Lending</c>: ni tablas <c>ACC_</c>, ni <c>AccountingPoster</c>, ni
/// los <c>DbSet</c> contables de <see cref="IApplicationDbContext"/>; (3) el puerto declara exactamente sus cinco métodos; (4) la inversa: el
/// consumidor contable de Cartera (<c>Application.Accounting.Lending</c>) no depende de las entidades ni de la aplicación de Cartera: lee lo
/// que Cartera manda en los mensajes y le pregunta por <c>IDimensionesDeCartera</c>.
/// </para>
/// </summary>
public class CarteraNoConoceContabilidad
{
    /// <summary>Espacios de nombres de Cartera (con sus hijos).</summary>
    private static readonly string[] NamespacesDeCartera =
    [
        "IngenIA365ERP.Application.Lending",
        "IngenIA365ERP.Domain.Lending",
        "IngenIA365ERP.Domain.Entities.Lending",
    ];

    /// <summary>
    /// Todo espacio de nombres con un segmento <c>Accounting</c>, salvo el puerto de la plataforma
    /// (<c>IngenIA365ERP.Application.Common.Integration.Accounting</c>, donde vive <c>IContabilidadParaCartera</c>).
    /// </summary>
    private const string ProhibidoParaCartera =
        @"^IngenIA365ERP\.(?!Application\.Common\.Integration\.Accounting(\.|$))([\w.]+\.)?Accounting(\..+)?$";

    /// <summary>Carpetas (relativas a <c>src/Core</c>) cuyo fuente se revisa con las expresiones de (2).</summary>
    private static readonly string[] CarpetasDeCartera =
    [
        Path.Combine("IngenIA365ERP.Application", "Lending"),
        Path.Combine("IngenIA365ERP.Domain", "Lending"),
        Path.Combine("IngenIA365ERP.Domain", "Entities", "Lending"),
    ];

    private static readonly Lazy<ArchUnitNET.Domain.Architecture> Arquitectura = new(() => new ArchLoader()
        .LoadAssemblies(
            typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly,
            typeof(IngenIA365ERP.Application.DependencyInjection).Assembly)
        .Build());

    private static string ConHijos(string ns) => $"^{Regex.Escape(ns)}(\\..+)?$";

    [Fact]
    public void Cartera_no_depende_de_Contabilidad_salvo_su_puerto()
    {
        foreach (var cartera in NamespacesDeCartera)
        {
            Types().That().ResideInNamespaceMatching(ConHijos(cartera))
                .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(ProhibidoParaCartera)
                .Because($"FR-126, T51: {cartera} pregunta a Contabilidad sólo por IContabilidadParaCartera y le avisa por mensajes")
                .Check(Arquitectura.Value);
        }
    }

    [Fact]
    public void Cartera_no_escribe_asientos_ni_conoce_cuentas()
    {
        var root = RepoPath.FindRepoRoot();
        var prohibidos = new List<(Regex Patron, string Que)>
        {
            (new Regex("\"ACC_", RegexOptions.Compiled), "una tabla ACC_"),
            (new Regex(@"\bAccountingPoster\b", RegexOptions.Compiled), "AccountingPoster"),
            (new Regex(@"IngenIA365ERP\.Domain\.Entities\.Accounting\b", RegexOptions.Compiled), "entidades contables"),
        };
        foreach (var dbSet in DbSetsDe("IngenIA365ERP.Domain.Entities.Accounting"))
            prohibidos.Add((new Regex($@"\b(db|_db|context|contexto|Db)\s*\.\s*{Regex.Escape(dbSet)}\b", RegexOptions.Compiled), $"el DbSet {dbSet}"));

        var infractores = new List<string>();
        var revisados = 0;
        foreach (var carpeta in CarpetasDeCartera)
        {
            var ruta = Path.Combine(root, "src", "Core", carpeta);
            Assert.True(Directory.Exists(ruta), $"No existe {ruta}: si el módulo se movió, actualizá CarpetasDeCartera.");
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

        Assert.True(revisados > 0, "No se revisó ningún archivo de Cartera: ¿cambió la estructura de carpetas?");
        Assert.True(infractores.Count == 0,
            "Cartera conoce Contabilidad fuera de su puerto (FR-126, E-01):\n  " + string.Join("\n  ", infractores));
    }

    [Fact]
    public void El_puerto_de_Contabilidad_para_Cartera_declara_exactamente_sus_cinco_metodos()
    {
        var puerto = typeof(IngenIA365ERP.Application.Common.Integration.Accounting.IContabilidadParaCartera);
        Assert.Equal("IngenIA365ERP.Application.Common.Integration.Accounting", puerto.Namespace);
        Assert.Equal(
            ["CuentasBancariasAsync", "EstadoDelPeriodoAsync", "EvaluarAsync", "IndicadoresDeLaEntidadAsync", "SaldosDeCuentasMapeadasAsync"],
            puerto.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName).Select(m => m.Name).Order(StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void El_consumidor_contable_no_depende_de_Cartera()
    {
        foreach (var cartera in new[] { "IngenIA365ERP.Domain.Entities.Lending", "IngenIA365ERP.Application.Lending" })
        {
            Types().That().ResideInNamespaceMatching(ConHijos("IngenIA365ERP.Application.Accounting.Lending"))
                .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(ConHijos(cartera))
                .Because("T51: Contabilidad lee lo que Cartera manda en los mensajes y le pregunta por IDimensionesDeCartera, no sus tablas ni su código")
                .Check(Arquitectura.Value);
        }
    }

    /// <summary>Los nombres de los <c>DbSet</c> de <see cref="IApplicationDbContext"/> cuyas entidades viven en esos espacios de nombres.</summary>
    private static IEnumerable<string> DbSetsDe(params string[] espacios) => typeof(IApplicationDbContext)
        .GetProperties()
        .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
        .Where(p => p.PropertyType.GetGenericArguments()[0].Namespace is { } ns
                    && espacios.Any(e => ns == e || ns.StartsWith(e + ".", StringComparison.Ordinal)))
        .Select(p => p.Name);
}
