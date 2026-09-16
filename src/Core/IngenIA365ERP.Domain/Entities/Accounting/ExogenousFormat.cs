using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Accounting;

namespace IngenIA365ERP.Domain.Entities.Accounting;

/// <summary>
/// Formato de información exógena de un año gravable (feature 009, FR-071): código y versión
/// DIAN, si aplica a la cooperativa, cuantía mínima y regla de menores cuantías. Sembrado con
/// todos los de la resolución vigente; los que no aplican se marcan y no generan.
/// </summary>
public class ExogenousFormat : AuditableEntity
{
    public int TaxYear { get; set; }
    public string FormatCode { get; set; } = string.Empty;
    public int FormatVersion { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Applies { get; set; }
    public ExogenousOrigin Origin { get; set; }
    public decimal MinAmount { get; set; }
    public bool MinorAmountsRule { get; set; }
    public string MinorAmountsTaxId { get; set; } = string.Empty;

    public ICollection<ExogenousConcept> Concepts { get; set; } = [];
}

/// <summary>Concepto de un formato (p. ej. 5001 salarios) y las cuentas que alimentan cada columna de valor.</summary>
public class ExogenousConcept : AuditableEntity
{
    public int FormatId { get; set; }
    public ExogenousFormat? Format { get; set; }
    public string ConceptCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public ICollection<ExogenousConceptAccount> Accounts { get; set; } = [];
}

/// <summary>Qué cuentas (por prefijo) y qué se suma (débito, crédito, neto o base) en una columna de valor del concepto.</summary>
public class ExogenousConceptAccount : AuditableEntity
{
    public int ConceptId { get; set; }
    public ExogenousConcept? Concept { get; set; }

    /// <summary>Nombre de la columna del formato: PagoDeducible, RetencionPracticada, IvaDescontable…</summary>
    public string ValueField { get; set; } = string.Empty;

    public string AccountPrefix { get; set; } = string.Empty;

    /// <summary>Debit, Credit, Net o Base.</summary>
    public string Selector { get; set; } = "Net";
}
