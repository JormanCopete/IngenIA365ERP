namespace IngenIA365ERP.Architecture.Tests.Helpers;

/// <summary>
/// Feature 015 (Setup, T0001–T0005; decisiones-transversales §2.2; plan.md «Source Code»): el árbol de carpetas y de espacios de nombres
/// que recorren las pruebas de arquitectura de Recaudo y Tesorería. Una sola lista para todas, así una carpeta nueva se suma aquí y la
/// miran todas a la vez.
///
/// <para>
/// <b>Lo heredado de Tesorería queda fuera por nombre</b> (R-04): las entidades de la raíz de <c>Domain/Entities/Treasury</c>
/// (<c>Check</c>, <c>TreasuryConcept</c>, <c>TreasuryInvoice</c>, tablas <c>TRS_</c>), <c>Application/Treasury/{Checks, Invoices,
/// TreasuryConcepts}</c>, sus configuraciones de la raíz de <c>Persistence/Configurations/Treasury</c>, sus rutas
/// <c>API/Endpoints/Treasury/{Checks, TreasuryConcepts, TreasuryTransactions}Endpoints.cs</c> y sus cuatro pantallas de
/// <c>Pages/Tesoreria</c>. Todo eso convive sin cambios hasta su retiro después de T2 (fase 46) y ninguna regla de la 015 lo mira.
/// </para>
///
/// <para>
/// No se crean carpetas vacías ni archivos marcadores (T0005): cada carpeta nace con su primer archivo en la tarea que lo crea, y una
/// carpeta que todavía no existe no tiene nada que violar.
/// </para>
/// </summary>
internal static class RecaudoYTesoreria
{
    /// <summary>Espacios de nombres de Recaudo (con sus hijos).</summary>
    public static readonly string[] NamespacesDeRecaudo =
    [
        "IngenIA365ERP.Application.Receipts",
        "IngenIA365ERP.Domain.Receipts",
        "IngenIA365ERP.Domain.Entities.Receipts",
    ];

    /// <summary>
    /// Espacios de nombres de la Tesorería nueva. De <c>Domain.Entities.Treasury</c> sólo los hijos: la raíz es lo heredado.
    /// </summary>
    public static readonly string[] NamespacesDeTesoreria =
    [
        "IngenIA365ERP.Application.Treasury",
        "IngenIA365ERP.Domain.Treasury",
        "IngenIA365ERP.Domain.Entities.Treasury",
    ];

    /// <summary>Los espacios heredados de Tesorería, que ninguna regla de la 015 mira (R-04).</summary>
    public static readonly string[] NamespacesHeredadosDeTesoreria =
    [
        "IngenIA365ERP.Application.Treasury.Checks",
        "IngenIA365ERP.Application.Treasury.Invoices",
        "IngenIA365ERP.Application.Treasury.TreasuryConcepts",
    ];

    /// <summary>Carpetas de Recaudo relativas a <c>src/</c>, con barras normales.</summary>
    public static readonly string[] CarpetasDeRecaudo =
    [
        "Core/IngenIA365ERP.Application/Receipts",
        "Core/IngenIA365ERP.Domain/Receipts",
        "Core/IngenIA365ERP.Domain/Entities/Receipts",
        "Core/IngenIA365ERP.Domain/Enums/Receipts",
        "Infrastructure/IngenIA365ERP.Persistence/Configurations/Receipts",
        "Infrastructure/IngenIA365ERP.Persistence/Receipts",
    ];

    /// <summary>Carpetas de la Tesorería nueva relativas a <c>src/</c> (lo heredado lo quita <see cref="EsHeredadoDeTesoreria"/>).</summary>
    public static readonly string[] CarpetasDeTesoreria =
    [
        "Core/IngenIA365ERP.Application/Treasury",
        "Core/IngenIA365ERP.Domain/Treasury",
        "Core/IngenIA365ERP.Domain/Entities/Treasury",
        "Core/IngenIA365ERP.Domain/Enums/Treasury",
        "Infrastructure/IngenIA365ERP.Persistence/Configurations/Treasury",
        "Infrastructure/IngenIA365ERP.Persistence/Treasury",
    ];

