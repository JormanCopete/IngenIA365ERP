using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Risk;

namespace IngenIA365ERP.Application.Lending.Risk;

/// <summary>
/// Los indicadores de la entidad tal como los mira Cartera a una fecha (feature 013, US9, T0703; C33; contracts/contabilidad.md §15): el
/// patrimonio técnico y la solvencia registrados en Contabilidad, con su corte, y si están <b>vigentes</b> —el último con corte ≤ la fecha y no
/// más viejo que <c>Contabilidad.IndicadoresVigenciaMeses</c>—. Contabilidad no juzga la vigencia: la decide este lector. (nuevo)
/// </summary>
/// <param name="Fecha">La fecha consultada.</param>
/// <param name="PatrimonioTecnico">El último patrimonio técnico registrado (vigente o no).</param>
/// <param name="Solvencia">La última relación de solvencia registrada (vigente o no).</param>
/// <param name="PatrimonioTecnicoVigente">Hay patrimonio técnico positivo y dentro de la vigencia.</param>
/// <param name="SolvenciaVigente">Hay relación de solvencia dentro de la vigencia.</param>
/// <param name="VigenciaMeses">Los meses de vigencia que rigen (nulo si el parámetro no se pudo leer: no se acota).</param>
/// <param name="ConPerdidas">Las pérdidas acumuladas a la fecha son positivas.</param>
public sealed record IndicadoresDeCartera(
    DateOnly Fecha,
    IndicadorRegistradoDto? PatrimonioTecnico,
    IndicadorRegistradoDto? Solvencia,
    bool PatrimonioTecnicoVigente,
    bool SolvenciaVigente,
    int? VigenciaMeses,
    bool ConPerdidas)
{
    /// <summary>
    /// «Disponible» para el riesgo (D19, FR-100, FR-103): patrimonio técnico y solvencia vigentes. Sin dato vigente, lo conservador: ni la
    /// excepción de aportes del arrastre ni el descuento de aportes en el deterioro.
    /// </summary>
    public bool Disponibles => PatrimonioTecnicoVigente && SolvenciaVigente;

    /// <summary>
    /// El contexto de aportes del motor (feature 014, T084): la excepción del arrastre y el descuento en el deterioro como los dejan los parámetros de la
    /// Cartera, con la entidad disponible y sin pérdidas.
    /// </summary>
    public ContextoDeAportes ContextoDeAportes(bool excepcionDeArrastre, bool descuentoEnDeterioro) =>
        new(excepcionDeArrastre, descuentoEnDeterioro, Disponibles && !ConPerdidas);
}

/// <summary>
/// El lector único de los indicadores de la entidad para Cartera (feature 013, US9, T0703; C33): lo usan el motor de calificación y deterioro
/// (D19) y la validación de concentración de US2 (que deja de leer el puerto por su cuenta). Lee por
/// <see cref="IContabilidadParaCartera.IndicadoresDeLaEntidadAsync"/> y la vigencia de <c>Contabilidad.IndicadoresVigenciaMeses</c>; si
/// Contabilidad no responde, «no disponible» —lo conservador—, nunca una cifra supuesta. (nuevo)
/// </summary>
public sealed class LectorDeIndicadoresDeLaEntidad(IContabilidadParaCartera contabilidad, ReglasDeParametrosDeCartera parametros)
{
    public async Task<IndicadoresDeCartera> LeerAsync(DateOnly fecha, CancellationToken ct)
    {
        var indicadores = await contabilidad.IndicadoresDeLaEntidadAsync(fecha, ct);
        var meses = await parametros.LeerAsync(ParametrosDeCartera.ContabilidadIndicadoresVigenciaMeses, fecha, ct: ct);
        var vigencia = meses.IsSuccess && meses.Value.Valor is int m ? m : (int?)null;
        if (indicadores.IsFailure) return new IndicadoresDeCartera(fecha, null, null, false, false, vigencia, false);

        var i = indicadores.Value;
        return new IndicadoresDeCartera(fecha, i.TechnicalCapital, i.SolvencyRatio,
            Vigente(i.TechnicalCapital, fecha, vigencia) && i.TechnicalCapital!.Value > 0m,
            Vigente(i.SolvencyRatio, fecha, vigencia),
            vigencia,
            i.AccumulatedLosses > 0m);
    }

    /// <summary>Corte ≤ la fecha y no más viejo que la vigencia (C33).</summary>
    public static bool Vigente(IndicadorRegistradoDto? indicador, DateOnly fecha, int? vigenciaMeses) =>
        indicador is { } x && x.CutoffDate <= fecha && (vigenciaMeses is not { } v || x.CutoffDate >= fecha.AddMonths(-v));
}
