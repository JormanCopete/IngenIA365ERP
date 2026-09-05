using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Core;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_ConceptDefinitionAccounts]. Cuentas contables de un concepto
/// (por código: aplican a todas sus versiones) y, opcionalmente, por centro de costo.
/// FK a <see cref="ChartOfAccount"/>, no códigos de texto: la aprobación exige una
/// fila para cada concepto liquidado (FR-019, FR-023).
/// </summary>
public class PayrollConceptDefinitionAccount : AuditableEntity
{
    [MaxLength(30)]
    public string ConceptCode { get; set; } = string.Empty;

    /// <summary>Nulo = cuentas por defecto del concepto.</summary>
    public int? CostCenterId { get; set; }

    public int DebitAccountId { get; set; }
    public int CreditAccountId { get; set; }

    public CostCenter? CostCenter { get; set; }
    public ChartOfAccount? DebitAccount { get; set; }
    public ChartOfAccount? CreditAccount { get; set; }
}
