using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;

/// <summary>
/// Las versiones de mensaje que Contabilidad acepta, <b>por módulo de origen</b> (feature 012, T514; feature 013, T0074, T15;
/// contracts/mensajes.md §12 de la 012 y §17 de la 013): de Inventario, la v1 de los dieciocho tipos a Contabilidad; de Cartera, la
/// v1 de los 35 hechos (los de K3 y K4 aceptados desde K1; los tres contingentes mientras rija A3). Salen del catálogo agregado.
/// Una versión no aceptada queda <c>Rejected</c> con <c>Integration.VersionNotAccepted</c> y nunca se reescribe. Al publicar una
/// v2 se agrega sin quitar la v1 mientras quede alguna entrega v1 sin procesar.
///
/// <para>
/// <see cref="Acepta(string, int)"/> responde por lo que el consumidor contable consume hoy, que es lo de Inventario: el reparto de
/// lo de Cartera a <c>PostLendingFactsCommand</c> lo agrega la fase de US7, y con él <c>DestinoContabilidad</c> pasa a aceptar
/// <see cref="DeCartera"/>.
/// </para>
/// </summary>
public static class VersionesAceptadas
{
    /// <summary>Las versiones aceptadas de cada módulo de origen.</summary>
    public static IReadOnlyDictionary<string, IReadOnlySet<(string Type, int Version)>> PorModulo { get; } =
        new[] { ModulosDeOrigen.Inventario, ModulosDeOrigen.Cartera }.ToDictionary(
            m => m,
            m => (IReadOnlySet<(string Type, int Version)>)CatalogoDeMensajes.DelModulo(m)
                .Where(t => t.AdmiteDestino(IntegrationDestinations.Accounting))
                .Select(t => (t.Type, t.Version))
                .ToHashSet(),
            StringComparer.Ordinal);

    /// <summary>Las de Inventario: las que hoy consume Contabilidad.</summary>
    public static IReadOnlySet<(string Type, int Version)> Todas => PorModulo[ModulosDeOrigen.Inventario];

    /// <summary>Las de Cartera (35 en V1), que consume Contabilidad desde US7.</summary>
    public static IReadOnlySet<(string Type, int Version)> DeCartera => PorModulo[ModulosDeOrigen.Cartera];

    /// <summary>¿Acepta hoy esta versión de este tipo? (lo de Inventario).</summary>
    public static bool Acepta(string type, int version) => Todas.Contains((type, version));

    /// <summary>¿Acepta esta versión de este tipo de este módulo?</summary>
    public static bool Acepta(string modulo, string type, int version) =>
        PorModulo.TryGetValue(modulo, out var del) && del.Contains((type, version));
}
