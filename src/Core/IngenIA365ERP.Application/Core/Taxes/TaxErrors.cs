using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core.Taxes;

namespace IngenIA365ERP.Application.Core.Taxes;

/// <summary>
/// Los errores del catálogo tributario (feature 012, T164; contracts/api.md §30; decisiones-transversales §2.17):
/// <c>Core.Tax.NotFound</c>, <c>Core.TaxRate.{NotFound, Overlaps, InEffect, Ambiguous}</c>,
/// <c>Core.WithholdingConcept.{NotFound, InUse}</c>; más <c>Core.Tax.Immutable</c> (nuevo) para la plantilla, que no
/// puede cambiar la clase ni la forma de cálculo de un impuesto existente, y <c>Validation.Invalid</c> para las reglas
/// que dependen de la definición (400, como el validador).
/// </summary>
public static class TaxErrors
{
    public const string TaxNotFoundCode = "Core.Tax.NotFound";
    public const string TaxImmutableCode = "Core.Tax.Immutable";
    public const string RateNotFoundCode = "Core.TaxRate.NotFound";
    public const string OverlapsCode = "Core.TaxRate.Overlaps";
    public const string InEffectCode = "Core.TaxRate.InEffect";
    public const string AmbiguousCode = "Core.TaxRate.Ambiguous";
    public const string ConceptNotFoundCode = "Core.WithholdingConcept.NotFound";
    public const string ConceptInUseCode = "Core.WithholdingConcept.InUse";
    public const string InvalidCode = "Validation.Invalid";
    public const string MunicipalityUnknownCode = "Core.TaxRate.MunicipalityUnknown";

    public static Error TaxNotFound(string? codigo = null) => new(TaxNotFoundCode,
        codigo is null ? "El impuesto no existe." : $"No hay un impuesto «{codigo}». Créelo en Maestros › Impuestos o en la hoja Impuestos.");

    public static Error RateNotFound() => new(RateNotFoundCode, "La tarifa no existe.");

    public static Error ConceptNotFound(string? codigo = null) => new(ConceptNotFoundCode,
        codigo is null ? "El concepto de retención no existe." : $"No hay un concepto de retención «{codigo}». Créelo en la hoja Conceptos o en Maestros › Impuestos.");

    /// <summary>Dos vigencias del mismo código no se cruzan (422, <c>data { taxRatePublicId, validFrom, validTo }</c>).</summary>
    public static Error Overlaps(TaxRate otra) => new ErrorConDatos(OverlapsCode,
        $"La tarifa {otra.Code} ya tiene una vigencia desde {otra.ValidFrom:yyyy-MM-dd}{(otra.ValidTo is { } h ? $" hasta {h:yyyy-MM-dd}" : " sin fin")} que se cruza con ésta. Cierre esa vigencia antes.",
        new { taxRatePublicId = otra.PublicId, validFrom = otra.ValidFrom, validTo = otra.ValidTo });

    /// <summary>Una tarifa no se edita desde que entra en vigencia: se cierra y se crea otra (422).</summary>
    public static Error InEffect(TaxRate tarifa) => new(InEffectCode,
        $"La tarifa {tarifa.Code} rige desde {tarifa.ValidFrom:yyyy-MM-dd}: ya no se edita. Cierre su vigencia y cree otra con el mismo código.");

    /// <summary>Empate evidente con otra tarifa candidata (422), nombrando las dos.</summary>
    public static Error Ambiguous(string codigo, string otro) => new(AmbiguousCode,
        $"Las tarifas {codigo} y {otro} empatan: mismo impuesto, concepto, municipio, actividad, condiciones y prioridad con vigencias cruzadas. Cambie la prioridad o una condición.");

    public static Error ConceptInUse(IEnumerable<string> tarifas) => new(ConceptInUseCode,
        $"El concepto lo usan tarifas vigentes ({string.Join(", ", tarifas)}): no se inactiva mientras rijan.");

    public static Error Immutable(string codigo, string campo) => new(TaxImmutableCode,
        $"El impuesto {codigo} ya existe: su {campo} no cambia. Para otra, cree un impuesto con otro código.");

    public static Error Invalid(string mensaje) => new(InvalidCode, mensaje);

    /// <summary>El municipio de la tarifa no está en <c>COR_Cities.DaneCode</c> (422; feature 012, T176). (nuevo)</summary>
    public static Error MunicipalityUnknown(string codigo) => new(MunicipalityUnknownCode,
        $"El municipio {codigo} no está en las ciudades (código DIVIPOLA). Cárguelo en Maestros › Ciudades o revise el código.");
}
