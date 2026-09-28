using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Application.Inventory.Pos;

/// <summary>
/// Los errores de puntos de venta, cajas y disponibilidad de medios (feature 012, I3, T593–T595; contracts/api.md §20.1, §22.3).
/// Un punto inexistente o fuera del alcance es el mismo 404 (<c>Inventory.PointOfSale.NotFound</c>, <see cref="ErroresDeAlcance"/>).
/// <c>Inventory.CashRegister.{NotFound, RoleDuplicate, RoleRequired, TransitWarehouse}</c> e
/// <c>Inventory.PointOfSale.WarehouseBranchMismatch</c> son (nuevo). (nuevo)
/// </summary>
public static class ErroresDePuntoDeVenta
{
    public const string HasOpenSessionsCode = "Inventory.PointOfSale.HasOpenSessions";
    public const string PointWarehouseBranchMismatchCode = "Inventory.PointOfSale.WarehouseBranchMismatch";
    public const string CashRegisterNotFoundCode = "Inventory.CashRegister.NotFound";
    public const string RoleClassMismatchCode = "Inventory.CashRegister.RoleClassMismatch";
    public const string WarehouseBranchMismatchCode = "Inventory.CashRegister.WarehouseBranchMismatch";
    public const string RoleDuplicateCode = "Inventory.CashRegister.RoleDuplicate";
    public const string RoleRequiredCode = "Inventory.CashRegister.RoleRequired";
    public const string TransitWarehouseCode = "Inventory.CashRegister.TransitWarehouse";
    public const string AvailabilityConflictCode = "Inventory.PaymentMeans.AvailabilityConflict";

    public static Error PointOfSaleNotFound() => ErroresDeAlcance.PuntoInexistente();

    public static Error CashRegisterNotFound(string? codigo = null) => new(CashRegisterNotFoundCode,
        codigo is null ? "La caja no existe en este punto." : $"No hay una caja «{codigo}». Créela en la hoja Cajas o en Ventas › Puntos de venta.");

    /// <summary>Con una sesión abierta el punto no se desactiva (422).</summary>
    public static Error HasOpenSessions(int openSessions) => new ErrorConDatos(HasOpenSessionsCode,
        $"El punto tiene {openSessions} sesión(es) de caja abierta(s): ciérrelas antes de desactivarlo.", new { openSessions });

    public static Error PointWarehouseBranchMismatch(string warehouseCode) => new ErrorConDatos(PointWarehouseBranchMismatchCode,
        $"La bodega {warehouseCode} es de otra sucursal: la bodega por defecto del punto es de su misma sucursal.", new { warehouseCode });

    /// <summary>La clase del tipo no sirve para el rol (422, §20.1).</summary>
    public static Error RoleClassMismatch(CashRegisterDocumentRole role, string documentTypeCode, DocumentClass documentClass, IEnumerable<DocumentClass> allowed) =>
        new ErrorConDatos(RoleClassMismatchCode,
            $"El tipo {documentTypeCode} es de clase {documentClass}: el rol {role} admite {string.Join(" o ", allowed)}.",
            new { role, documentTypeCode, documentClass, allowed = allowed.ToList() });

    /// <summary>La bodega de la caja es de otra sucursal que el punto (422, §20.1).</summary>
    public static Error WarehouseBranchMismatch(string warehouseCode) => new ErrorConDatos(WarehouseBranchMismatchCode,
        $"La bodega {warehouseCode} es de otra sucursal: la bodega de la caja es de la sucursal de su punto.", new { warehouseCode });

    /// <summary>Un rol por caja (índice único <c>(CashRegisterId, Role)</c>).</summary>
    public static Error RoleDuplicate(CashRegisterDocumentRole role) => new ErrorConDatos(RoleDuplicateCode,
        $"El rol {role} viene dos veces: una caja tiene a lo sumo un tipo por rol.", new { role });

    /// <summary>Un rol que exige otro (la factura a petición exige su nota crédito, plantilla 10).</summary>
    public static Error RoleRequired(CashRegisterDocumentRole role, CashRegisterDocumentRole requiredBy) => new ErrorConDatos(RoleRequiredCode,
        $"Con el rol {requiredBy} la caja necesita también el tipo del rol {role}.", new { role, requiredBy });

    public static Error TransitWarehouse(string warehouseCode) => new ErrorConDatos(TransitWarehouseCode,
        $"La bodega {warehouseCode} es de tránsito: un punto o una caja vende desde una bodega operativa.", new { warehouseCode });

    /// <summary>Un conjunto explícito junto con la marca «todos» de la misma dimensión (422, §22.3).</summary>
    /// <param name="dimension"><c>pointsOfSale</c>, <c>salesChannels</c> o <c>documentTypes</c>.</param>
    public static Error AvailabilityConflict(string dimension) => new ErrorConDatos(AvailabilityConflictCode,
        "El medio se ofrece en «todos» en esa dimensión: quite la marca «todos» del medio o deje vacío el conjunto explícito.",
        new { dimension });

    /// <summary>Los roles y las clases que admiten, para el mensaje.</summary>
    public static IReadOnlyList<DocumentClass> ClasesDe(CashRegisterDocumentRole rol) => CashRegisterDocumentType.ClasesDelRol(rol).OrderBy(c => c).ToList();
}
