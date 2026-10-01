using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;

namespace IngenIA365ERP.Domain.Entities.Inventory.Security;

/// <summary>
/// Una bodega asignada a un usuario (<c>INV_UserWarehouseScopes</c>; feature 012, T204; T35; data-model §21): el alcance
/// por bodega sale de estas filas vivas (falla cerrado: sin filas, ninguna bodega, salvo
/// <c>Inventory.Scope.AllWarehouses</c>). A lo sumo una por defecto por usuario. La plataforma la lee y la escribe sólo
/// por <c>IAsignacionesDeBodega</c>, cuya implementación real es <c>AsignacionesDeBodegaEnBase</c>.
/// </summary>
public class UserWarehouseScope : AuditableEntity
{
    /// <summary><c>SEC_Users.Id</c>.</summary>
    public int UserId { get; set; }

    public int WarehouseId { get; set; }

    public Warehouse? Warehouse { get; set; }

    public bool IsDefault { get; set; }
}
