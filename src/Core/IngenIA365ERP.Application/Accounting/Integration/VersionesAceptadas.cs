using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Accounting.Integration;

/// <summary>
/// Las versiones de mensaje que Contabilidad acepta, <b>por módulo de origen</b> (feature 012, T514; feature 013, T0074, T0644, T15;
/// contracts/mensajes.md §12 de la 012 y §17 de la 013; contracts/contabilidad.md §6): de Inventario, la v1 de los dieciocho tipos a
/// Contabilidad; de Cartera, la v1 de los 35 hechos (los de K3 y K4 aceptados desde K1; los tres contingentes mientras rija A3). Salen del
/// catálogo agregado. Una versión no aceptada queda <c>Rejected</c> con <c>Integration.VersionNotAccepted</c> y nunca se reescribe. Al
/// publicar una v2 se agrega sin quitar la v1 mientras quede alguna entrega v1 sin procesar.
///
/// <para>
/// Se mudó de <c>Accounting/Inventory/Contabilizacion</c> a la carpeta común (C82) sin cambiar lo que responde por Inventario:
/// <see cref="Acepta(string, int)"/> busca el tipo en el catálogo agregado y responde con las versiones de <b>su</b> módulo.
/// </para>
/// </summary>
public static class VersionesAceptadas
{
    /// <summary>Las versiones aceptadas de cada módulo de origen.</summary>
    public static IReadOnlyDictionary<string, IReadOnlySet<(string Type, int Version)>> PorModulo { get; } =
        new[] { ModulosDeOrigen.Inventario, ModulosDeOrigen.Cartera, ModulosDeOrigen.Recaudo, ModulosDeOrigen.Tesoreria }.ToDictionary(
            m => m,
            m => (IReadOnlySet<(string Type, int Version)>)CatalogoDeMensajes.DelModulo(m)
                .Where(t => t.AdmiteDestino(IntegrationDestinations.Accounting))
                .Select(t => (t.Type, t.Version))
                .ToHashSet(),
            StringComparer.Ordinal);

    /// <summary>Las de Inventario.</summary>
    public static IReadOnlySet<(string Type, int Version)> Todas => PorModulo[ModulosDeOrigen.Inventario];

    /// <summary>Las de Cartera (35 en V1).</summary>
    public static IReadOnlySet<(string Type, int Version)> DeCartera => PorModulo[ModulosDeOrigen.Cartera];

    /// <summary>Las de Recaudo y las de Tesorería (feature 015, T0258; contabilidad.md §3, §4).</summary>
    public static IReadOnlySet<(string Type, int Version)> DeRecaudo => PorModulo[ModulosDeOrigen.Recaudo];

    public static IReadOnlySet<(string Type, int Version)> DeTesoreria => PorModulo[ModulosDeOrigen.Tesoreria];

    /// <summary>¿Acepta esta versión de este tipo? Busca el tipo en el catálogo agregado y responde por su módulo de origen (§6).</summary>
    public static bool Acepta(string type, int version) =>
        CatalogoDeMensajes.Buscar(type, version) is { } tipo && Acepta(tipo.OriginModule, type, version);

    /// <summary>¿Acepta esta versión de este tipo de este módulo?</summary>
    public static bool Acepta(string modulo, string type, int version) =>
        PorModulo.TryGetValue(modulo, out var del) && del.Contains((type, version));

    /// <summary>El módulo de origen que el catálogo agregado le da a un tipo (nulo si no está).</summary>
    public static string? ModuloDe(string type, int version) => CatalogoDeMensajes.Buscar(type, version)?.OriginModule;
}
