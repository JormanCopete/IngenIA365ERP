using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>
/// Plantilla de plan de cuentas hasta nivel 4 (feature 009, FR-001, FR-002): PUC Solidario y
/// PUC Comercial transcritos de la norma, o uno propio importado por la cooperativa. Se
/// siembra en cada base de cooperativa (Principio IV); la empresa lo copia al iniciar la
/// contabilidad y no lo edita.
/// </summary>
public class AccountCatalog : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public CatalogSource Source { get; set; }
    public DateTime? ImportedAt { get; set; }
    public string? ImportedBy { get; set; }

    /// <summary>Validación del contador antes de producción (FR-001).</summary>
    public DateTime? ValidatedAt { get; set; }
    public string? ValidatedBy { get; set; }

    public int EntryCount { get; set; }

    public ICollection<AccountCatalogEntry> Entries { get; set; } = [];
}
