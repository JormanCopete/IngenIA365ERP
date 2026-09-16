using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Una cuenta de un catálogo (niveles 1 a 4). El padre es el código del nivel anterior.</summary>
public class AccountCatalogEntry : AuditableEntity
{
    public int CatalogId { get; set; }
    public AccountCatalog? Catalog { get; set; }

    /// <summary>1, 2, 4 o 6 dígitos según el nivel.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public byte Level { get; set; }
    public AccountNature Nature { get; set; }

    /// <summary>Rubro de estado financiero NIIF (<see cref="FinancialStatementItem"/>) que heredan las auxiliares.</summary>
    public string NiifItemCode { get; set; } = string.Empty;

    public string? ParentCode { get; set; }
}
