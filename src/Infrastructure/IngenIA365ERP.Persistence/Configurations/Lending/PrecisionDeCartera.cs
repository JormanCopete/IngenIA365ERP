using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IngenIA365ERP.Persistence.Configurations.Lending;

/// <summary>
/// La precisión de cada clase de número de Cartera (feature 013, T35, T0205; data-model §0.4). La convención global (18,2) no
/// cambia y en PostgreSQL <c>numeric(18,2)</c> redondea en silencio una tasa diaria o un puntaje: por eso cada propiedad decimal de
/// <c>Entities/Lending</c> declara la suya con uno de estos alias, y ninguna configuración de <c>Configurations/Lending</c> escribe
/// la precisión a mano. Lo vigila <c>LosSaldosYTasasDeCarteraTienenSuPrecision</c>. Molde: <c>PrecisionDeInventario</c>. (nuevo)
/// </summary>
public static class PrecisionDeCartera
{
    /// <summary>(18,2): pesos del libro, del plan, de las posiciones, de las operaciones, de los cupos, garantías, órdenes y archivos.</summary>
    public static PropertyBuilder<decimal> Monto(this PropertyBuilder<decimal> propiedad) => propiedad.HasPrecision(18, 2);

    /// <inheritdoc cref="Monto(PropertyBuilder{decimal})"/>
    public static PropertyBuilder<decimal?> Monto(this PropertyBuilder<decimal?> propiedad) => propiedad.HasPrecision(18, 2);

    /// <summary>
    /// (9,6): toda tasa <b>como fracción</b> (0,265000 = 26,5 % EA): pactada, usura, índice, margen, piso, techo, mora fija, factor
    /// relativo de mora, tarifa de seguro. Una tasa con más de 6 decimales se rechaza, nunca se redondea (<c>Lending.Rate.TooPrecise</c>).
    /// </summary>
    public static PropertyBuilder<decimal> TasaAnual(this PropertyBuilder<decimal> propiedad) => propiedad.HasPrecision(9, 6);

    /// <inheritdoc cref="TasaAnual(PropertyBuilder{decimal})"/>
    public static PropertyBuilder<decimal?> TasaAnual(this PropertyBuilder<decimal?> propiedad) => propiedad.HasPrecision(9, 6);

    /// <summary>(28,18): tasas derivadas selladas o explicadas: diaria, del período, sobretasa de cargos (<c>ChargesSpread</c>).</summary>
    public static PropertyBuilder<decimal> TasaEquivalente(this PropertyBuilder<decimal> propiedad) => propiedad.HasPrecision(28, 18);

    /// <inheritdoc cref="TasaEquivalente(PropertyBuilder{decimal})"/>
    public static PropertyBuilder<decimal?> TasaEquivalente(this PropertyBuilder<decimal?> propiedad) => propiedad.HasPrecision(28, 18);

    /// <summary>
    /// (9,6): proporciones que no son tasas: cobertura mínima, veces los aportes, pesos del puntaje, umbral de coincidencia de
    /// nombre, retención de prima, porcentajes normativos.
    /// </summary>
    public static PropertyBuilder<decimal> Fraccion(this PropertyBuilder<decimal> propiedad) => propiedad.HasPrecision(9, 6);

    /// <inheritdoc cref="Fraccion(PropertyBuilder{decimal})"/>
    public static PropertyBuilder<decimal?> Fraccion(this PropertyBuilder<decimal?> propiedad) => propiedad.HasPrecision(9, 6);

    /// <summary>(12,4): puntajes propios y de centrales (escalas de 0 a 1.000 con decimales).</summary>
    public static PropertyBuilder<decimal> Puntaje(this PropertyBuilder<decimal> propiedad) => propiedad.HasPrecision(12, 4);

    /// <inheritdoc cref="Puntaje(PropertyBuilder{decimal})"/>
    public static PropertyBuilder<decimal?> Puntaje(this PropertyBuilder<decimal?> propiedad) => propiedad.HasPrecision(12, 4);

    /// <summary>
    /// (18,4): valores en UVR y el valor de la UVR. K6b (<c>CarteraVivienda</c>): la cuota en UVR (<c>LND_RepaymentInstallments</c>), la UVR del plan,
    /// el valor en UVR de una fila del libro y su UVR, y la valoración de la posición diaria.
    /// </summary>
    public static PropertyBuilder<decimal> ValorUvr(this PropertyBuilder<decimal> propiedad) => propiedad.HasPrecision(18, 4);

    /// <inheritdoc cref="ValorUvr(PropertyBuilder{decimal})"/>
    public static PropertyBuilder<decimal?> ValorUvr(this PropertyBuilder<decimal?> propiedad) => propiedad.HasPrecision(18, 4);
}
