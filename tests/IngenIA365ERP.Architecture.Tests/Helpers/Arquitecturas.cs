using ArchUnitNET.Loader;

namespace IngenIA365ERP.Architecture.Tests.Helpers;

/// <summary>
/// Los grafos de ArchUnitNET de la suite, construidos <b>una sola vez</b> por corrida y compartidos.
/// <para>
/// Hasta el 2026-11-14 cada una de las diez clases que usan ArchUnitNET construía su propio grafo de Domain y
/// Application (y dos, además, de Persistence e Identity) y lo guardaba en un campo estático hasta el final de la
/// corrida. Medido en la máquina del dueño: esas diez clases solas llegaban a 22 GB y tardaban 2 min; con dos
/// sesiones corriendo la suite a la vez, Windows se quedaba sin memoria comprometida y se colgaba. Ahora hay un
/// grafo por conjunto de ensamblados, el mismo conjunto que cada clase cargaba antes (no cambia qué ve cada regla).
/// </para>
/// <para>
/// Las clases que los usan van en la colección <see cref="Coleccion"/>: xUnit corre en serie las clases de una
/// misma colección, así nadie recorre un grafo compartido mientras otra clase lo está usando (los cachés internos
/// de ArchUnitNET no se garantizan seguros entre hilos).
/// </para>
/// </summary>
internal static class Arquitecturas
{
    public const string Coleccion = "Grafos de ArchUnitNET compartidos";

    /// <summary>Domain y Application.</summary>
    public static readonly Lazy<ArchUnitNET.Domain.Architecture> DominioYAplicacion = new(() => new ArchLoader()
        .LoadAssemblies(
            typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly,
            typeof(IngenIA365ERP.Application.DependencyInjection).Assembly)
        .Build());

    /// <summary>Domain, Application, Persistence e Identity.</summary>
    public static readonly Lazy<ArchUnitNET.Domain.Architecture> ConPersistenciaEIdentidad = new(() => new ArchLoader()
        .LoadAssemblies(
            typeof(IngenIA365ERP.Domain.Common.BaseEntity).Assembly,
            typeof(IngenIA365ERP.Application.DependencyInjection).Assembly,
            typeof(IngenIA365ERP.Persistence.DbContext.AdminDbContext).Assembly,
            typeof(IngenIA365ERP.Identity.CentralIdentity.CentralJwtIssuer).Assembly)
        .Build());
}
