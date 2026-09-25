using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Domain.Taxes;

/// <summary>
/// Si el impuesto de una compra es descontable o va al costo (feature 012, T22, T162; FR-044; research R21; NIC 2
/// párr. 11). El IVA, en el orden de FR-044:
/// <list type="number">
/// <item>la cooperativa no es responsable de IVA a la fecha (<c>Tributario.ResponsableIva</c>): al costo;</item>
/// <item>el tipo de compra está marcado <c>VatNonDeductible</c> (consumo interno, actividades excluidas): al costo;</item>
/// <item>el producto se vende excluido: al costo;</item>
/// <item>si nada de lo anterior, descontable.</item>
/// </list>
/// El INC y cualquier otro impuesto que no sea IVA van siempre al costo. El prorrateo del IVA en costos comunes (ET
/// art. 490) queda en Contabilidad.
/// </summary>
public static class IvaDescontable
{
    public static TaxTreatment Determinar(TaxKind kind, bool cooperativaResponsableIva, bool tipoIvaNoDescontable, VatSaleTreatment tratamientoDeVenta) =>
        Razon(kind, cooperativaResponsableIva, tipoIvaNoDescontable, tratamientoDeVenta).Tratamiento;

    /// <summary>La razón que decidió, en palabras, para la explicación del renglón.</summary>
    public static string Explicar(TaxKind kind, bool cooperativaResponsableIva, bool tipoIvaNoDescontable, VatSaleTreatment tratamientoDeVenta) =>
        Razon(kind, cooperativaResponsableIva, tipoIvaNoDescontable, tratamientoDeVenta).Texto;

    private static (TaxTreatment Tratamiento, string Texto) Razon(TaxKind kind, bool responsable, bool noDescontable, VatSaleTreatment tratamiento)
    {
        if (kind == TaxKind.Inc) return (TaxTreatment.AddedToCost, "El INC de una compra siempre va al costo.");
        if (kind != TaxKind.Iva) return (TaxTreatment.AddedToCost, "Un impuesto distinto del IVA en una compra va al costo.");
        if (!responsable) return (TaxTreatment.AddedToCost, "La cooperativa no es responsable de IVA a la fecha: el IVA va al costo.");
        if (noDescontable) return (TaxTreatment.AddedToCost, "El tipo de compra está marcado con IVA no descontable: el IVA va al costo.");
        if (tratamiento == VatSaleTreatment.Excluded) return (TaxTreatment.AddedToCost, "El producto se vende excluido: el IVA va al costo.");
        return (TaxTreatment.Deductible, "IVA descontable.");
    }
}
