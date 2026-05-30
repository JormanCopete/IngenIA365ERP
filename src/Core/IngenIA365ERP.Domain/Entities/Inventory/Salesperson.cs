using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;

namespace IngenIA365ERP.Domain.Entities.Inventory;

/// <summary>
/// Maps to [dbo].[INV_Salespeople] (inv_Vendedor). Hija de <see cref="Person"/>.
///
/// <para>
/// Contiene SOLO datos del rol "vendedor": tipo de vendedor y si aplica comision.
/// El nombre, documento, direccion y contacto vienen de <see cref="Person"/>
/// via <see cref="PersonId"/>. La marca <c>Person.IsSalesperson</c> debe estar
/// en true cuando exista esta fila (decision P6).
/// </para>
/// </summary>
public class Salesperson : AuditableEntity
{
    public int PersonId { get; set; }

    public int? SalespersonType { get; set; }

    public bool AppliesCommission { get; set; }

    // === Navigation ===

    public Person Person { get; set; } = null!;
}
