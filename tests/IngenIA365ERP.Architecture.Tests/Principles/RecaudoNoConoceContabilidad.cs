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
/// Feature 015, T0001 (R-22, R-37; FR-004; contracts/interfaces.md §3; contracts/contabilidad.md §1): Recaudo nunca conoce una cuenta ni
/// escribe asientos. Lo que cruza a Contabilidad cruza por hechos <c>REC → Accounting</c> nacidos en la transacción de la operación; lo que
/// Recaudo le pregunta, por <c>IContabilidadParaRecaudo</c> (<c>Application.Common.Integration.Accounting</c>). Molde:
/// <see cref="CarteraNoConoceContabilidad"/>.
///
/// <para>
/// Árbol que recorre: <see cref="RecaudoYTesoreria.NamespacesDeRecaudo"/> y <see cref="RecaudoYTesoreria.CarpetasDeRecaudo"/>. Las listas
/// nacen vacías en el Setup: <see cref="Puertos"/> la llena la fase 2 (T0041) con el puerto y sus cuatro métodos, y
/// <see cref="ConsumidoresContables"/> la fase 16 (US8) con <c>Application.Accounting.Receipts</c>, que lee lo que Recaudo manda en los
/// mensajes y le pregunta por <c>IDimensionesDeRecaudo</c>, nunca sus tablas.
/// </para>
/// </summary>
public class RecaudoNoConoceContabilidad
{
    /// <summary>Todo espacio con un segmento <c>Accounting</c>, salvo los puertos de la plataforma.</summary>
    // Fase 7 (T0145): el emisor de hechos del propio módulo (Application.Receipts.Accounting) no es Contabilidad: es el lado del módulo que arma los hechos.
    private const string Prohibido = @"^IngenIA365ERP\.(?!Application\.Common\.Integration\.Accounting(\.|$))(?!Application\.Receipts\.Accounting(\.|$))([\w.]+\.)?Accounting(\..+)?$";

    /// <summary>Los puertos contables de Recaudo y exactamente sus métodos (orden ordinal).</summary>
    private static readonly (string Tipo, string[] Metodos)[] Puertos =
    [
        // Fase 2 (T0041; interfaces.md §3).
        ("IngenIA365ERP.Application.Common.Integration.Accounting.IContabilidadParaRecaudo",
            ["CuentasPorCobrarComercialesAsync", "EstadoDelPeriodoAsync", "EvaluarAsync", "SaldoContableAsync"]),
    ];

    /// <summary>Los espacios del consumidor contable de Recaudo, que no dependen de Recaudo (la inversa).</summary>
    private static readonly string[] ConsumidoresContables =
    [
    ];

    private static readonly Lazy<ArchUnitNET.Domain.Architecture> Arquitectura = new(() => new ArchLoader()
        .LoadAssemblies(
            typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly,
            typeof(IngenIA365ERP.Application.DependencyInjection).Assembly)
        .Build());

    [Fact]
    public void Recaudo_no_depende_de_Contabilidad_salvo_su_puerto() =>
        ReglaContable.SinDependencia(Arquitectura.Value, RecaudoYTesoreria.NamespacesDeRecaudo, RecaudoYTesoreria.EsDeRecaudo, Prohibido,
            "FR-004, R-22: Recaudo pregunta a Contabilidad sólo por IContabilidadParaRecaudo y le avisa por hechos");

    [Fact]
    public void Recaudo_no_escribe_asientos_ni_conoce_cuentas() =>
        ReglaContable.SinNombresContables(RecaudoYTesoreria.ArchivosDeRecaudo(), "Recaudo conoce Contabilidad fuera de su puerto (FR-004, E-04)");

    [Fact]
    public void Cada_puerto_contable_declara_exactamente_sus_metodos() =>
        ReglaContable.MetodosExactos(Puertos);

    [Fact]
    public void El_consumidor_contable_no_depende_de_Recaudo() =>
        ReglaContable.ConsumidorSinDependencia(Arquitectura.Value, ConsumidoresContables, RecaudoYTesoreria.NamespacesDeRecaudo,
            "R-22: Contabilidad lee lo que Recaudo manda en los mensajes y le pregunta por IDimensionesDeRecaudo, no sus tablas ni su código");
}

/// <summary>
/// Lo común de <see cref="RecaudoNoConoceContabilidad"/> y <see cref="TesoreriaNoConoceContabilidad"/> (feature 015, T0001): las cuatro
/// comprobaciones del molde de Cartera con el módulo como parámetro.
/// </summary>
internal static class ReglaContable
{
    private static string ConHijos(string ns) => $"^{Regex.Escape(ns)}(\\..+)?$";

