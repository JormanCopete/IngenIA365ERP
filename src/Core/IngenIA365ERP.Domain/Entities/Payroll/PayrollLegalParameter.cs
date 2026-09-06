using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_LegalParameters]. Una fila por vigencia (FR-010, FR-011): el
/// motor sólo conoce los códigos (<c>LegalParameterCodes</c>); los valores los carga la
/// semilla del año y los mantiene la cooperativa. Las tablas por rangos (retención en
/// UVT, fondo de solidaridad) viven en <see cref="Ranges"/>.
/// </summary>
public class PayrollLegalParameter : AuditableEntity
{
    [MaxLength(40)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public LegalParameterKind Kind { get; set; }

    /// <summary>Amount o Percent. Nulo para RangeTable.</summary>
    public decimal? Value { get; set; }

    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }

    /// <summary>Decreto, resolución o ley que fija el valor.</summary>
    [MaxLength(200)]
    public string? Source { get; set; }

    /// <summary>
    /// RangeTable: codigo del parametro que da la UNIDAD en que estan expresados los
    /// tramos (UVT para la retencion, SMMLV para el fondo de solidaridad). Nulo = los
    /// tramos estan en pesos. El motor convierte la base a esa unidad antes de buscar
    /// el tramo, y la explicacion lo muestra (29,92 UVT -> tramo 0-95).
    /// </summary>
    [MaxLength(40)]
    public string? RangeUnitParameterCode { get; set; }

    /// <summary>
    /// RangeTable: true = tarifa marginal sobre el exceso del tramo mas el fijo del
    /// tramo (retencion en la fuente, art. 383 E.T.); false = la tarifa del tramo se
    /// aplica a TODA la base (fondo de solidaridad pensional).
    /// </summary>
    public bool RangeIsMarginal { get; set; }

    public ICollection<PayrollLegalParameterRange> Ranges { get; set; } = [];

    public bool IsValidAt(DateTime date) =>
        ValidFrom <= date && (ValidTo is null || ValidTo >= date);
}
