using System.Text.RegularExpressions;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Architecture.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 015, T0001 (R-05; FR-003; contracts/interfaces.md §2; data-model §14): Recaudo y Tesorería comparten caja, medios, cuentas bancarias
/// y remesas, pero <b>no se leen las tablas</b>: ningún tipo de <c>Receipts</c> nombra un <c>DbSet</c> ni una tabla <c>TES_</c> de Tesorería, ni
/// al revés; lo que cruza va por los siete puertos de <c>Application/Common/Integration/{Receipts,Treasury}</c> y se guarda como
/// <c>…PublicId</c> y número, sin FK entre <c>RCD_</c> y <c>TES_</c>.
///
/// <para>
/// Árbol que recorre (DT §2.2, plan.md «Source Code»): <see cref="RecaudoYTesoreria.CarpetasDeRecaudo"/> y
/// <see cref="RecaudoYTesoreria.CarpetasDeTesoreria"/>, sin lo heredado de Tesorería (R-04). La lista de puertos <see cref="Puertos"/> nace
/// vacía en el Setup y la llena la fase 2 (T0040) con los siete puertos y su dueño; cada historia que agregue un método lo agrega a su puerto,
/// no a un puerto nuevo.
/// </para>
/// </summary>
public class RecaudoYTesoreriaSeHablanPorSusPuertos
{
    /// <summary>Los puertos entre los dos módulos: nombre completo del tipo y el espacio de nombres donde vive (el del módulo que lo ofrece).</summary>
    private static readonly (string Tipo, string EspacioDeNombres)[] Puertos =
    [
    ];

    private static readonly Lazy<ArchUnitNET.Domain.Architecture> Arquitectura = new(() => new ArchLoader()
        .LoadAssemblies(
            typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly,
            typeof(IngenIA365ERP.Application.DependencyInjection).Assembly)
        .Build());

    [Fact]
    public void Cada_puerto_existe_es_una_interfaz_y_vive_en_los_puertos_de_la_plataforma()
    {
        var ensamblado = typeof(IngenIA365ERP.Application.DependencyInjection).Assembly;
        var infracciones = new List<string>();
        foreach (var (tipo, espacio) in Puertos)
        {
            var encontrado = ensamblado.GetType(tipo);
            if (encontrado is null) { infracciones.Add($"{tipo}: no existe"); continue; }
            if (!encontrado.IsInterface) infracciones.Add($"{tipo}: no es una interfaz");
            if (encontrado.Namespace != espacio) infracciones.Add($"{tipo}: vive en {encontrado.Namespace} y no en {espacio}");
        }
        ReglaSobreFuente.SinInfracciones(infracciones, "Un puerto entre Recaudo y Tesorería no está donde dice contracts/interfaces.md §2 (R-05)");
    }

    [Fact]
    public void Recaudo_no_nombra_las_tablas_ni_los_DbSets_de_Tesoreria_ni_al_reves()
    {
        var infracciones = new List<string>();
        infracciones.AddRange(Nombra(RecaudoYTesoreria.ArchivosDeRecaudo(), "\"TES_", DbSetsDe(RecaudoYTesoreria.EsDeTesoreria), "Tesorería"));
        infracciones.AddRange(Nombra(RecaudoYTesoreria.ArchivosDeTesoreria(), "\"RCD_", DbSetsDe(RecaudoYTesoreria.EsDeRecaudo), "Recaudo"));
        ReglaSobreFuente.SinInfracciones(infracciones, "Recaudo y Tesorería se leen las tablas en vez de hablarse por sus puertos (FR-003, R-05)");
    }

    /// <summary>
    /// Por tipos (ArchUnit): ninguna entidad ni caso de uso de un módulo depende de las entidades del otro. Un espacio de nombres que todavía
    /// no tiene tipos no tiene nada que violar, como la carpeta que todavía no existe de <see cref="ReglaSobreFuente"/>.
    /// </summary>
    [Fact]
    public void Ningun_tipo_de_un_modulo_depende_de_las_entidades_del_otro()
    {
        var ensamblados = new[] { typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly, typeof(IngenIA365ERP.Application.DependencyInjection).Assembly };
        var conTipos = ensamblados.SelectMany(a => a.GetTypes()).Select(t => t.Namespace).Where(n => n is not null).ToHashSet(StringComparer.Ordinal);

        foreach (var recaudo in RecaudoYTesoreria.NamespacesDeRecaudo.Where(n => conTipos.Any(c => c == n || c!.StartsWith(n + ".", StringComparison.Ordinal))))
        {
            Types().That().ResideInNamespaceMatching(ConHijos(recaudo))
                .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(@"^IngenIA365ERP\.Domain\.Entities\.Treasury\..+$")
                .Because("FR-003, R-05: Recaudo pide a Tesorería por ICuentasBancariasPropias, IMovimientosBancariosParaRecaudo, IRemesasDeTesoreria e IOrdenesDePagoDeTesoreria")
                .Check(Arquitectura.Value);
        }

        var tesoreriaNueva = new[] { "IngenIA365ERP.Domain.Treasury", "IngenIA365ERP.Domain.Entities.Treasury" };
        foreach (var tesoreria in tesoreriaNueva.Where(n => conTipos.Any(c => c!.StartsWith(n + ".", StringComparison.Ordinal))))
        {
            Types().That().ResideInNamespaceMatching($@"^{Regex.Escape(tesoreria)}\..+$")
                .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(ConHijos("IngenIA365ERP.Domain.Entities.Receipts"))
                .Because("FR-003, R-05: Tesorería pide a Recaudo por IRecaudoParaConciliacion, IPartidasDesdeConciliacion e IResultadoDeOrdenesDeRecaudo")
                .Check(Arquitectura.Value);
        }
    }

    private static string ConHijos(string ns) => $"^{Regex.Escape(ns)}(\\..+)?$";

    private static IEnumerable<string> Nombra(IEnumerable<string> archivos, string tabla, IEnumerable<string> dbSets, string otro)
    {
        var root = RepoPath.FindRepoRoot();
        var patrones = new List<Regex> { new(Regex.Escape(tabla), RegexOptions.Compiled) };
        patrones.AddRange(dbSets.Select(d => new Regex($@"\b(db|_db|context|contexto|Db)\s*\.\s*{Regex.Escape(d)}\b", RegexOptions.Compiled)));
        foreach (var archivo in archivos)
        {
            var texto = FuenteSinComentarios.Leer(archivo);
            foreach (var patron in patrones.Where(p => p.IsMatch(texto)))
                yield return $"{ReglaSobreFuente.Relativo(root, archivo)}: nombra {patron} de {otro}";
        }
    }

    /// <summary>Los <c>DbSet</c> de <see cref="IApplicationDbContext"/> cuyas entidades son del módulo.</summary>
    private static IEnumerable<string> DbSetsDe(Func<string?, bool> delModulo) => typeof(IApplicationDbContext)
        .GetProperties()
        .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
        .Where(p => delModulo(p.PropertyType.GetGenericArguments()[0].Namespace))
        .Select(p => p.Name);
}
