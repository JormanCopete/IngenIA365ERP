using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Application.Core.Taxes;

/// <summary>
/// La plantilla 1 de la parametrización (feature 012, T166; contracts/plantillas.md §1): hojas <c>Conceptos</c>,
/// <c>Impuestos</c> y <c>Tarifas</c> con sus columnas. La misma definición arma el libro vacío o lleno, su hoja
/// «Instrucciones» y es lo que exige <c>ImportTaxCatalogCommand</c>; <c>CatalogoDePlantillas</c> la publica con la
/// clave <c>core.taxes</c>. (nuevo)
/// </summary>
public static class PlantillaDeImpuestos
{
    public const string Clave = "core.taxes";

    public const string HojaConceptos = "Conceptos";
    public const string HojaImpuestos = "Impuestos";
    public const string HojaTarifas = "Tarifas";

    // Columnas comunes.
    public const string Codigo = "codigo";
    public const string Nombre = "nombre";
    public const string Activo = "activo";
    public const string Notas = "notas";

    // Impuestos.
    public const string Clase = "clase";
    public const string FormaDeCalculo = "formaDeCalculo";
    public const string CalculadoSobre = "calculadoSobre";
    public const string EsRetencion = "esRetencion";
    public const string CodigoDian = "codigoDian";

    // Tarifas.
    public const string Impuesto = "impuesto";
    public const string TarifaPorcentaje = "tarifaPorcentaje";
    public const string ValorPorUnidad = "valorPorUnidad";
    public const string ConceptoRetencion = "conceptoRetencion";
    public const string Municipio = "municipio";
    public const string Actividad = "actividad";
    public const string BaseMinimaUvt = "baseMinimaUvt";
    public const string BaseMinimaPesos = "baseMinimaPesos";
    public const string AplicaA = "aplicaA";
    public const string Prioridad = "prioridad";
    public const string SujetoTipoPersona = "sujetoTipoPersona";
    public const string SujetoDeclarante = "sujetoDeclarante";
    public const string SujetoResponsableIva = "sujetoResponsableIva";
    public const string SujetoGranContribuyente = "sujetoGranContribuyente";
    public const string SujetoAutorretenedor = "sujetoAutorretenedor";
    public const string SujetoRegimenSimple = "sujetoRegimenSimple";
    public const string AgenteGranContribuyente = "agenteGranContribuyente";
    public const string AgenteRetenedorIva = "agenteRetenedorIva";
    public const string VigenteDesde = "vigenteDesde";
    public const string VigenteHasta = "vigenteHasta";
    public const string Norma = "norma";

    /// <summary>Las etiquetas en español que la columna <c>formaDeCalculo</c> admite además del nombre del enum.</summary>
    public static readonly IReadOnlyDictionary<string, TaxCalculationForm> EtiquetasDeForma = new Dictionary<string, TaxCalculationForm>
    {
        ["porcentaje sobre la base"] = TaxCalculationForm.PercentOfBase,
        ["sobre otro impuesto"] = TaxCalculationForm.PercentOfTax,
        ["valor por unidad"] = TaxCalculationForm.AmountPerUnit,
    };

    /// <summary>Las etiquetas en español de <c>aplicaA</c>.</summary>
    public static readonly IReadOnlyDictionary<string, TaxAppliesTo> EtiquetasDeAplicaA = new Dictionary<string, TaxAppliesTo>
    {
        ["compras"] = TaxAppliesTo.Purchases,
        ["ventas"] = TaxAppliesTo.Sales,
        ["ambas"] = TaxAppliesTo.Both,
    };

