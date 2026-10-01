using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Domain.Entities.Core.Taxes;

/// <summary>
/// <c>COR_TaxRates</c> (feature 012, T22, T161; data-model §17): una tarifa con vigencia, sus condiciones y su norma.
/// Una vigencia nueva es una fila nueva con el mismo <see cref="Code"/> (el patrón de <c>PAY_LegalParameters</c>): el
/// código es la dimensión <c>TaxRateCode</c> de la matriz contable (T27) y no cambia. <b>Una tarifa no se edita desde
/// que entra en vigencia</b> (<c>Core.TaxRate.InEffect</c>): se cierra su <see cref="ValidTo"/> y se crea otra, así la
/// foto de cada documento sigue valiendo sin que Core lea los documentos.
/// </summary>
public class TaxRate : AuditableEntity
{
    public int TaxDefinitionId { get; set; }

    public TaxDefinition? TaxDefinition { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Fracción (0,19; 0,00966 para 9,66 por mil). Obligatoria salvo en <c>AmountPerUnit</c>.</summary>
    public decimal? Rate { get; set; }

    /// <summary>Valor por unidad gravable (impuesto a las bolsas).</summary>
    public decimal? AmountPerUnit { get; set; }

    /// <summary>Obligatorio en <c>ReteFuente</c>; opcional en <c>ReteIca</c>.</summary>
    public int? WithholdingConceptId { get; set; }

    public WithholdingConcept? WithholdingConcept { get; set; }

    /// <summary>DIVIPOLA; obligatorio en <c>Ica</c> y <c>ReteIca</c>.</summary>
    public string? MunicipalityDaneCode { get; set; }

    /// <summary>CIIU, o <c>*</c> = tarifa general del municipio.</summary>
    public string? ActivityCode { get; set; }

    /// <summary>Base mínima en UVT; excluyente con <see cref="MinimumBasePesos"/>.</summary>
    public decimal? MinimumBaseUvt { get; set; }

    /// <summary>Base mínima en pesos, sólo donde el municipio la fija así.</summary>
    public decimal? MinimumBasePesos { get; set; }

    /// <summary><c>01</c> natural, <c>02</c> jurídica; nula = cualquiera.</summary>
    public string? SubjectPersonType { get; set; }

    public bool? SubjectIsIncomeTaxFiler { get; set; }
    public bool? SubjectIsVatResponsible { get; set; }
    public bool? SubjectIsLargeContributor { get; set; }
    public bool? SubjectIsSelfWithholder { get; set; }
    public bool? SubjectIsSimpleTaxRegime { get; set; }
    public bool? AgentIsLargeContributor { get; set; }
    public bool? AgentIsVatWithholdingAgent { get; set; }

    public TaxAppliesTo AppliesTo { get; set; } = TaxAppliesTo.Both;

    /// <summary>Desempata candidatas: gana la mayor.</summary>
    public short Priority { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    /// <summary>La norma que la respalda (FR-013).</summary>
    public string LegalSource { get; set; } = string.Empty;

    /// <summary>«Pendiente de validar por la contadora» (A8): la semilla la deja en verdadero.</summary>
    public bool ReviewPending { get; set; }

    public string? Notes { get; set; }

    public bool VigenteEn(DateOnly fecha) => ValidFrom <= fecha && (ValidTo is null || ValidTo >= fecha);

    /// <summary>Dos vigencias se cruzan si comparten al menos un día.</summary>
    public bool SeCruzaCon(DateOnly desde, DateOnly? hasta) =>
        ValidFrom <= (hasta ?? DateOnly.MaxValue) && desde <= (ValidTo ?? DateOnly.MaxValue);
}
