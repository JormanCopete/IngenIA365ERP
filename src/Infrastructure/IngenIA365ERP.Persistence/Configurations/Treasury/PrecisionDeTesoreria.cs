using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Treasury;

/// <summary>
/// La precisión de cada clase de número de la Tesorería nueva (feature 015, T0037; R-33; data-model §0.4): los mismos alias que
/// <see cref="Receipts.PrecisionDeRecaudo"/>, con su nombre de módulo para que las configuraciones de Tesorería no dependan de las de Recaudo.
/// Lo vigila <c>LasCantidadesYMontosDeCajaTienenSuPrecision</c>. (nuevo)
/// </summary>
public static class PrecisionDeTesoreria
{
    /// <summary>(18,2): todo valor en pesos.</summary>
    public static PropertyBuilder<decimal> Monto(this PropertyBuilder<decimal> propiedad) => propiedad.HasPrecision(18, 2);

    /// <inheritdoc cref="Monto(PropertyBuilder{decimal})"/>
    public static PropertyBuilder<decimal?> Monto(this PropertyBuilder<decimal?> propiedad) => propiedad.HasPrecision(18, 2);

    /// <summary>(9,6): tarifas y fracciones como fracción (GMF).</summary>
    public static PropertyBuilder<decimal> Tarifa(this PropertyBuilder<decimal> propiedad) => propiedad.HasPrecision(9, 6);

    /// <inheritdoc cref="Tarifa(PropertyBuilder{decimal})"/>
    public static PropertyBuilder<decimal?> Tarifa(this PropertyBuilder<decimal?> propiedad) => propiedad.HasPrecision(9, 6);

    /// <summary>(18,2): el valor facial de una denominación.</summary>
    public static PropertyBuilder<decimal> Facial(this PropertyBuilder<decimal> propiedad) => propiedad.HasPrecision(18, 2);

    /// <inheritdoc cref="Facial(PropertyBuilder{decimal})"/>
    public static PropertyBuilder<decimal?> Facial(this PropertyBuilder<decimal?> propiedad) => propiedad.HasPrecision(18, 2);
}
