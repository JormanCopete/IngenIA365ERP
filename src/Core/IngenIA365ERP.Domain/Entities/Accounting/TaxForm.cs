using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>Formulario tributario (350, 300, ICA…) cuyos renglones se resumen desde las cuentas (feature 009, FR-066). Sin semilla: lo configura el contador.</summary>
public class TaxForm : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<TaxFormLine> Lines { get; set; } = [];
}

/// <summary>Un renglón del formulario: qué cuentas (por prefijo) y qué se suma (base o valor), con signo.</summary>
public class TaxFormLine : AuditableEntity
{
    public int FormId { get; set; }
    public TaxForm? Form { get; set; }
    public string LineCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TaxFormSelector Selector { get; set; }

    /// <summary>Prefijos de cuenta separados por coma (p. ej. «236505,236510»).</summary>
    public string AccountPrefixes { get; set; } = string.Empty;

    public short Sign { get; set; } = 1;
    public int Order { get; set; }
}