    /// <summary>
    /// El principio de la expresión que deja fuera lo heredado de Tesorería (R-04): la raíz de <c>Domain.Entities.Treasury</c> y
    /// <c>Application.Treasury.{Checks, Invoices, TreasuryConcepts}</c>.
    /// </summary>
    private const string SinLoHeredado =
        @"^(?!IngenIA365ERP\.Domain\.Entities\.Treasury$)(?!IngenIA365ERP\.Application\.Treasury\.(Checks|Invoices|TreasuryConcepts)(\.|$))";

    /// <summary>(1) ArchUnit: el módulo no depende de un espacio contable salvo el de los puertos. Un espacio sin tipos no tiene nada que violar.</summary>
    public static void SinDependencia(ArchUnitNET.Domain.Architecture arquitectura, IEnumerable<string> espacios, Func<string?, bool> delModulo,
        string prohibido, string porque)
    {
        var presentes = new[] { typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly, typeof(IngenIA365ERP.Application.DependencyInjection).Assembly }
            .SelectMany(a => a.GetTypes()).Select(t => t.Namespace).Where(delModulo).ToHashSet(StringComparer.Ordinal);
        foreach (var espacio in espacios.Where(e => presentes.Any(p => p == e || p!.StartsWith(e + ".", StringComparison.Ordinal))))
        {
            Types().That().ResideInNamespaceMatching(SinLoHeredado + ConHijos(espacio)[1..])
                .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(prohibido)
                .Because(porque)
                .Check(arquitectura);
        }
    }

    /// <summary>(2) El fuente no nombra tablas <c>ACC_</c>, <c>AccountingPoster</c>, entidades contables ni los <c>DbSet</c> contables.</summary>
    public static void SinNombresContables(IEnumerable<string> archivos, string mensaje)
    {
        var prohibidos = new List<(Regex Patron, string Que)>
        {
            (new Regex("\"ACC_", RegexOptions.Compiled), "una tabla ACC_"),
            (new Regex(@"\bAccountingPoster\b", RegexOptions.Compiled), "AccountingPoster"),
            (new Regex(@"IngenIA365ERP\.Domain\.Entities\.Accounting\b", RegexOptions.Compiled), "entidades contables"),
        };
        foreach (var dbSet in typeof(IApplicationDbContext).GetProperties()
                     .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
                     .Where(p => p.PropertyType.GetGenericArguments()[0].Namespace is { } ns
                                 && (ns == "IngenIA365ERP.Domain.Entities.Accounting" || ns.StartsWith("IngenIA365ERP.Domain.Entities.Accounting.", StringComparison.Ordinal)))
                     .Select(p => p.Name))
            prohibidos.Add((new Regex($@"\b(db|_db|context|contexto|Db)\s*\.\s*{Regex.Escape(dbSet)}\b", RegexOptions.Compiled), $"el DbSet {dbSet}"));

        var root = RepoPath.FindRepoRoot();
        var infractores = new List<string>();
        foreach (var archivo in archivos)
        {
            var texto = FuenteSinComentarios.Leer(archivo);
            foreach (var (patron, que) in prohibidos.Where(p => p.Patron.IsMatch(texto)))
                infractores.Add($"{ReglaSobreFuente.Relativo(root, archivo)}: nombra {que}");
        }
        ReglaSobreFuente.SinInfracciones(infractores, mensaje);
    }

    /// <summary>(3) Cada puerto está en <c>Application.Common.Integration.Accounting</c> y declara exactamente sus métodos.</summary>
    public static void MetodosExactos(IEnumerable<(string Tipo, string[] Metodos)> puertos)
    {
        var ensamblado = typeof(IngenIA365ERP.Application.DependencyInjection).Assembly;
        foreach (var (nombre, metodos) in puertos)
        {
            var puerto = ensamblado.GetType(nombre);
            Assert.True(puerto is not null, $"El puerto {nombre} no existe.");
            Assert.Equal("IngenIA365ERP.Application.Common.Integration.Accounting", puerto!.Namespace);
            Assert.Equal(metodos.Order(StringComparer.Ordinal).ToList(),
                puerto.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(m => !m.IsSpecialName).Select(m => m.Name).Order(StringComparer.Ordinal).ToList());
        }
    }

    /// <summary>(4) La inversa: el consumidor contable no depende de las entidades ni de la aplicación del módulo.</summary>
    public static void ConsumidorSinDependencia(ArchUnitNET.Domain.Architecture arquitectura, IEnumerable<string> consumidores,
        IEnumerable<string> espaciosDelModulo, string porque)
    {
        foreach (var consumidor in consumidores)
            foreach (var modulo in espaciosDelModulo)
            {
                Types().That().ResideInNamespaceMatching(ConHijos(consumidor))
                    .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(ConHijos(modulo))
                    .Because(porque)
                    .Check(arquitectura);
            }
    }
}
