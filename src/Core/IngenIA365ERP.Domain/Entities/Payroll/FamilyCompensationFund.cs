using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Caja de compensación familiar a la que está afiliado cada empleado
/// (<c>PAY_FamilyCompensationFunds</c>; <see cref="Employee.FamilySubsidyId"/>). En SOLIDO la
/// caja era un entero suelto en la ficha sin tabla detrás; aquí es un catálogo como
/// EPS, pensión, ARL y cesantías. El aporte del 4 % (<c>CAJA</c>) se liquida igual sin
/// caja; el dato importa para la planilla PILA y los reportes por entidad.
/// </summary>
public class FamilyCompensationFund : AuditableEntity
{
    /// <summary>Código alfanumérico (hasta 10) que elige la cooperativa; único en la tabla. Numérico hasta el 2026-09-12.</summary>
    [MaxLength(10)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string ShortName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string TaxId { get; set; } = string.Empty;

    public int CheckDigit { get; set; }

    // Feature 009 (FR-088): persona del maestro que representa a la entidad como tercero contable.
    public int? PersonId { get; set; }
    public Person? Person { get; set; }
}