    /// <summary>Las etiquetas en español de <c>clase</c>.</summary>
    public static readonly IReadOnlyDictionary<string, TaxKind> EtiquetasDeClase = new Dictionary<string, TaxKind>
    {
        ["otro"] = TaxKind.Other,
    };

    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Impuestos, retenciones y conceptos", ModuloDeAuditoria.Taxes,
    [
        new HojaDePlantilla(HojaConceptos,
        [
            new(Codigo, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Reglas: "llave; nomenclatura de la cooperativa", Ejemplo: "COMPRAS"),
            new(Nombre, TipoDeValor.Texto, Obligatoria: true, Largo: ReglasDelCatalogoTributario.LargoDeNombre, Ejemplo: "Compras generales"),
            new(Activo, TipoDeValor.SiNo, Reglas: "vacío = sí; un concepto que usan tarifas vigentes no se inactiva", Ejemplo: "sí"),
        ]),
        new HojaDePlantilla(HojaImpuestos,
        [
            new(Codigo, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Reglas: "llave", Ejemplo: "IVA"),
            new(Nombre, TipoDeValor.Texto, Obligatoria: true, Largo: ReglasDelCatalogoTributario.LargoDeNombre, Ejemplo: "IVA"),
            new(Clase, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "Iva, Inc, ReteFuente, ReteIva, ReteIca, Ica, Other; no cambia", Ejemplo: "Iva"),
            new(FormaDeCalculo, TipoDeValor.Enumeracion, Obligatoria: true,
                Reglas: "PercentOfBase (porcentaje sobre la base), PercentOfTax (sobre otro impuesto), AmountPerUnit (valor por unidad); no cambia",
                Ejemplo: "PercentOfBase"),
            new(CalculadoSobre, TipoDeValor.Codigo, Largo: 10, Reglas: "con PercentOfTax: el código del impuesto base (el IVA para ReteIVA)", Ejemplo: "IVA"),
            new(EsRetencion, TipoDeValor.SiNo, Reglas: "sólo con Other; en las demás clases se deriva", Ejemplo: "no"),
            new(CodigoDian, TipoDeValor.Texto, Obligatoria: true, Largo: 4, Reglas: "tributo DIAN: 01 IVA, 04 INC, 22 bolsas, 05 ReteIVA, 06 ReteFuente, 07 ReteICA, ZZ otros", Ejemplo: "01"),
            new(Activo, TipoDeValor.SiNo, Reglas: "vacío = sí", Ejemplo: "sí"),
        ]),
        new HojaDePlantilla(HojaTarifas,
        [
            new(Codigo, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Reglas: "llave con vigenteDesde; estable entre vigencias", Ejemplo: "IVA19"),
            new(Impuesto, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Reglas: "código de la hoja Impuestos o existente", Ejemplo: "IVA"),
            new(Nombre, TipoDeValor.Texto, Obligatoria: true, Largo: ReglasDelCatalogoTributario.LargoDeNombre, Ejemplo: "IVA 19 %"),
            new(TarifaPorcentaje, TipoDeValor.Porcentaje, Reglas: "en puntos (19 = 19 %; 0,966 = 9,66 por mil); con PercentOfBase o PercentOfTax", Ejemplo: "19"),
            new(ValorPorUnidad, TipoDeValor.Monto, Reglas: "con AmountPerUnit; excluye a tarifaPorcentaje", Ejemplo: "66"),
            new(ConceptoRetencion, TipoDeValor.Codigo, Largo: 10, Reglas: "código de la hoja Conceptos; obligatorio en ReteFuente, opcional en ReteIca", Ejemplo: "COMPRAS"),
            new(Municipio, TipoDeValor.Texto, Largo: 5, Reglas: "código DANE (DIVIPOLA); obligatorio en Ica y ReteIca", Ejemplo: "76001 (Cali)"),
            new(Actividad, TipoDeValor.Texto, Largo: 6, Reglas: "CIIU o * (tarifa general del municipio); sólo Ica y ReteIca; exige municipio", Ejemplo: "*"),
            new(BaseMinimaUvt, TipoDeValor.Cantidad, Reglas: "≥ 0; excluye a baseMinimaPesos; procede con base igual o superior", Ejemplo: "27"),
            new(BaseMinimaPesos, TipoDeValor.Monto, Reglas: "sólo donde el municipio la fija en pesos", Ejemplo: ""),
            new(AplicaA, TipoDeValor.Enumeracion, Obligatoria: true, Reglas: "Purchases (compras), Sales (ventas), Both (ambas)", Ejemplo: "Both"),
            new(Prioridad, TipoDeValor.Entero, Reglas: "≥ 0; vacío = 0; desempata tarifas que coinciden, gana la mayor", Ejemplo: "0"),
            new(SujetoTipoPersona, TipoDeValor.Texto, Largo: 8, Reglas: "Natural, Juridica o vacío; sólo retenciones", Ejemplo: ""),
            new(SujetoDeclarante, TipoDeValor.SiNoIndiferente, Reglas: "vacío = no importa; sólo retenciones", Ejemplo: "sí"),
            new(SujetoResponsableIva, TipoDeValor.SiNoIndiferente, Reglas: "vacío = no importa; sólo retenciones"),
            new(SujetoGranContribuyente, TipoDeValor.SiNoIndiferente, Reglas: "vacío = no importa; sólo retenciones"),
            new(SujetoAutorretenedor, TipoDeValor.SiNoIndiferente, Reglas: "vacío = no importa; sólo retenciones"),
            new(SujetoRegimenSimple, TipoDeValor.SiNoIndiferente, Reglas: "vacío = no importa; sólo retenciones"),
            new(AgenteGranContribuyente, TipoDeValor.SiNoIndiferente, Reglas: "vacío = no importa; sólo retenciones"),
            new(AgenteRetenedorIva, TipoDeValor.SiNoIndiferente, Reglas: "vacío = no importa; sólo retenciones"),
            new(VigenteDesde, TipoDeValor.Fecha, Obligatoria: true, Reglas: "una vigencia nueva es otra fila del mismo código", Ejemplo: "AAAA-MM-01"),
            new(VigenteHasta, TipoDeValor.Fecha, Reglas: "≥ vigenteDesde; cerrar una vigencia exige motivo"),
            new(Norma, TipoDeValor.Texto, Obligatoria: true, Largo: ReglasDelCatalogoTributario.LargoDeNorma, Reglas: "la norma que respalda la tarifa", Ejemplo: "ET art. 468"),
            new(Notas, TipoDeValor.Texto, Largo: ReglasDelCatalogoTributario.LargoDeNotas),
        ]),
    ]);

    /// <summary><c>01</c>/<c>02</c> desde lo que escribe la persona (Natural, Juridica, 01, 02); nulo si no se reconoce.</summary>
    public static string? TipoDePersona(string? texto) => TablaLeida.Normalizar(texto) switch
    {
        "natural" or "01" or "1" => "01",
        "juridica" or "02" or "2" => "02",
        _ => null,
    };

    /// <summary>Lo que se escribe en la plantilla con datos para un tipo de persona.</summary>
    public static string? EtiquetaDeTipoDePersona(string? codigo) => codigo switch
    {
        "01" => "Natural",
        "02" => "Juridica",
        _ => null,
    };
}
