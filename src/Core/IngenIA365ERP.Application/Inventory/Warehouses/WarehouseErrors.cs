using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Inventory.Warehouses;

/// <summary>
/// Los códigos de error de bodegas, ubicaciones y políticas de reorden (feature 012, T212, T225; contracts/api.md §4.5).
/// Una bodega inexistente o fuera del alcance es el <b>mismo</b> 404 (<c>Inventory.Warehouse.NotFound</c>,
/// <see cref="ErroresDeAlcance"/>). El aviso <c>Inventory.Branch.MunicipalityMissing</c> no detiene nada: viaja en
/// <c>warnings</c>.
/// </summary>
public static class WarehouseErrors
{
    public const string MunicipalityMissingCode = "Inventory.Branch.MunicipalityMissing";

    public static Error WarehouseNotFound() => ErroresDeAlcance.BodegaInexistente();

    public static Error WarehouseTypeNotFound(string? codigo = null) => new("Inventory.WarehouseType.NotFound",
        codigo is null ? "El tipo de bodega no existe." : $"El tipo de bodega «{codigo}» no existe.");

    public static Error LocationNotFound() => new("Inventory.Location.NotFound", "La ubicación no existe en esta bodega.");

    public static Error ReorderPolicyNotFound() => new("Inventory.ReorderPolicy.NotFound", "La política de reorden no existe.");

    public static Error BranchNotFound(string? codigo = null) => new("Inventory.Branch.NotFound",
        codigo is null ? "La sucursal no existe." : $"La sucursal «{codigo}» no existe.");

    // ------------------------------------------------------------------------------ tipos --

    public static Error WarehouseTypeTransitIsSystem() => new("Inventory.WarehouseType.TransitIsSystem",
        "La bodega de tránsito la crea el sistema con la primera bodega operativa de la sucursal: no se da de alta aparte ni se crea otro tipo de tránsito.");

    public static Error WarehouseTypeInUse(int warehouses) => new ErrorConDatos("Inventory.WarehouseType.InUse",
        $"El tipo lo usan {warehouses} bodega(s) activa(s).", new { warehouses });

    // ------------------------------------------------------------------------------ bodegas --

    public static Error WarehouseBehaviorLocked() => new("Inventory.Warehouse.BehaviorLocked",
        "El tipo sólo se cambia por otro del mismo comportamiento: una bodega operativa no se vuelve de tránsito ni al revés.");

    public static Error WarehouseHasStock(int products, decimal quantity) => new ErrorConDatos("Inventory.Warehouse.HasStock",
        $"La bodega tiene existencia ({products} producto(s), {quantity} unidades base): trasládela o ajústela antes de inactivarla.",
        new { products, quantity });

    public static Error WarehouseTransitHasStock(int products, decimal quantity) => new ErrorConDatos("Inventory.Warehouse.TransitHasStock",
        $"La bodega de tránsito tiene mercancía en camino ({products} producto(s), {quantity} unidades base): reciba o resuelva los traslados antes.",
        new { products, quantity });

    /// <summary>Aviso: sin municipio, las compras de la bodega no tendrán municipio propuesto para la ReteICA.</summary>
    public static Error BranchMunicipalityMissing(Guid branchPublicId) => new ErrorConDatos(MunicipalityMissingCode,
        "La sucursal no tiene municipio (código DANE): las compras de esta bodega no tendrán municipio propuesto para la ReteICA. Regístrelo en Maestros › Agencias.",
        new { branchPublicId });

    // ---------------------------------------------------------------------------- ubicaciones --

    public static Error LocationTransitHasOnlyDefault() => new("Inventory.Location.TransitHasOnlyDefault",
        "La bodega de tránsito sólo tiene su ubicación por defecto.");

    public static Error LocationIsDefault() => new("Inventory.Location.IsDefault",
        "Es la ubicación por defecto de la bodega: marque otra por defecto antes de inactivarla o desmarcarla.");

    public static Error LocationHasStock(int products, decimal quantity) => new ErrorConDatos("Inventory.Location.HasStock",
        $"La ubicación tiene existencia ({products} producto(s), {quantity} unidades base): muévala antes de inactivarla.",
        new { products, quantity });

    // --------------------------------------------------------------------------------- reorden --

    public static Error ReorderPolicyInvalid(decimal minimum, decimal reorderPoint, decimal maximum) => new ErrorConDatos(
        "Inventory.ReorderPolicy.Invalid",
        $"Debe cumplirse 0 ≤ mínimo ≤ punto de reorden ≤ máximo (llegó {minimum} / {reorderPoint} / {maximum}).",
        new { minimum, reorderPoint, maximum });

    public static Error ReorderPolicyTransitNotAllowed() => new("Inventory.ReorderPolicy.TransitNotAllowed",
        "La bodega de tránsito no tiene mínimos ni máximos: se reponen las bodegas operativas.");
}
