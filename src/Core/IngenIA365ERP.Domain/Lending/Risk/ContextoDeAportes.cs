using IngenIA365ERP.Domain.Lending.Risk.Normativa;

namespace IngenIA365ERP.Domain.Lending.Risk;

/// <summary>
/// Los aportes en el riesgo (feature 013, US9, T0693, T0694; D19; FR-100, FR-103; feature 014, US3, T084; research R20, E09; FR-094): la excepción de
/// aportes en el arrastre y el descuento de aportes en la base del deterioro individual. Hasta la 014 los forzaba a falso la bandera <c>RigeD19</c>
/// (Aportes y Ahorros no ofrecían su consulta); desde la 014 cada uno lo decide su <b>parámetro</b> de la Cartera —<c>Riesgo.ExcepcionAportesArrastre</c>
/// y <c>Riesgo.DescuentoAportesDeterioro</c>—, que la cooperativa habilita por decisión (no automática), y además sólo aplican si la norma los admite y
/// la entidad tiene indicadores vigentes (sin pérdidas, con solvencia: <c>LectorDeIndicadoresDeLaEntidad</c>, C33). Sin la consulta de aportes real
/// (cooperativa sin aportes activados) el lector deja los dos parámetros en falso. (nuevo)
/// </summary>
/// <param name="ExcepcionDeArrastre">¿Habilitó la cooperativa la excepción de aportes en el arrastre?</param>
/// <param name="DescuentoEnDeterioro">¿Habilitó la cooperativa el descuento de aportes en el deterioro individual?</param>
/// <param name="IndicadoresDisponibles">¿La entidad tiene indicadores vigentes (C33)?</param>
public sealed record ContextoDeAportes(bool ExcepcionDeArrastre, bool DescuentoEnDeterioro, bool IndicadoresDisponibles)
{
    /// <summary>Sin aportes en el riesgo: los dos parámetros apagados y sin indicadores (lo de K1 a K6b, y la calificación informativa).</summary>
    public static ContextoDeAportes SinAportes { get; } = new(false, false, false);

    /// <summary>
    /// El contexto como lo dicen los casos dorados de la 013, que nombran la bandera <c>RigeD19</c>: mientras rige, ni excepción ni descuento; sin ella,
    /// los dos habilitados.
    /// </summary>
    public static ContextoDeAportes DesdeD19(bool rigeD19, bool indicadoresDisponibles) => new(!rigeD19, !rigeD19, indicadoresDisponibles);

    /// <summary>¿Rige la excepción de aportes en el arrastre con esta norma?</summary>
    public bool ExcepcionAplica(Anexo1 norma) => ExcepcionDeArrastre && Admitidos(norma);

    /// <summary>¿Se descuentan los aportes en la base del deterioro individual con esta norma?</summary>
    public bool DescuentoAplica(Anexo1 norma) => DescuentoEnDeterioro && Admitidos(norma);

    /// <summary>Por qué no aplica la excepción o el descuento, para la explicación.</summary>
    public string MotivoDeQueNoApliquen(Anexo1 norma, bool descuento)
    {
        ArgumentNullException.ThrowIfNull(norma);
        var parametro = descuento ? DescuentoEnDeterioro : ExcepcionDeArrastre;
        return !parametro ? $"rige D19: el parámetro {(descuento ? "Riesgo.DescuentoAportesDeterioro" : "Riesgo.ExcepcionAportesArrastre")} no está habilitado"
            : !IndicadoresDisponibles ? "la entidad no tiene indicadores vigentes (pérdidas, solvencia)"
            : !norma.Aportes.Deducibles ? "la norma vigente no los admite"
            : string.Empty;
    }

    private bool Admitidos(Anexo1 norma)
    {
        ArgumentNullException.ThrowIfNull(norma);
        return IndicadoresDisponibles && norma.Aportes.Deducibles;
    }
}
