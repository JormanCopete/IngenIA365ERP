using System.Globalization;
using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Domain.Taxes;

/// <summary>
/// Lo que recibe <see cref="MotorTributario"/> (feature 012, T162; research R21): la fecha y la perspectiva (en compras
/// retiene la cooperativa; en ventas, el comprador agente retenedor), el perfil de las dos partes, el municipio de la
/// operación, las líneas con su base neta de descuentos no condicionados y, en notas y devoluciones, la foto del
/// original. (nuevo)
/// </summary>
public sealed record EntradaTributaria
{
    public required DateOnly Fecha { get; init; }

    /// <summary><see cref="TaxAppliesTo.Purchases"/> o <see cref="TaxAppliesTo.Sales"/>.</summary>
    public required TaxAppliesTo Perspectiva { get; init; }

    /// <summary>Quien vende: el sujeto de las retenciones y quien cobra el impuesto.</summary>
    public required PerfilTributario Vendedor { get; init; }

    /// <summary>Quien compra: el agente de las retenciones.</summary>
    public required PerfilTributario Comprador { get; init; }

    /// <summary>Municipio DIVIPOLA de la operación (ReteICA). Nulo = no hay ReteICA.</summary>
    public string? MunicipioDane { get; init; }

    /// <summary>El tipo de compra está marcado <c>VatNonDeductible</c> (FR-044).</summary>
    public bool TipoIvaNoDescontable { get; init; }

    public required IReadOnlyList<LineaTributaria> Lineas { get; init; }

    /// <summary>
    /// Nota o devolución: los renglones del documento original. El motor aplica sus tarifas a la base nueva y no vuelve
    /// a probar la base mínima (pregunta E9).
    /// </summary>
    public IReadOnlyList<RenglonTributario>? Original { get; init; }
}

/// <summary>
/// Una línea del documento para el motor (nuevo). <see cref="Base"/> es la base neta de descuentos no condicionados;
/// <see cref="UnidadesBase"/> la cantidad en unidad base (impuestos por unidad). <see cref="LineaOriginal"/>: en una
/// nota, la línea del original que corrige.
/// </summary>
public sealed record LineaTributaria(
    int Numero,
    decimal Base,
    decimal UnidadesBase,
    VatSaleTreatment TratamientoIvaVenta,
    IReadOnlyList<ImpuestoDeLinea> Impuestos,
    int? ConceptoDeRetencionId = null,
    int? LineaOriginal = null);

/// <summary>
/// Un impuesto del producto de la línea (<c>INV_ProductTaxes</c>, nuevo): la definición, la tarifa por su código estable
/// (nulo = el motor la elige), a qué perspectiva aplica y, en los impuestos por unidad, cuántas unidades gravables trae
/// una unidad base.
/// </summary>
public sealed record ImpuestoDeLinea(
    int TaxDefinitionId,
    string? TaxRateCode,
    TaxAppliesTo AppliesTo = TaxAppliesTo.Both,
    decimal? TaxableUnitsPerBaseUnit = null);

/// <summary>
/// Un renglón que aplicó el motor (nuevo): lo que se guarda en <c>INV_DocumentTaxLines</c> al confirmar. <see cref="Linea"/>
/// nula = renglón del documento (retenciones).
/// </summary>
public sealed record RenglonTributario(
    int? Linea,
    int TaxDefinitionId,
    int TaxRateId,
    string TaxRateCode,
    TaxKind Kind,
    TaxTreatment Treatment,
    int? WithholdingConceptId,
    string? MunicipalityDaneCode,
    decimal? Rate,
    decimal? AmountPerUnit,
    decimal? TaxableUnits,
    decimal Base,
    decimal Amount,
    string? DianTaxCode,
    ExplicacionTributaria Explicacion)
{
    public bool EsRetencion => Treatment is TaxTreatment.WithholdingApplied or TaxTreatment.WithholdingSuffered;
}

/// <summary>Por qué el documento no se puede confirmar (nuevo): <c>Core.TaxRate.Ambiguous</c>, <c>Core.TaxRate.NotFound</c>…</summary>
public sealed record RechazoTributario(string Codigo, string Mensaje);

/// <summary>
/// Lo que devuelve el motor (nuevo): los renglones, los rechazos (el documento no se confirma si hay alguno) y las
/// omisiones (lo que se evaluó y no aplicó, con su razón, para la explicación del documento).
/// </summary>
public sealed record ResultadoTributario(
    IReadOnlyList<RenglonTributario> Renglones,
    IReadOnlyList<RechazoTributario> Rechazos,
    IReadOnlyList<string> Omisiones)
{
    public bool Rechazado => Rechazos.Count > 0;

    public decimal TotalDeImpuestos => Renglones.Where(r => !r.EsRetencion).Sum(r => r.Amount);

    public decimal TotalDeRetenciones => Renglones.Where(r => r.EsRetencion).Sum(r => r.Amount);
}

/// <summary>
/// La explicación de un renglón, paso a paso (nuevo; FR-013): UVT y fecha, base mínima en pesos, la tarifa elegida y la
/// condición que decidió. Se guarda como JSON en <c>INV_DocumentTaxLines.ExplanationJson</c>.
/// </summary>
public sealed class ExplicacionTributaria
{
    public string Resumen { get; set; } = string.Empty;

    public List<PasoTributario> Pasos { get; init; } = [];

    public ExplicacionTributaria Paso(string etiqueta, decimal valor)
    {
        Pasos.Add(new PasoTributario(etiqueta, valor, null));
        return this;
    }

    public ExplicacionTributaria Nota(string etiqueta, string texto)
    {
        Pasos.Add(new PasoTributario(etiqueta, null, texto));
        return this;
    }

    /// <summary>Todo en una línea, para leerlo y para las pruebas.</summary>
    public string Texto() =>
        Resumen + " | " + string.Join(" | ", Pasos.Select(p =>
            p.Valor is { } v ? $"{p.Etiqueta}: {v.ToString("0.######", CultureInfo.InvariantCulture)}" : $"{p.Etiqueta}: {p.Texto}"));
}

/// <summary>Un paso de la explicación (nuevo): etiqueta y valor o texto.</summary>
public sealed record PasoTributario(string Etiqueta, decimal? Valor, string? Texto);
