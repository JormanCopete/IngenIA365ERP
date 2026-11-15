using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Pricing;

/// <summary>Qué le pasó a un precio de una lista. (nuevo)</summary>
public enum PriceChangeKind
{
    /// <summary>El producto entró a la lista en esa unidad.</summary>
    Created = 1,

    /// <summary>Cambió el valor.</summary>
    Updated = 2,

    /// <summary>Salió de la lista (o se retiró su unidad alterna).</summary>
    Removed = 3,

    /// <summary>Pasó a la nueva unidad base del producto (el precio acompaña a la unidad).</summary>
    UnitChanged = 4,
}

/// <summary>
/// La historia de un precio (<c>INV_PriceListItemHistory</c>; 2026-11-15, pedido del dueño): cada alta, cambio, retiro o paso de unidad
/// de un precio de una lista, con el valor anterior y el nuevo, el motivo, quién y cuándo (<c>CreatedBy</c>/<c>CreatedAt</c>). Sólo se
/// agrega: no se edita ni se borra. La escribe <c>HistoriaDePrecios</c>, el único sitio, en el mismo guardado que el cambio. La historia
/// empieza el día que se desplegó: lo anterior no se reconstruye. (nuevo)
/// </summary>
public class PriceListItemHistory : AuditableEntity
{
    public int PriceListId { get; set; }

    public PriceList? PriceList { get; set; }

    /// <summary><c>INV_Products</c>.</summary>
    public int ProductId { get; set; }

    /// <summary><c>INV_UnitsOfMeasure</c>: la unidad del precio después del cambio (la anterior, si se retiró).</summary>
    public int UnitId { get; set; }

    public PriceChangeKind Kind { get; set; }

    /// <summary>Nulo en un alta.</summary>
    public decimal? PreviousPrice { get; set; }

    /// <summary>Nulo en un retiro.</summary>
    public decimal? NewPrice { get; set; }

    /// <summary>En un paso de unidad, la unidad anterior.</summary>
    public int? PreviousUnitId { get; set; }

    public string Reason { get; set; } = string.Empty;
}
