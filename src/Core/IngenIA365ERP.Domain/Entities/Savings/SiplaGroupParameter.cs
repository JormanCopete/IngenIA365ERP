using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Savings;

/// <summary>Maps to [dbo].[LND_SiplaGroupParameters].</summary>
public class SiplaGroupParameter : AuditableEntity
{
    [MaxLength(3)]
    public string ConceptCode { get; set; } = string.Empty;
    [MaxLength(5)]
    public string CompanyCode { get; set; } = string.Empty;

    /// <summary>
    /// Código de la línea de crédito del sistema anterior, como dato: la llave hacia la tabla de líneas heredada se
    /// soltó al retirar la cartera heredada (feature 013, data-model parte 3 §13.2; §7.6 de las transversales).
    /// </summary>
    public int CreditLineId { get; set; }
}
