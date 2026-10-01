using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Domain.Taxes;

/// <summary>
/// La foto del catálogo tributario a una fecha (feature 012, T22, T162; data-model §17): las definiciones, las tarifas
/// vigentes con sus condiciones, los conceptos de retención, la UVT y los parámetros <c>TAX</c> que el motor necesita.
/// La arma un solo lector, <c>LectorDeCatalogoTributario</c>; <see cref="MotorTributario"/> no hace IO. Ningún valor
/// legal está en el código: todo sale de aquí.
/// </summary>
/// <param name="Fecha">La fecha de la foto.</param>
/// <param name="Uvt">La UVT vigente a la fecha (<c>LectorDeUvt</c>).</param>
/// <param name="RedondeoUvt"><c>Tributario.RedondeoUvtAPesos</c>.</param>
/// <param name="DecimalesDeMonto">Decimales de los montos en pesos (<c>Redondeo.Montos</c>: 2 al centavo, 0 al peso).</param>
/// <param name="Cooperativa">El perfil de la cooperativa (parámetros <c>TAX</c>).</param>
public sealed record TaxCatalogSnapshot(
    DateOnly Fecha,
    decimal Uvt,
    RedondeoUvt RedondeoUvt,
    int DecimalesDeMonto,
    PerfilTributario Cooperativa,
    IReadOnlyList<ImpuestoEnFoto> Impuestos,
    IReadOnlyList<TarifaEnFoto> Tarifas,
    IReadOnlyList<ConceptoEnFoto> Conceptos)
{
    public ImpuestoEnFoto? Impuesto(int id) => Impuestos.FirstOrDefault(i => i.Id == id);

    public ConceptoEnFoto? Concepto(int id) => Conceptos.FirstOrDefault(c => c.Id == id);
}

/// <summary>Una definición de impuesto o retención en la foto (<c>COR_TaxDefinitions</c>). (nuevo)</summary>
public sealed record ImpuestoEnFoto(
    int Id,
    string Code,
    string Name,
    TaxKind Kind,
    TaxCalculationForm CalculationForm,
    int? TaxedOnDefinitionId,
    bool IsWithholding,
    string? DianTaxCode,
    bool IsActive = true);

/// <summary>Un concepto de retención en la foto (<c>COR_WithholdingConcepts</c>). (nuevo)</summary>
public sealed record ConceptoEnFoto(int Id, string Code, string Name);

/// <summary>Una tarifa (una vigencia de un código) en la foto (<c>COR_TaxRates</c>). (nuevo)</summary>
public sealed record TarifaEnFoto(
    int Id,
    int TaxDefinitionId,
    string Code,
    string Name,
    decimal? Rate,
    decimal? AmountPerUnit,
    int? WithholdingConceptId,
    string? MunicipalityDaneCode,
    string? ActivityCode,
    decimal? MinimumBaseUvt,
    decimal? MinimumBasePesos,
    CondicionesDeTarifa Condiciones,
    TaxAppliesTo AppliesTo,
    int Priority,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string LegalSource)
{
    public bool VigenteEn(DateOnly fecha) => ValidFrom <= fecha && (ValidTo is null || ValidTo >= fecha);

    public bool AplicaA(TaxAppliesTo perspectiva) => AppliesTo == TaxAppliesTo.Both || AppliesTo == perspectiva;
}

/// <summary>
/// Las condiciones tipadas de una tarifa (data-model §17): sobre quien soporta el impuesto o la retención (el sujeto) y
/// sobre quien retiene (el agente). Nula = no importa. Sólo las tarifas de retención las usan. (nuevo)
/// </summary>
public sealed record CondicionesDeTarifa(
    string? SubjectPersonType = null,
    bool? SubjectIsIncomeTaxFiler = null,
    bool? SubjectIsVatResponsible = null,
    bool? SubjectIsLargeContributor = null,
    bool? SubjectIsSelfWithholder = null,
    bool? SubjectIsSimpleTaxRegime = null,
    bool? AgentIsLargeContributor = null,
    bool? AgentIsVatWithholdingAgent = null)
{
    public static CondicionesDeTarifa Ninguna { get; } = new();

    /// <summary>Cuántas condiciones no nulas tiene: a igual prioridad, gana la más específica.</summary>
    public int Cantidad => Lista().Count;

    public bool SeCumplen(PerfilTributario sujeto, PerfilTributario agente) =>
        (SubjectPersonType is null || string.Equals(SubjectPersonType, sujeto.PersonType, StringComparison.Ordinal))
        && (SubjectIsIncomeTaxFiler is null || SubjectIsIncomeTaxFiler == sujeto.IsIncomeTaxFiler)
        && (SubjectIsVatResponsible is null || SubjectIsVatResponsible == sujeto.IsVatResponsible)
        && (SubjectIsLargeContributor is null || SubjectIsLargeContributor == sujeto.IsLargeContributor)
        && (SubjectIsSelfWithholder is null || SubjectIsSelfWithholder == sujeto.IsSelfWithholder)
        && (SubjectIsSimpleTaxRegime is null || SubjectIsSimpleTaxRegime == sujeto.IsSimpleTaxRegime)
        && (AgentIsLargeContributor is null || AgentIsLargeContributor == agente.IsLargeContributor)
        && (AgentIsVatWithholdingAgent is null || AgentIsVatWithholdingAgent == agente.IsVatWithholdingAgent);

    /// <summary>Las condiciones no nulas, en palabras («sujeto declarante: sí»).</summary>
    public IReadOnlyList<string> Lista()
    {
        var lista = new List<string>();
        if (SubjectPersonType is { } tipo) lista.Add($"sujeto persona {(tipo == "01" ? "natural" : tipo == "02" ? "jurídica" : tipo)}");
        Agregar(lista, "sujeto declarante", SubjectIsIncomeTaxFiler);
        Agregar(lista, "sujeto responsable de IVA", SubjectIsVatResponsible);
        Agregar(lista, "sujeto gran contribuyente", SubjectIsLargeContributor);
        Agregar(lista, "sujeto autorretenedor", SubjectIsSelfWithholder);
        Agregar(lista, "sujeto del régimen simple", SubjectIsSimpleTaxRegime);
        Agregar(lista, "agente gran contribuyente", AgentIsLargeContributor);
        Agregar(lista, "agente retenedor de IVA", AgentIsVatWithholdingAgent);
        return lista;
    }

    private static void Agregar(List<string> lista, string nombre, bool? valor)
    {
        if (valor is { } v) lista.Add($"{nombre}: {(v ? "sí" : "no")}");
    }
}
