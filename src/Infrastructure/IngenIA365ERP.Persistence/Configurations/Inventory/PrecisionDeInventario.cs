using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Inventory;

/// <summary>
/// La precisión de cada clase de número del comercio (feature 012, T19; data-model §0). La convención global (18,2) no
/// cambia y en PostgreSQL <c>numeric(18,2)</c> redondea en silencio un costo promedio o una tarifa de ICA por mil: por
/// eso cada propiedad decimal de <c>Entities/Inventory</c> y <c>Entities/Core/Taxes</c> declara la suya con uno de
/// estos alias. Lo vigila <c>LasCantidadesYCostosTienenSuPrecision</c>.
/// </summary>
public static class PrecisionDeInventario
{
    /// <summary>(18,4): cantidades de línea y en unidad base, existencias, reservas, capas, conteos, mínimos y máximos.</summary>
    public static PropertyBuilder<decimal> Cantidad(this PropertyBuilder<decimal> propiedad) => propiedad.HasPrecision(18, 4);

    /// <inheritdoc cref="Cantidad(PropertyBuilder{decimal})"/>
    public static PropertyBuilder<decimal?> Cantidad(this PropertyBuilder<decimal?> propiedad) => propiedad.HasPrecision(18, 4);

    /// <summary>(18,6): factor de conversión de unidades.</summary>
    public static PropertyBuilder<decimal> Factor(this PropertyBuilder<decimal> propiedad) => propiedad.HasPrecision(18, 6);

    /// <inheritdoc cref="Factor(PropertyBuilder{decimal})"/>
    public static PropertyBuilder<decimal?> Factor(this PropertyBuilder<decimal?> propiedad) => propiedad.HasPrecision(18, 6);

    /// <summary>(18,6): costo unitario del kardex, de la línea, del estado de costo y de las capas.</summary>
    public static PropertyBuilder<decimal> CostoUnitario(this PropertyBuilder<decimal> propiedad) => propiedad.HasPrecision(18, 6);

    /// <inheritdoc cref="CostoUnitario(PropertyBuilder{decimal})"/>
    public static PropertyBuilder<decimal?> CostoUnitario(this PropertyBuilder<decimal?> propiedad) => propiedad.HasPrecision(18, 6);

    /// <summary>(18,6): precio unitario de la línea de compra o venta.</summary>
    public static PropertyBuilder<decimal> PrecioUnitario(this PropertyBuilder<decimal> propiedad) => propiedad.HasPrecision(18, 6);

    /// <inheritdoc cref="PrecioUnitario(PropertyBuilder{decimal})"/>
    public static PropertyBuilder<decimal?> PrecioUnitario(this PropertyBuilder<decimal?> propiedad) => propiedad.HasPrecision(18, 6);

    /// <summary>(18,2): pesos (totales, costo total, valor del estado de costo, impuestos, valorizado).</summary>
    public static PropertyBuilder<decimal> Monto(this PropertyBuilder<decimal> propiedad) => propiedad.HasPrecision(18, 2);

    /// <inheritdoc cref="Monto(PropertyBuilder{decimal})"/>
    public static PropertyBuilder<decimal?> Monto(this PropertyBuilder<decimal?> propiedad) => propiedad.HasPrecision(18, 2);

    /// <summary>(9,6): toda tarifa, porcentaje o tope, como fracción (0,19; 0,00966).</summary>
    public static PropertyBuilder<decimal> Tarifa(this PropertyBuilder<decimal> propiedad) => propiedad.HasPrecision(9, 6);

    /// <inheritdoc cref="Tarifa(PropertyBuilder{decimal})"/>
    public static PropertyBuilder<decimal?> Tarifa(this PropertyBuilder<decimal?> propiedad) => propiedad.HasPrecision(9, 6);
}
