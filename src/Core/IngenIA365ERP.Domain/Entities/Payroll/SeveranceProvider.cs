using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>Maps to [dbo].[PAY_SeveranceProviders] (nom_cesantias).</summary>
public class SeveranceProvider : AuditableEntity
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
}
