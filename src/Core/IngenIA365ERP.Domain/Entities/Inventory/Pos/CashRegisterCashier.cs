using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Pos;

/// <summary>
/// <c>INV_CashRegisterCashiers</c> (2026-11-25, pedido del dueño): un usuario que puede abrir una caja del punto de venta, con su vigencia, como los
/// cajeros de una caja de Recaudo (<c>RCD_CashBoxCashiers</c>). Con <c>Caja.SoloCajerosAsignados</c> encendido, una caja con cajeros vigentes sólo la
/// abren ellos (y los de la caja de Recaudo a la que esté unida); sin ninguno, la abre cualquiera con acceso al punto. Retirar cierra la vigencia; las
/// vigencias del mismo usuario en la misma caja no se cruzan. (nuevo)
/// </summary>
public class CashRegisterCashier : AuditableEntity
{
    public int CashRegisterId { get; set; }

    /// <summary><c>SEC_Users.Id</c>.</summary>
    public int UserId { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public bool VigenteEn(DateOnly fecha) => ValidFrom <= fecha && (ValidTo is null || ValidTo >= fecha);
}
