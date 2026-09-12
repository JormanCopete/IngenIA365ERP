using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

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
    public int Code { get; set; }

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string ShortName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string TaxId { get; set; } = string.Empty;

    public int CheckDigit { get; set; }
}
