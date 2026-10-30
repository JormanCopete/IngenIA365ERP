using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Receipts;

/// <summary>
/// La precisión de cada clase de número de Recaudo (feature 015, T0037; R-33; data-model §0.4). Cada propiedad decimal de
/// <c>Entities/Receipts</c> declara uno de estos alias y ninguna configuración escribe la precisión a mano
/// (<c>LasCantidadesYMontosDeCajaTienenSuPrecision</c>). La cantidad de billetes o monedas por denominación es <c>int</c>: no tiene alias.
/// Molde: <c>PrecisionDeCartera</c>. (nuevo)
/// </summary>
public static class PrecisionDeRecaudo
{
    /// <summary>(18,2): todo valor en pesos.</summary>
    public static PropertyBuilder<decimal> Monto(this PropertyBuilder<decimal> propiedad) => propiedad.HasPrecision(18, 2);

    /// <inheritdoc cref="Monto(PropertyBuilder{decimal})"/>
    public static PropertyBuilder<decimal?> Monto(this PropertyBuilder<decimal?> propiedad) => propiedad.HasPrecision(18, 2);

    /// <summary>(9,6): tarifas y fracciones como fracción (GMF, tolerancias porcentuales).</summary>
    public static PropertyBuilder<decimal> Tarifa(this PropertyBuilder<decimal> propiedad) => propiedad.HasPrecision(9, 6);

    /// <inheritdoc cref="Tarifa(PropertyBuilder{decimal})"/>
    public static PropertyBuilder<decimal?> Tarifa(this PropertyBuilder<decimal?> propiedad) => propiedad.HasPrecision(9, 6);

    /// <summary>(18,2): el valor facial de una denominación (catálogo de la 012).</summary>
    public static PropertyBuilder<decimal> Facial(this PropertyBuilder<decimal> propiedad) => propiedad.HasPrecision(18, 2);

    /// <inheritdoc cref="Facial(PropertyBuilder{decimal})"/>
    public static PropertyBuilder<decimal?> Facial(this PropertyBuilder<decimal?> propiedad) => propiedad.HasPrecision(18, 2);
}