    /// <summary>Las carpetas de lo heredado dentro de las de Tesorería (relativas a la raíz, barras normales, con barra final).</summary>
    private static readonly string[] CarpetasHeredadas =
    [
        "src/Core/IngenIA365ERP.Application/Treasury/Checks/",
        "src/Core/IngenIA365ERP.Application/Treasury/Invoices/",
        "src/Core/IngenIA365ERP.Application/Treasury/TreasuryConcepts/",
    ];

    /// <summary>Los archivos heredados en la raíz de las carpetas de Tesorería (relativos a la raíz, barras normales).</summary>
    private static readonly string[] ArchivosHeredados =
    [
        "src/Core/IngenIA365ERP.Domain/Entities/Treasury/Check.cs",
        "src/Core/IngenIA365ERP.Domain/Entities/Treasury/TreasuryConcept.cs",
        "src/Core/IngenIA365ERP.Domain/Entities/Treasury/TreasuryInvoice.cs",
        "src/Infrastructure/IngenIA365ERP.Persistence/Configurations/Treasury/CheckConfiguration.cs",
        "src/Infrastructure/IngenIA365ERP.Persistence/Configurations/Treasury/TreasuryConceptConfiguration.cs",
        "src/Infrastructure/IngenIA365ERP.Persistence/Configurations/Treasury/TreasuryInvoiceConfiguration.cs",
        "src/Presentation/IngenIA365ERP.API/Endpoints/Treasury/ChecksEndpoints.cs",
        "src/Presentation/IngenIA365ERP.API/Endpoints/Treasury/TreasuryConceptsEndpoints.cs",
        "src/Presentation/IngenIA365ERP.API/Endpoints/Treasury/TreasuryTransactionsEndpoints.cs",
        "src/Presentation/IngenIA365ERP.Shared/Pages/Tesoreria/Cheques.razor",
        "src/Presentation/IngenIA365ERP.Shared/Pages/Tesoreria/ConceptosTesoreria.razor",
        "src/Presentation/IngenIA365ERP.Shared/Pages/Tesoreria/FacturasTesoreria.razor",
        "src/Presentation/IngenIA365ERP.Shared/Pages/Tesoreria/FlujoCaja.razor",
    ];

    /// <summary>¿El archivo (ruta absoluta o relativa a la raíz) es de la Tesorería heredada?</summary>
    public static bool EsHeredadoDeTesoreria(string archivo)
    {
        var relativo = ReglaSobreFuente.Relativo(RepoPath.FindRepoRoot(), archivo);
        return ArchivosHeredados.Contains(relativo, StringComparer.Ordinal)
               || CarpetasHeredadas.Any(c => relativo.StartsWith(c, StringComparison.Ordinal));
    }

    /// <summary>Los archivos de Recaudo.</summary>
    public static IEnumerable<string> ArchivosDeRecaudo() => ReglaSobreFuente.ArchivosEn(CarpetasDeRecaudo);

    /// <summary>Los archivos de la Tesorería nueva, sin lo heredado.</summary>
    public static IEnumerable<string> ArchivosDeTesoreria() => ReglaSobreFuente.ArchivosEn(CarpetasDeTesoreria).Where(a => !EsHeredadoDeTesoreria(a));

    /// <summary>¿El espacio de nombres es de Recaudo?</summary>
    public static bool EsDeRecaudo(string? ns) => ns is not null && NamespacesDeRecaudo.Any(n => ns == n || ns.StartsWith(n + ".", StringComparison.Ordinal));

    /// <summary>¿El espacio de nombres es de la Tesorería nueva? La raíz de <c>Domain.Entities.Treasury</c> y los heredados no lo son.</summary>
    public static bool EsDeTesoreria(string? ns)
    {
        if (ns is null || ns == "IngenIA365ERP.Domain.Entities.Treasury") return false;
        if (NamespacesHeredadosDeTesoreria.Any(n => ns == n || ns.StartsWith(n + ".", StringComparison.Ordinal))) return false;
        return NamespacesDeTesoreria.Any(n => ns == n || ns.StartsWith(n + ".", StringComparison.Ordinal));
    }

    /// <summary>Los tipos de un ensamblado de los dos módulos (sin los generados por el compilador).</summary>
    public static IEnumerable<Type> TiposDe(System.Reflection.Assembly ensamblado, Func<string?, bool> delModulo) =>
        ensamblado.GetTypes().Where(t => delModulo(t.Namespace) && !t.Name.Contains('<', StringComparison.Ordinal));
}
