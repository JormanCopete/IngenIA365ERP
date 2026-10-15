namespace IngenIA365ERP.Domain.Lending.Payroll;

/// <summary>De dónde salió el tope efectivo de descuento: el más estricto de los tres.</summary>
public enum OrigenDelTopeDeDescuento
{
    /// <summary>El límite legal replicado (<c>LIBRANZA_CAPACIDAD_DESCUENTO</c>, Ley 1527 de 2012).</summary>
    LimiteLegal = 1,

    /// <summary>El parámetro de la cooperativa (<c>Libranza.CapacidadDeDescuento</c>).</summary>
    ParametroDeLaCooperativa = 2,

    /// <summary>El tope propio de la versión del convenio (<c>DeductionCapFraction</c>).</summary>
    Convenio = 3,
}

/// <summary>Los tres topes (fracciones) que pueden regir la capacidad de descuento; sólo el legal es obligatorio.</summary>
public sealed record TopesDeDescuento(decimal LimiteLegal, decimal? ParametroDeLaCooperativa = null, decimal? Convenio = null);

/// <summary>
/// El veredicto de capacidad (FR-110): el tope que rige y de dónde salió, lo permitido sobre la base certificada, lo que ya está comprometido,
/// la cuota nueva y lo disponible para ella.
/// </summary>
public sealed record ResultadoDeCapacidadDeDescuento(
    decimal Base,
    decimal TopeEfectivo,
    OrigenDelTopeDeDescuento OrigenDelTope,
    decimal Permitido,
    decimal DescuentosVigentes,
    decimal CuotaNueva)
{
    /// <summary>Lo que cabe para la cuota nueva: lo permitido menos lo ya comprometido, nunca negativo.</summary>
    public decimal Disponible => Math.Max(Permitido - DescuentosVigentes, 0m);

    /// <summary>Los descuentos vigentes más la cuota nueva caben en lo permitido (y hay base).</summary>
    public bool Alcanza => Base > 0m && DescuentosVigentes + CuotaNueva <= Permitido;
}

/// <summary>
/// La capacidad de descuento por libranza (feature 013, K2, T0825; FR-110, D13; Ley 1527 de 2012): el total de descuentos —los vigentes más la
/// cuota nueva— no supera el tope sobre el <b>salario o la pensión después de los descuentos de ley</b> que certificó la pagaduría. El tope
/// efectivo es el más estricto entre el límite legal (<c>LIBRANZA_CAPACIDAD_DESCUENTO</c>), el parámetro de la cooperativa
/// (<c>Libranza.CapacidadDeDescuento</c>) y el tope del convenio (<c>DeductionCapFraction</c>); a igual valor manda el legal, luego el de la
/// cooperativa. Lo permitido se redondea al centavo. Sin valores legales: los tres llegan por parámetro. Pura. (nuevo)
/// </summary>
public static class CapacidadDeDescuento
{
    /// <param name="baseCertificada">Salario o pensión después de los descuentos de ley, según la certificación de la pagaduría.</param>
    /// <param name="topes">Las tres fracciones; las nulas no rigen.</param>
    /// <param name="descuentosVigentes">Lo que ya descuenta esa pagaduría (libranzas y demás descuentos certificados).</param>
    /// <param name="cuotaNueva">La cuota del crédito que se radica.</param>
    public static ResultadoDeCapacidadDeDescuento Evaluar(decimal baseCertificada, TopesDeDescuento topes, decimal descuentosVigentes, decimal cuotaNueva)
    {
        ArgumentNullException.ThrowIfNull(topes);
        if (baseCertificada < 0m) throw new ArgumentOutOfRangeException(nameof(baseCertificada), baseCertificada, "La base certificada no es negativa.");
        if (descuentosVigentes < 0m) throw new ArgumentOutOfRangeException(nameof(descuentosVigentes), descuentosVigentes, "Los descuentos vigentes no son negativos.");
        if (cuotaNueva < 0m) throw new ArgumentOutOfRangeException(nameof(cuotaNueva), cuotaNueva, "La cuota nueva no es negativa.");
        var (tope, origen) = TopeEfectivo(topes);
        var permitido = decimal.Round(baseCertificada * tope, 2, MidpointRounding.AwayFromZero);
        return new ResultadoDeCapacidadDeDescuento(baseCertificada, tope, origen, permitido, descuentosVigentes, cuotaNueva);
    }

    /// <summary>El más estricto (el menor) de los topes que rigen y de dónde salió.</summary>
    public static (decimal Tope, OrigenDelTopeDeDescuento Origen) TopeEfectivo(TopesDeDescuento topes)
    {
        ArgumentNullException.ThrowIfNull(topes);
        Validar(topes.LimiteLegal, nameof(topes.LimiteLegal));
        var tope = topes.LimiteLegal;
        var origen = OrigenDelTopeDeDescuento.LimiteLegal;
        if (topes.ParametroDeLaCooperativa is { } p)
        {
            Validar(p, nameof(topes.ParametroDeLaCooperativa));
            if (p < tope) (tope, origen) = (p, OrigenDelTopeDeDescuento.ParametroDeLaCooperativa);
        }
        if (topes.Convenio is { } c)
        {
            Validar(c, nameof(topes.Convenio));
            if (c < tope) (tope, origen) = (c, OrigenDelTopeDeDescuento.Convenio);
        }
        return (tope, origen);
    }

    /// <summary>
    /// ¿El tope propio de un convenio respeta el límite legal? Nunca por encima (T42): un tope más alto es
    /// <c>Lending.Parameter.BelowLegalFloor</c> al guardar la versión. Nulo (sin tope propio) siempre lo respeta.
    /// </summary>
    public static bool RespetaElLimite(decimal? topeDelConvenio, decimal limiteLegal) => topeDelConvenio is not { } t || (t > 0m && t <= limiteLegal);

    private static void Validar(decimal fraccion, string nombre)
    {
        if (fraccion <= 0m || fraccion > 1m)
            throw new ArgumentOutOfRangeException(nombre, fraccion, "Un tope de descuento es una fracción mayor que cero y hasta uno.");
    }
}
