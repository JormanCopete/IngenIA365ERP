using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Catalog;

/// <summary>
/// El número de una marca dentro de una categoría en la codificación automática de productos (<c>INV_ProductCodeSubgroups</c>;
/// decisión del dueño del 2026-11-12). El código de un producto es <c>categoría + subgrupo + consecutivo</c>
/// (<c>01 01 013</c>: ACEITES, CASTROL, el 13); el subgrupo depende de la pareja, porque la numeración empieza de nuevo en cada
/// categoría (CASTROL es 01 en ACEITES y VARTA es 01 en BATERÍAS). La fila la crea <c>CodificacionDeProductos</c> la primera vez
/// que se codifica un producto de la pareja y no cambia después: un número asignado no se reutiliza. (nuevo)
/// </summary>
public class ProductCodeSubgroup : AuditableEntity
{
    public int CategoryId { get; set; }

    public ProductCategory? Category { get; set; }

    public int BrandId { get; set; }

    public Brand? Brand { get; set; }

    /// <summary>El número de la marca en la categoría, con sus ceros a la izquierda (<c>01</c>).</summary>
    public string Segment { get; set; } = string.Empty;
}
