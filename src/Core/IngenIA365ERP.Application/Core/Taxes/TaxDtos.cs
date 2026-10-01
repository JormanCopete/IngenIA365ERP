using IngenIA365ERP.Domain.Entities.Core.Taxes;
using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Application.Core.Taxes;

/// <summary>
/// Una definición de impuesto o retención (contracts/api.md §30). <see cref="Rates"/> sólo viene en el detalle
/// (<c>GET /taxes/{id}</c>: todas las vigencias).
/// </summary>
public sealed record TaxDefinitionDto(
    Guid TaxPublicId,
    string Code,
    string Name,
    TaxKind Kind,
    TaxCalculationForm CalculationForm,
    Guid? TaxedOnTaxPublicId,
    bool IsWithholding,
    string? DianTaxCode,
    bool IsActive,
    string? Notes)
{
    public IReadOnlyList<TaxRateDto>? Rates { get; init; }
}

/// <summary>Las condiciones tipadas de una tarifa (contracts/api.md §30; nulo = no importa). (nuevo)</summary>
public sealed record TaxRateConditionsDto(
    string? SubjectPersonType = null,
    bool? SubjectIsIncomeTaxFiler = null,
    bool? SubjectIsVatResponsible = null,
    bool? SubjectIsLargeContributor = null,
    bool? SubjectIsSelfWithholder = null,
    bool? SubjectIsSimpleTaxRegime = null,
    bool? AgentIsLargeContributor = null,
    bool? AgentIsVatWithholdingAgent = null)
{
    public static TaxRateConditionsDto De(TaxRate t) => new(t.SubjectPersonType, t.SubjectIsIncomeTaxFiler, t.SubjectIsVatResponsible,
        t.SubjectIsLargeContributor, t.SubjectIsSelfWithholder, t.SubjectIsSimpleTaxRegime, t.AgentIsLargeContributor, t.AgentIsVatWithholdingAgent);

    public bool Vacias => SubjectPersonType is null && SubjectIsIncomeTaxFiler is null && SubjectIsVatResponsible is null
        && SubjectIsLargeContributor is null && SubjectIsSelfWithholder is null && SubjectIsSimpleTaxRegime is null
        && AgentIsLargeContributor is null && AgentIsVatWithholdingAgent is null;
}

/// <summary>
/// Una tarifa (una vigencia de un código; contracts/api.md §30). <see cref="Rate"/> es fracción. <see cref="OtherVersions"/>
/// sólo viene en el detalle (<c>GET /tax-rates/{id}</c>: las otras vigencias del mismo código).
/// </summary>
public sealed record TaxRateDto(
    Guid TaxRatePublicId,
    Guid TaxPublicId,
    string Code,
    string Name,
    decimal? Rate,
    decimal? AmountPerUnit,
    Guid? WithholdingConceptPublicId,
    string? MunicipalityDaneCode,
    string? ActivityCode,
    decimal? MinimumBaseUvt,
    decimal? MinimumBasePesos,
    TaxRateConditionsDto Conditions,
    TaxAppliesTo AppliesTo,
    short Priority,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string LegalSource,
    bool ReviewPending,
    string? Notes)
{
    public IReadOnlyList<TaxRateDto>? OtherVersions { get; init; }
}

/// <summary>Un concepto de retención (contracts/api.md §30). (nuevo)</summary>
public sealed record WithholdingConceptDto(Guid WithholdingConceptPublicId, string Code, string Name, bool IsActive, string? Notes);

/// <summary>Arma los DTO desde las entidades (con sus navegaciones cargadas).</summary>
internal static class TaxMapper
{
    public static TaxDefinitionDto Definicion(TaxDefinition d) => new(d.PublicId, d.Code, d.Name, d.Kind, d.CalculationForm,
        d.TaxedOnDefinition?.PublicId, d.IsWithholding, d.DianTaxCode, d.IsActive, d.Notes);

    public static TaxRateDto Tarifa(TaxRate t) => new(t.PublicId, t.TaxDefinition?.PublicId ?? Guid.Empty, t.Code, t.Name, t.Rate, t.AmountPerUnit,
        t.WithholdingConcept?.PublicId, t.MunicipalityDaneCode, t.ActivityCode, t.MinimumBaseUvt, t.MinimumBasePesos,
        TaxRateConditionsDto.De(t), t.AppliesTo, t.Priority, t.ValidFrom, t.ValidTo, t.LegalSource, t.ReviewPending, t.Notes);

    public static WithholdingConceptDto Concepto(WithholdingConcept c) => new(c.PublicId, c.Code, c.Name, c.IsActive, c.Notes);
}
