using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Documents;

/// <summary>
/// Los permisos con que se ve, crea, confirma y anula un documento de cada grupo (feature 012, T48; contracts/api.md
/// §9.1; decisiones-transversales §2.10). Nulo = el grupo no tiene esa acción por el ciclo común (el ajuste de costo lo
/// genera el sistema; los conteos se cierran por su propio ciclo). <c>Costing</c> se ve con <c>Inventory.Costs.Read</c>
/// además de <c>Inventory.Documents.View</c>. (nuevo)
/// </summary>
public sealed record PermisosDeGrupo(string View, string? Create, string? Confirm, string? Void)
{
    public const string VerDocumentos = "Inventory.Documents.View";
    public const string LeerCostos = "Inventory.Costs.Read";
    public const string VerMensajes = "Inventory.Messages.View";

    /// <summary>El permiso de ver, crear, confirmar y anular de cada grupo.</summary>
    public static PermisosDeGrupo De(DocumentClassGroup grupo) => grupo switch
    {
        DocumentClassGroup.Purchases => Familia("Inventory.Purchases"),
        DocumentClassGroup.Adjustments => Familia("Inventory.Adjustments"),
        DocumentClassGroup.Transfers => new("Inventory.Transfers.View", "Inventory.Transfers.Create", "Inventory.Transfers.Dispatch", "Inventory.Transfers.Void"),
        DocumentClassGroup.Counts => new("Inventory.Counts.View", "Inventory.Counts.Open", "Inventory.Counts.Close", null),
        DocumentClassGroup.Sales => Familia("Inventory.Sales"),
        DocumentClassGroup.Cash => new("Inventory.CashSessions.View", "Inventory.CashMovements.Create", "Inventory.CashMovements.Create", null),
        DocumentClassGroup.OpeningBalance => new("Inventory.OpeningBalance.Load", "Inventory.OpeningBalance.Load", "Inventory.OpeningBalance.Load", "Inventory.OpeningBalance.Load"),
        DocumentClassGroup.Costing => new(LeerCostos, null, null, null),
        _ => throw new ArgumentOutOfRangeException(nameof(grupo), grupo, "Grupo de documento desconocido."),
    };

    /// <summary>
    /// El permiso cuyo monto máximo se compara al confirmar (contracts/api.md §1.3), o nulo si el grupo no admite límite.
    /// </summary>
    public string? PermisoLimitado => Confirm is { } c && PermisosLimitables.Admite(c) ? c : null;

    private static PermisosDeGrupo Familia(string prefijo) => new($"{prefijo}.View", $"{prefijo}.Create", $"{prefijo}.Confirm", $"{prefijo}.Void");
}
