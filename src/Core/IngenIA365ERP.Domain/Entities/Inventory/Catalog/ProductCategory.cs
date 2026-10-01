using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Catalog;

/// <summary>
/// Categoría jerárquica de productos (<c>INV_ProductCategories</c>; feature 012, T199; FR-024; data-model §1.2): hasta
/// <see cref="MaxLevel"/> niveles. <see cref="Path"/> <b>(nuevo)</b> es la ruta materializada de Ids (<c>/3/17/42/</c>)
/// que usan el conteo y los informes por categoría para incluir las subcategorías; se reescribe en los descendientes al
/// mover una rama.
/// </summary>
public class ProductCategory : AuditableEntity
{
    public const int MaxLevel = 5;

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Nulo = raíz.</summary>
    public int? ParentId { get; set; }

    public ProductCategory? Parent { get; set; }

    /// <summary>1..5; la raíz es 1 y cada hija, uno más que su padre.</summary>
    public byte Level { get; set; } = 1;

    public string Path { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>La ruta de una hija de <paramref name="rutaDelPadre"/> (vacía en la raíz) con el Id <paramref name="id"/>.</summary>
    public static string RutaDe(string? rutaDelPadre, int id) => (string.IsNullOrEmpty(rutaDelPadre) ? "/" : rutaDelPadre) + id + "/";
}
