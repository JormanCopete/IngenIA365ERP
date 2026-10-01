using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Inventory.Documents;

/// <summary>
/// La copia fiscal de la contraparte al confirmar (<c>INV_DocumentPartySnapshots</c>; feature 012, T52; FR-011;
/// data-model §5.6): con qué nombre, documento, dirección y perfil tributario se emitió o registró el documento, aunque
/// la persona cambie después. Hecho inmutable (sólo inserción); la corrección del caso a de FR-066 agrega la versión
/// siguiente con motivo. Vigente = la de mayor <see cref="Version"/>.
/// </summary>
public class DocumentPartySnapshot : AuditableEntity, IHechoInmutable
{
    public int DocumentId { get; init; }

    public int Version { get; init; } = 1;

    public int PersonId { get; init; }

    /// <summary>Persona natural o jurídica (código DIAN).</summary>
    public string DianOrganizationType { get; init; } = string.Empty;

    /// <summary>Tipo de identificación DIAN, derivado por <c>CatalogoDian</c>.</summary>
    public string DianIdTypeCode { get; init; } = string.Empty;

    public string TaxId { get; init; } = string.Empty;

    public string? CheckDigit { get; init; }

    public string LegalName { get; init; } = string.Empty;

    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public string? Address { get; init; }

    public string? MunicipalityDaneCode { get; init; }

    public string? CityName { get; init; }

    public string? DepartmentName { get; init; }

    public string? CountryCode { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    /// <summary><c>O-13;O-15…</c>, derivadas de las marcas (T24).</summary>
    public string? DianResponsibilities { get; init; }

    /// <summary>Tributo (<c>01</c>, <c>ZZ</c>).</summary>
    public string? DianTaxSchemeCode { get; init; }

    public bool IsVatResponsible { get; init; }
    public bool IsLargeContributor { get; init; }
    public bool IsSelfWithholder { get; init; }
    public bool IsVatWithholdingAgent { get; init; }
    public bool IsSimpleTaxRegime { get; init; }
    public bool IsIncomeTaxFiler { get; init; }
    public bool WithholdingExempt { get; init; }
    public bool IcaWithholdingExempt { get; init; }

    public string? CiiuCode { get; init; }

    /// <summary>Obligatorio desde la versión 2.</summary>
    public string? ChangeReason { get; init; }
}
