namespace IngenIA365ERP.Architecture.Tests.Helpers;

/// <summary>
/// Feature 015 (Setup, T0001–T0005; decisiones-transversales §2.2; plan.md «Source Code»): el árbol de carpetas y de espacios de nombres
/// que recorren las pruebas de arquitectura de Recaudo y Tesorería. Una sola lista para todas, así una carpeta nueva se suma aquí y la
/// miran todas a la vez.
///
/// <para>
/// <b>Lo heredado de Tesorería ya no existe</b> (R-04; fase 46, <c>RetiroDeLaTesoreriaHeredada</c>): hasta el retiro quedaban fuera por nombre las
/// entidades de la raíz de <c>Domain/Entities/Treasury</c> (<c>Check</c>, <c>TreasuryConcept</c>, <c>TreasuryInvoice</c>), <c>Application/Treasury/{Checks,
/// Invoices, TreasuryConcepts}</c>, sus configuraciones, sus rutas y sus cuatro pantallas. Desde el retiro las reglas de la 015 miran las carpetas enteras, y
/// <see cref="Principles.LaTesoreriaHeredadaNoVuelve"/> vigila que ningún nombre retirado reaparezca.
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

    /// <summary>Espacios de nombres de la Tesorería nueva (con sus hijos).</summary>
    public static readonly string[] NamespacesDeTesoreria =
    [
        "IngenIA365ERP.Application.Treasury",
        "IngenIA365ERP.Domain.Treasury",
        "IngenIA365ERP.Domain.Entities.Treasury",
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

    /// <summary>Carpetas de la Tesorería relativas a <c>src/</c>.</summary>
    public static readonly string[] CarpetasDeTesoreria =
    [
        "Core/IngenIA365ERP.Application/Treasury",
        "Core/IngenIA365ERP.Domain/Treasury",
        "Core/IngenIA365ERP.Domain/Entities/Treasury",
        "Core/IngenIA365ERP.Domain/Enums/Treasury",
        "Infrastructure/IngenIA365ERP.Persistence/Configurations/Treasury",
        "Infrastructure/IngenIA365ERP.Persistence/Treasury",
    ];

    /// <summary>Los archivos de Recaudo.</summary>
    public static IEnumerable<string> ArchivosDeRecaudo() => ReglaSobreFuente.ArchivosEn(CarpetasDeRecaudo);

    /// <summary>Los archivos de la Tesorería.</summary>
    public static IEnumerable<string> ArchivosDeTesoreria() => ReglaSobreFuente.ArchivosEn(CarpetasDeTesoreria);

    /// <summary>¿El espacio de nombres es de Recaudo?</summary>
    public static bool EsDeRecaudo(string? ns) => ns is not null && NamespacesDeRecaudo.Any(n => ns == n || ns.StartsWith(n + ".", StringComparison.Ordinal));

    /// <summary>¿El espacio de nombres es de la Tesorería?</summary>
    public static bool EsDeTesoreria(string? ns) => ns is not null && NamespacesDeTesoreria.Any(n => ns == n || ns.StartsWith(n + ".", StringComparison.Ordinal));

    /// <summary>Los tipos de un ensamblado de los dos módulos (sin los generados por el compilador).</summary>
    public static IEnumerable<Type> TiposDe(System.Reflection.Assembly ensamblado, Func<string?, bool> delModulo) =>
        ensamblado.GetTypes().Where(t => delModulo(t.Namespace) && !t.Name.Contains('<', StringComparison.Ordinal));
}
