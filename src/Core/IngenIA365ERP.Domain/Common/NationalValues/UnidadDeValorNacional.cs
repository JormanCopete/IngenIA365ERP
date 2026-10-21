namespace IngenIA365ERP.Domain.Common.NationalValues;

/// <summary>
/// Unidad de un valor nacional (feature 013, T37, R47; data-model parte 3 §3.1, §12.3, §12.5). Se guarda como
/// <b>texto</b> (el nombre) en <c>ADM_NationalValues.Unit</c> y su copia; el número sólo ordena. Tasas y fracciones van
/// como fracción (0,2675 = 26,75 %). <see cref="Veces"/> la agregó data-model §12.5 a la lista de DT §2.21: un múltiplo
/// (del SMMLV, de la mora) o un conteo.
/// </summary>
public enum UnidadDeValorNacional
{
    /// <summary>Tasa efectiva anual, como fracción.</summary>
    EA = 1,

    /// <summary>Tasa nominal mes vencido, como fracción (IBR).</summary>
    NominalMV = 2,

    Pesos = 3,
    Fraccion = 4,
    Dias = 5,
    Meses = 6,
    Horas = 7,
    Veces = 8,
}
