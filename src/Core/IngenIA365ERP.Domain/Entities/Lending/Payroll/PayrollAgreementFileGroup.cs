using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Lending.Payroll;

/// <summary>
/// <c>LND_PayrollAgreementFileGroups</c> (feature 014, K7, T090; data-model §1.4.1; FR-125, E-05): un archivo de la pagaduría dentro de una versión
/// del convenio que usa grupos —«Créditos», «Aportes y ahorros»—, con su formato de ida y de vuelta (ámbitos de descuento de pagaduría, genéricos,
/// por referencia) y, si lo tiene, su calendario propio (nulo = el de la versión). Hija de la versión e inmutable con ella: cambiar los grupos es
/// registrar una versión nueva del convenio. No es hecho. (nuevo)
/// </summary>
public class PayrollAgreementFileGroup : AuditableEntity
{
    public int PayrollAgreementVersionId { get; set; }

    /// <summary>Nomenclatura de la cooperativa (<c>CodigoDeCatalogo</c>, 10); única dentro de la versión.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Formato de ida: ámbito <c>PayrollDeductionRequest</c>, genérico.</summary>
    public int OutboundFormatId { get; set; }

    /// <summary>Formato de vuelta: ámbito <c>PayrollDeductionResult</c>, genérico.</summary>
    public int ResultFormatId { get; set; }

    /// <summary>Día de corte propio; nulo = el de la versión.</summary>
    public byte? CutoffDay { get; set; }

    /// <summary>Día de pago propio; nulo = el de la versión.</summary>
    public byte? PaymentDay { get; set; }

    /// <summary>Antelación propia del archivo; nula = la de la versión.</summary>
    public int? SendLeadDays { get; set; }

    /// <summary>Orden de generación entre los grupos de la versión.</summary>
    public int Order { get; set; }
}

/// <summary>
/// <c>LND_PayrollAgreementConceptEquivalences</c> (feature 014, K7, T090; data-model §1.4.2; FR-125, E-05): un código o concepto de la pagaduría y
/// las líneas y conceptos de la Cartera que agrupa (<see cref="ItemsJson"/>, JSON canónico de <c>EquivalenciaDePagaduria</c>), con la regla con
/// que se reparte lo que la pagaduría informa descontado por ese código (<c>ConceptAllocationRule</c>) y su prelación frente a los demás códigos.
/// Hija de la versión del convenio. No es hecho. (nuevo)
/// </summary>
public class PayrollAgreementConceptEquivalence : AuditableEntity
{
    public int PayrollAgreementVersionId { get; set; }

    /// <summary>El grupo de archivo; obligatorio si la versión usa grupos.</summary>
    public int? FileGroupId { get; set; }

    /// <summary>El código tal como lo escribe la pagaduría; único por versión.</summary>
    public string PaymasterConceptCode { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Cómo se reparte lo descontado por este código entre sus ítems.</summary>
    public Enums.Lending.ConceptAllocationRule AllocationRule { get; set; } = Enums.Lending.ConceptAllocationRule.Priority;

    /// <summary>Prelación de este código frente a los demás del convenio cuando lo descontado no alcanza.</summary>
    public int Priority { get; set; }

    /// <summary>Los ítems (naturaleza, línea o «*», concepto y prioridad), en JSON canónico; al menos uno.</summary>
    public string ItemsJson { get; set; } = "[]";
}
