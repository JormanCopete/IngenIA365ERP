using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;

/// <summary>
/// Las versiones de mensaje que Contabilidad acepta (feature 012, T514; contracts/mensajes.md §12): hoy la v1 de los dieciocho
/// tipos a Contabilidad del catálogo. Una versión no aceptada queda <c>Rejected</c> con <c>Integration.VersionNotAccepted</c> y
/// nunca se reescribe. Al publicar una v2 se agrega aquí sin quitar la v1 mientras quede alguna entrega v1 sin procesar.
/// </summary>
public static class VersionesAceptadas
{
    public static IReadOnlySet<(string Type, int Version)> Todas { get; } = CatalogoDeMensajesV1.Todos
        .Where(t => t.Destination == IntegrationDestinations.Accounting)
        .Select(t => (t.Type, t.Version))
        .ToHashSet();

    public static bool Acepta(string type, int version) => Todas.Contains((type, version));
}
