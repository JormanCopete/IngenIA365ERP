using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Core;

/// <summary>
/// Grupo del asociado (<c>COR_AssociateGroups</c>): la clasificación libre con la que la cooperativa agrupa a sus
/// asociados —zona, empresa, gremio, agencia…—. Es independiente de la pagaduría y de la contabilidad: no lleva
/// cuentas, centros de costo ni datos de nómina. El código es la nomenclatura de la cooperativa (alfanumérico,
/// mayúsculas, único entre vivos, 10 caracteres) y no cambia una vez creado.
/// </summary>
public class AssociateGroup : AuditableEntity
{
    [MaxLength(10)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(80)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}