namespace IngenIA365ERP.Shared.Services.Core;

// DTOs del catálogo tributario de Core tal como los sirve la API (feature 012, T170; contracts/api.md §30). `Shared` no
// referencia `Application`, así que se duplican aquí con los mismos nombres de propiedad; el JSON es el contrato. Los
// enums salen de la API como número: aquí son `int` con sus etiquetas en `CatalogoTributarioTextos`.

/// <summary>Un impuesto o retención (<c>TaxDefinitionDto</c>).</summary>
public sealed record ImpuestoDto
{
    public Guid TaxPublicId { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    /// <summary><c>TaxKind</c>: 1 IVA, 2 INC, 3 ReteFuente, 4 ReteIVA, 5 ReteICA, 6 ICA, 99 otro.</summary>
    public int Kind { get; init; }
    /// <summary><c>TaxCalculationForm</c>: 1 porcentaje sobre la base, 2 sobre otro impuesto, 3 valor por unidad.</summary>
    public int CalculationForm { get; init; }
    public Guid? TaxedOnTaxPublicId { get; init; }
    public bool IsWithholding { get; init; }
    public string? DianTaxCode { get; init; }
    public bool IsActive { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<TarifaTributariaDto>? Rates { get; init; }
}

/// <summary>Las condiciones de una tarifa (<c>TaxRateConditionsDto</c>); nulo = no importa.</summary>
public sealed record CondicionesDeTarifaDto
{
    public string? SubjectPersonType { get; init; }
    public bool? SubjectIsIncomeTaxFiler { get; init; }
    public bool? SubjectIsVatResponsible { get; init; }
    public bool? SubjectIsLargeContributor { get; init; }
    public bool? SubjectIsSelfWithholder { get; init; }
    public bool? SubjectIsSimpleTaxRegime { get; init; }
    public bool? AgentIsLargeContributor { get; init; }
    public bool? AgentIsVatWithholdingAgent { get; init; }
}

/// <summary>Una tarifa con vigencia (<c>TaxRateDto</c>). <see cref="Rate"/> es fracción (0,19).</summary>
public sealed record TarifaTributariaDto
{
    public Guid TaxRatePublicId { get; init; }
    public Guid TaxPublicId { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public decimal? Rate { get; init; }
    public decimal? AmountPerUnit { get; init; }
    public Guid? WithholdingConceptPublicId { get; init; }
    public string? MunicipalityDaneCode { get; init; }
    public string? ActivityCode { get; init; }
    public decimal? MinimumBaseUvt { get; init; }
    public decimal? MinimumBasePesos { get; init; }
    public CondicionesDeTarifaDto Conditions { get; init; } = new();
    /// <summary><c>TaxAppliesTo</c>: 1 compras, 2 ventas, 3 ambas.</summary>
    public int AppliesTo { get; init; }
    public short Priority { get; init; }
    public DateOnly ValidFrom { get; init; }
    public DateOnly? ValidTo { get; init; }
    public string LegalSource { get; init; } = "";
    public bool ReviewPending { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<TarifaTributariaDto>? OtherVersions { get; init; }

    /// <summary>¿Rige a la fecha?</summary>
    public bool VigenteEn(DateOnly fecha) => ValidFrom <= fecha && (ValidTo is null || ValidTo >= fecha);
}

/// <summary>Un concepto de retención (<c>WithholdingConceptDto</c>).</summary>
public sealed record ConceptoDeRetencionDto
{
    public Guid WithholdingConceptPublicId { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public bool IsActive { get; init; }
    public string? Notes { get; init; }
}

/// <summary>El alta de un impuesto (<c>POST /api/core/taxes</c>).</summary>
public sealed record CrearImpuestoRequest(
    string Code, string Name, int Kind, int CalculationForm, Guid? TaxedOnTaxPublicId, bool IsWithholding,
    string? DianTaxCode, string? Notes, string Reason);

/// <summary>La edición de un impuesto (<c>PUT /api/core/taxes/{id}</c>).</summary>
public sealed record EditarImpuestoRequest(string Name, string? DianTaxCode, bool IsActive, string? Notes, string Reason);

/// <summary>El alta o la corrección de una tarifa (<c>POST /api/core/tax-rates</c>, <c>PUT /{id}</c>).</summary>
public sealed record TarifaRequest(
    Guid TaxPublicId, string? Code, string Name, decimal? Rate, decimal? AmountPerUnit, Guid? WithholdingConceptPublicId,
    string? MunicipalityDaneCode, string? ActivityCode, decimal? MinimumBaseUvt, decimal? MinimumBasePesos,
    CondicionesDeTarifaDto? Conditions, int AppliesTo, short Priority, DateOnly ValidFrom, DateOnly? ValidTo,
    string LegalSource, string? Notes, string Reason);

/// <summary>Cerrar una vigencia (<c>POST /api/core/tax-rates/{id}/close</c>).</summary>
public sealed record CerrarTarifaRequest(DateOnly ValidTo, string Reason);

/// <summary>Un motivo (<c>POST /api/core/tax-rates/{id}/review</c>).</summary>
public sealed record MotivoTributarioRequest(string Reason);

/// <summary>El alta de un concepto (<c>POST /api/core/withholding-concepts</c>).</summary>
public sealed record CrearConceptoDeRetencionRequest(string Code, string Name, string? Notes, string Reason);

/// <summary>La edición de un concepto (<c>PUT /api/core/withholding-concepts/{id}</c>).</summary>
public sealed record EditarConceptoDeRetencionRequest(string Name, bool IsActive, string? Notes, string Reason);

/// <summary>Lo que responde un alta (el id nuevo).</summary>
public sealed record ImpuestoCreadoDto(Guid TaxPublicId);

public sealed record TarifaCreadaDto(Guid TaxRatePublicId);

public sealed record ConceptoCreadoDto(Guid WithholdingConceptPublicId);

/// <summary>Las etiquetas en español de los números que sirve la API.</summary>
public static class CatalogoTributarioTextos
{
    public static readonly IReadOnlyList<(int Valor, string Texto)> Clases =
        [(1, "IVA"), (2, "INC"), (3, "ReteFuente"), (4, "ReteIVA"), (5, "ReteICA"), (6, "ICA"), (99, "Otro")];

    public static readonly IReadOnlyList<(int Valor, string Texto)> Formas =
        [(1, "Porcentaje sobre la base"), (2, "Sobre otro impuesto"), (3, "Valor por unidad")];

    public static readonly IReadOnlyList<(int Valor, string Texto)> AplicaA =
        [(1, "Compras"), (2, "Ventas"), (3, "Compras y ventas")];

    public static string Clase(int valor) => Clases.FirstOrDefault(c => c.Valor == valor).Texto ?? valor.ToString();

    public static string Forma(int valor) => Formas.FirstOrDefault(c => c.Valor == valor).Texto ?? valor.ToString();

    public static string Aplica(int valor) => AplicaA.FirstOrDefault(c => c.Valor == valor).Texto ?? valor.ToString();

    /// <summary>La tarifa en puntos para mostrarla («19 %»; «0,966 %» para 9,66 por mil).</summary>
    public static string Tarifa(TarifaTributariaDto t) =>
        t.Rate is { } r ? $"{(r * 100).ToString("0.####", System.Globalization.CultureInfo.GetCultureInfo("es-CO"))} %"
        : t.AmountPerUnit is { } v ? $"{v.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("es-CO"))} por unidad"
        : "—";
}
