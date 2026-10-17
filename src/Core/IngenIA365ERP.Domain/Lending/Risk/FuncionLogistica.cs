using IngenIA365ERP.Domain.Lending.Rates;

namespace IngenIA365ERP.Domain.Lending.Risk;

/// <summary>
/// La función logística del modelo de referencia de pérdida esperada (feature 013, K6a, T1028; Anexo 2, numeral 3; research R55, T26):
/// <c>puntaje = 1 / (1 + e^(−z))</c>, sólo con <c>decimal</c> (<c>ElMotorDeCarteraEsDecimal</c>): la exponencial es la de
/// <see cref="AritmeticaDecimal.Exponencial"/>, por serie con reducción del argumento. En coma flotante, la misma Z podía dar un puntaje
/// distinto entre plataformas y mover una calificación en el borde de un corte.
///
/// <para>Se calcula siempre sobre <c>|z|</c>, donde <c>e^(−|z|)</c> está en (0, 1] y no se desborda, y la mitad negativa sale de la
/// simetría <c>f(−z) = 1 − f(z)</c>, que así se cumple exacta. No redondea: el puntaje se redondea a la escala del documento
/// (<c>Anexo2.DecimalesDelPuntaje</c>) en el motor y en ningún otro paso (pregunta E9). Pura. (nuevo)</para>
/// </summary>
public static class FuncionLogistica
{
    /// <summary><c>1 / (1 + e^(−z))</c>, en (0, 1) mientras <c>e^|z|</c> quepa en <c>decimal</c>; más allá vale 0 o 1 a 28 decimales.</summary>
    public static decimal Evaluar(decimal z)
    {
        if (z < 0m) return 1m - Evaluar(-z);
        decimal t;
        try { t = 1m / AritmeticaDecimal.Exponencial(z); }
        catch (OverflowException) { t = 0m; }
        return 1m / (1m + t);
    }
}
