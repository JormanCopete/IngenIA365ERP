using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;

namespace IngenIA365ERP.Domain.Lending.Risk;

/// <summary>Las dimensiones del deterioro general que la matriz contable necesita (data-model §23.3).</summary>
public sealed record DimensionDeGeneral(int BranchId, string ModalityCode, string? SubsegmentCode, bool IsEmployee, ImpairmentModel Modelo);

/// <summary>Un crédito para el general: su dimensión, su cartera bruta (capital) y su deterioro individual (para la compensación).</summary>
public sealed record LineaParaGeneral(DimensionDeGeneral Dimension, decimal CarteraBruta, decimal DeterioroIndividual);

/// <summary>
/// Los porcentajes del general: el mínimo del perfil en la política efectiva, acotado por abajo por el límite legal
/// <c>DETERIORO_GENERAL_MINIMO</c> (dirección «Mayor»), el adicional de la política y si compensa el individual.
/// </summary>
public sealed record ParametrosDeDeterioroGeneral(decimal FraccionDelPerfil, decimal LimiteLegalMinimo, decimal FraccionAdicional, bool AdicionalCompensaIndividual)
{
    /// <summary>El mínimo que rige: el mayor entre el del perfil y el límite legal.</summary>
    public decimal FraccionMinima => Math.Max(FraccionDelPerfil, LimiteLegalMinimo);

    /// <summary>Desde la política efectiva y el perfil de la entidad; sin perfil en la norma, el general.</summary>
    public static ParametrosDeDeterioroGeneral Desde(ResultadoDePoliticaEfectiva efectiva, string? perfil, decimal limiteLegalMinimo)
    {
        ArgumentNullException.ThrowIfNull(efectiva);
        var delPerfil = efectiva.Efectiva.DeterioroGeneralDe(perfil)
            ?? throw new InvalidOperationException($"El Anexo 1 vigente no trae deterioro general para el perfil «{perfil}» ni el general.");
        return new(delPerfil.FraccionMinima, limiteLegalMinimo, efectiva.DeterioroGeneralAdicional, efectiva.AdicionalCompensaIndividual);
    }
}

/// <summary>El general de una dimensión (data-model §23.3).</summary>
public sealed record DeterioroGeneralPorDimension(
    DimensionDeGeneral Dimension,
    decimal CarteraBruta,
    decimal PorcentajeMinimo,
    decimal PorcentajeAdicional,
    decimal MontoMinimo,
    decimal MontoAdicional,
    decimal CompensacionDelIndividual,
    decimal Anterior,
    string Fuente)
{
    /// <summary>Lo que queda en el general: mínimo + adicional − lo que el adicional compensó del individual.</summary>
    public decimal Total => MontoMinimo + MontoAdicional - CompensacionDelIndividual;

    public decimal Delta => Total - Anterior;
}

/// <summary>
/// El deterioro general (feature 013, US9, T0694; T46, R55; FR-104; SC-023), por sucursal, modalidad, sub-segmento, empleado y
/// modelo:
/// <list type="bullet">
/// <item><b>mínimo</b> = cartera bruta × el mayor entre el porcentaje del perfil en la política efectiva y el límite legal
/// <c>DETERIORO_GENERAL_MINIMO</c>;</item>
/// <item><b>adicional</b> = cartera bruta × el adicional de la política (cero si no lo hay);</item>
/// <item><b>compensación</b>: si la política lo permite, el adicional cubre el individual de la misma dimensión hasta donde alcance
/// (es un traslado: el total del general baja en lo que compensa);</item>
/// <item>los modelos que no llevan general (parámetro por modelo y tipo de entidad) quedan fuera; una dimensión que tenía general en
/// el cierre anterior y ya no tiene cartera sale con cero, para que el delta la libere.</item>
/// </list>
/// Cada monto se redondea como el individual (<c>Riesgo.RedondeoDeterioro</c>). Pura: sin IO, sin valores legales. (nuevo)
/// </summary>
public static class MotorDeDeterioroGeneral
{
    public static IReadOnlyList<DeterioroGeneralPorDimension> Calcular(
        IEnumerable<LineaParaGeneral> lineas,
        ParametrosDeDeterioroGeneral parametros,
        IReadOnlyDictionary<DimensionDeGeneral, decimal> anteriores,
        IReadOnlySet<ImpairmentModel> modelosConGeneral,
        RedondeoDeDeterioro redondeo)
    {
        ArgumentNullException.ThrowIfNull(lineas);
        ArgumentNullException.ThrowIfNull(parametros);
        ArgumentNullException.ThrowIfNull(anteriores);
        ArgumentNullException.ThrowIfNull(modelosConGeneral);
        if (parametros.FraccionAdicional < 0m) throw new ArgumentOutOfRangeException(nameof(parametros), "El adicional no es negativo.");

        var minimo = parametros.FraccionMinima;
        var fuente = $"mínimo {ExplicacionDeCartera.Numero(minimo)} (perfil {ExplicacionDeCartera.Numero(parametros.FraccionDelPerfil)}, "
            + $"límite legal {ExplicacionDeCartera.Numero(parametros.LimiteLegalMinimo)}); adicional {ExplicacionDeCartera.Numero(parametros.FraccionAdicional)}"
            + (parametros.AdicionalCompensaIndividual ? ", compensa el individual" : string.Empty);

        var grupos = lineas.Where(l => modelosConGeneral.Contains(l.Dimension.Modelo))
            .GroupBy(l => l.Dimension)
            .ToDictionary(g => g.Key, g => (Bruta: g.Sum(l => l.CarteraBruta), Individual: g.Sum(l => l.DeterioroIndividual)));

        var resultado = new List<DeterioroGeneralPorDimension>();
        foreach (var (dimension, (bruta, individual)) in grupos)
        {
            var montoMinimo = MotorDeDeterioroIndividual.Redondear(bruta * minimo, redondeo);
            var montoAdicional = MotorDeDeterioroIndividual.Redondear(bruta * parametros.FraccionAdicional, redondeo);
            var compensacion = parametros.AdicionalCompensaIndividual ? Math.Min(montoAdicional, individual) : 0m;
            resultado.Add(new(dimension, bruta, minimo, parametros.FraccionAdicional, montoMinimo, montoAdicional, compensacion,
                anteriores.TryGetValue(dimension, out var anterior) ? anterior : 0m, fuente));
        }
        foreach (var (dimension, anterior) in anteriores.Where(a => !grupos.ContainsKey(a.Key)))
            resultado.Add(new(dimension, 0m, minimo, parametros.FraccionAdicional, 0m, 0m, 0m, anterior, fuente));

        return resultado
            .OrderBy(r => r.Dimension.BranchId).ThenBy(r => r.Dimension.ModalityCode, StringComparer.Ordinal)
            .ThenBy(r => r.Dimension.SubsegmentCode ?? string.Empty, StringComparer.Ordinal).ThenBy(r => r.Dimension.IsEmployee)
            .ThenBy(r => r.Dimension.Modelo).ToList();
    }
}
