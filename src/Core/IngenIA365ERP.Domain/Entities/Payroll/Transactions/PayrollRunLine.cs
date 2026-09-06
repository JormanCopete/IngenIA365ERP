using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll.Transactions;

/// <summary>
/// Maps to [dbo].[PAY_PayrollRunLines]. Una línea de la liquidación de un empleado y
/// la unidad de la explicación (FR-013): concepto (versión), cantidad, base, factor o
/// tramo, parámetro legal y vigencia, novedad de origen, valor. Inmutable: nunca se
/// actualiza ni se borra (Principio XI, <c>PrincipioXI_ContableImmutable</c>).
/// </summary>
public class PayrollRunLine : AuditableEntity
{
    public int PayrollRunEmployeeId { get; set; }

    /// <summary>Versión de la definición con la que se calculó.</summary>
    public int ConceptDefinitionId { get; set; }

    [MaxLength(30)]
    public string ConceptCode { get; set; } = string.Empty;

    [MaxLength(120)]
    public string ConceptName { get; set; } = string.Empty;

    public ConceptNature Nature { get; set; }

    public decimal? Quantity { get; set; }
    public decimal? BaseAmount { get; set; }
    public decimal? Factor { get; set; }
    public decimal? RangeFrom { get; set; }
    public decimal? RangeTo { get; set; }
    public decimal Amount { get; set; }

    /// <summary>Versión del parámetro legal usado, si la forma lo usa.</summary>
    public int? LegalParameterId { get; set; }

    public int? NoveltyId { get; set; }

    /// <summary>Explicación estructurada (forma, base, factor, parámetro, tramo, novedad, pasos).</summary>
    public string ExplanationJson { get; set; } = "{}";

    /// <summary>Falso para informativos y para los descuentos de Cartera, que ya contabilizó Cartera.</summary>
    public bool AffectsAccounting { get; set; } = true;

    public int Order { get; set; }

    public PayrollRunEmployee? RunEmployee { get; set; }
}
