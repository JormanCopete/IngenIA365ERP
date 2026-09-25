using System.Text.RegularExpressions;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace IngenIA365ERP.Architecture.Tests.Principles;

/// <summary>
/// Feature 012, T001 (decisiones-transversales T31, §2.18), FR-014: Inventario no lee ni escribe
/// datos de Contabilidad ni de Cartera. Lo que necesita de ellos lo pide por los puertos
/// <c>IContabilidadParaInventario</c> e <c>IConsultasDeCartera</c> (Application/Common/Integration),
/// que no viven en los espacios de nombres prohibidos; todo lo demás cruza por mensajes.
///
/// <para>
/// Esqueleto del Setup: <see cref="NamespacesDeInventario"/> empieza vacía y la prueba recorre la
/// lista afirmando la regla sobre cada elemento; con la lista vacía pasa porque no hay nada que
/// violar, no por un <c>return</c> temprano. La llena el bloque dueño del módulo nuevo (fase 3,
/// base de inventario) cuando aparezcan los espacios de nombres <c>Inventory</c> del módulo nuevo.
/// </para>
/// </summary>
public class InventarioNoConoceContabilidadNiCartera
{
    /// <summary>Espacios de nombres del módulo comercial (con sus hijos). Los agrega el bloque dueño.</summary>
    private static readonly string[] NamespacesDeInventario = [];

    /// <summary>Lo que Inventario no puede tocar: casos de uso y entidades de Contabilidad y Cartera.</summary>
    private static readonly string[] NamespacesProhibidos =
    [
        "IngenIA365ERP.Application.Accounting",
        "IngenIA365ERP.Application.Lending",
        "IngenIA365ERP.Domain.Entities.Accounting",
        "IngenIA365ERP.Domain.Entities.Lending",
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
            foreach (var prohibido in NamespacesProhibidos)
            {
                Types().That().ResideInNamespaceMatching(ConHijos(inventario))
                    .Should().NotDependOnAnyTypesThat().ResideInNamespaceMatching(ConHijos(prohibido))
                    .Because($"FR-014: {inventario} pide a Contabilidad y Cartera sólo por sus puertos, nunca por {prohibido}")
                    .WithoutRequiringPositiveResults()
                    .Check(Arquitectura.Value);
            }
        }
    }
}
