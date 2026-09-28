using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;

namespace IngenIA365ERP.Domain.Entities.Inventory.Security;

/// <summary>
/// Un punto de venta asignado a un usuario (<c>INV_UserPointOfSaleScopes</c>; feature 012, I3, T575; T35; data-model §21): el
/// alcance por punto sale de estas filas vivas (falla cerrado: sin filas, ningún punto, salvo
/// <c>Inventory.Scope.AllPointsOfSale</c>). A lo sumo una por defecto por usuario. La plataforma la lee y la escribe sólo por
/// <c>IAsignacionesDePuntoDeVenta</c>, cuya implementación real es <c>AsignacionesDePuntoDeVentaEnBase</c> (T596). Su tabla entra
/// en <c>VentasYPuntoDeVenta</c>.
/// </summary>
public class UserPointOfSaleScope : AuditableEntity
{
    /// <summary><c>SEC_Users.Id</c>.</summary>
    public int UserId { get; set; }

    public int PointOfSaleId { get; set; }

    public PointOfSale? PointOfSale { get; set; }

    public bool IsDefault { get; set; }
}
