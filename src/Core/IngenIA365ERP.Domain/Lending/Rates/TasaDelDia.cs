using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.DayCount;

namespace IngenIA365ERP.Domain.Lending.Rates;

/// <summary>
/// Un índice de referencia a la fecha de reajuste (IBR, IPC…), tal como se publica: <see cref="PeriodosNominales"/> nulo si es una
/// efectiva anual; si no, una nominal anual capitalizada esas veces al año, vencida (la IBR a un mes es nominal mes vencido: 12). El
/// valor llega de los valores nacionales (<c>ISeriesRegulatorias</c>); el motor no conoce ninguno. (nuevo)
/// </summary>
public sealed record IndiceDeReferencia(decimal Valor, int? PeriodosNominales = null);

/// <summary>
/// Lo que necesita <see cref="TasaDelDia.Calcular"/> para un día: la corriente pactada o reajustada (EA), la mora sellada, la usura
/// del día de la modalidad certificada (nula = no se conoce: no topa) y la sobretasa de cargos sellada al desembolsar. (nuevo)
/// </summary>
public sealed record EntradaDeTasaDelDia(
    decimal CorrienteEA,
    DefaultRateKind ClaseDeMora,
    decimal? MoraFijaEA,
    decimal? FactorDeMora,
    decimal? UsuraEA,
    decimal SobretasaDeCargos,
    DayCountConvention Convencion,
    RateEquivalence Equivalencia,
    RepaymentPeriodicity Periodicidad);

/// <summary>
/// La tasa de un día (feature 013, T28, R36; FR-020, FR-021, SC-002): la corriente y la de mora, cada una como tasa diaria ya
/// topada, con la EA de donde salen, el tope del día y si topó (para <c>Cartera.UsuraTopada</c> de US4). (nuevo)
/// </summary>
public sealed record ResultadoDeTasaDelDia(
    decimal CorrienteEA,
    decimal TasaDiariaCorriente,
    bool CorrienteTopada,
    decimal MoraEA,
    decimal TasaDiariaMora,
    bool MoraTopada,
    decimal? TopeDiarioCorriente,
    decimal? TopeDiarioMora,
    ExplicacionDeCartera Explicacion)
{
    /// <summary>¿Topó alguna de las dos?</summary>
    public bool Topada => CorrienteTopada || MoraTopada;
}

/// <summary>
/// La tasa del día del crédito (feature 013, T28, R36, M7; FR-020, FR-021; D10):
/// <list type="bullet">
/// <item>corriente = <c>min(pactada o reajustada, Tope(U_d − sobretasa de cargos))</c>;</item>
/// <item>mora = <c>min(pactada o relativa a la corriente, Tope(U_d))</c>;</item>
/// <item><c>Tope(U)</c> convierte la usura (una EA sobre el año calendario) en la tasa diaria pactable cuya efectiva bajo la
/// convención no la supera (<see cref="TopePactable"/>): en 30/360 y real/365 es la misma U; en real/360 el año tiene 365 días que
/// causan sobre base 360, y la pactable es <c>((1 + U)^(1/365))^360 − 1</c>.</item>
/// </list>
/// La tasa variable (<see cref="TasaVariable"/>) suma el margen <b>en la expresión del índice</b>, la lleva a EA y después aplica
/// piso y techo (D10); el tope de usura viene después, aquí. Todo se compara en tasa diaria. Puro. (nuevo)
/// </summary>
public static class TasaDelDia
{
    /// <summary>
    /// La EA de una tasa variable: el margen se suma al índice en su propia expresión (nominal o efectiva), el resultado se lleva a
    /// EA y luego se acota por el piso y el techo de la línea (D10). A 18 decimales.
    /// </summary>
    public static decimal TasaVariable(IndiceDeReferencia indice, decimal margen, decimal? piso = null, decimal? techo = null)
    {
        ArgumentNullException.ThrowIfNull(indice);
        if (piso is { } p && techo is { } t && p > t)
            throw new ArgumentException("El piso de la tasa variable supera el techo.", nameof(piso));

        var enSuExpresion = indice.Valor + margen;
        var ea = indice.PeriodosNominales is { } m
            ? EquivalenciaDeTasas.EfectivaDeNominal(enSuExpresion, m)
            : EquivalenciaDeTasas.Redondear(enSuExpresion);
        if (piso is { } minimo && ea < minimo) ea = minimo;
        if (techo is { } maximo && ea > maximo) ea = maximo;
        return ea;
    }

    /// <summary>
    /// La EA pactable más alta cuya efectiva bajo la convención no supera <paramref name="usura"/>: la misma U en 30/360 y real/365;
    /// en real/360, <c>RaizEntera(1 + U, 365)^360 − 1</c> (a 18 decimales).
    /// </summary>
    public static decimal TopePactable(decimal usura, DayCountConvention convencion)
    {
        if (usura < 0m) throw new ArgumentOutOfRangeException(nameof(usura), usura, "La usura no puede ser negativa.");
        var diasDelAnio = ContadorDeDias.DiasDelAnio(convencion);
        var baseDiaria = ContadorDeDias.Base(convencion);
        if (diasDelAnio == baseDiaria) return usura;
        var diariaReal = AritmeticaDecimal.RaizEntera(1m + usura, diasDelAnio);
        return EquivalenciaDeTasas.Redondear(AritmeticaDecimal.Potencia(diariaReal, baseDiaria) - 1m);
    }

    /// <summary>El tope del día como tasa diaria de la línea: la diaria de <see cref="TopePactable"/> con la equivalencia sellada.</summary>
    public static decimal TopeDiario(decimal usura, DayCountConvention convencion, RateEquivalence equivalencia, RepaymentPeriodicity periodicidad) =>
        EquivalenciaDeTasas.TasaDiaria(TopePactable(usura, convencion), convencion, equivalencia, periodicidad);

    /// <summary>La corriente y la mora del día, topadas.</summary>
    public static ResultadoDeTasaDelDia Calcular(EntradaDeTasaDelDia e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var explicacion = new ExplicacionDeCartera();

        var moraEA = e.ClaseDeMora switch
        {
            DefaultRateKind.Fixed => e.MoraFijaEA
                ?? throw new ArgumentException("La mora fija necesita su tasa.", nameof(e)),
            DefaultRateKind.RelativeToCurrent => EquivalenciaDeTasas.Redondear((e.FactorDeMora
                ?? throw new ArgumentException("La mora relativa necesita su factor.", nameof(e))) * e.CorrienteEA),
            _ => throw new ArgumentOutOfRangeException(nameof(e), e.ClaseDeMora, "Clase de mora desconocida."),
        };

        var diariaCorriente = EquivalenciaDeTasas.TasaDiaria(e.CorrienteEA, e.Convencion, e.Equivalencia, e.Periodicidad);
        var diariaMora = EquivalenciaDeTasas.TasaDiaria(moraEA, e.Convencion, e.Equivalencia, e.Periodicidad);

        if (e.UsuraEA is not { } usura)
        {
            explicacion.Linea("Sin usura conocida para el día: la tasa no se topa (la causación real se bloquea; la proyección informa ValidaHasta).");
            return new(e.CorrienteEA, diariaCorriente, false, moraEA, diariaMora, false, null, null, explicacion);
        }

        var usuraCorriente = usura - e.SobretasaDeCargos;
        if (usuraCorriente < 0m) usuraCorriente = 0m;
        var topeCorriente = TopeDiario(usuraCorriente, e.Convencion, e.Equivalencia, e.Periodicidad);
        var topeMora = TopeDiario(usura, e.Convencion, e.Equivalencia, e.Periodicidad);

        var corrienteTopada = diariaCorriente > topeCorriente;
        var moraTopada = diariaMora > topeMora;

        explicacion.Linea($"Usura del día {ExplicacionDeCartera.Numero(usura)} EA; sobretasa de cargos {ExplicacionDeCartera.Numero(e.SobretasaDeCargos)}: "
            + $"tope diario de la corriente {ExplicacionDeCartera.Numero(topeCorriente)}, de la mora {ExplicacionDeCartera.Numero(topeMora)}.");
        explicacion.Linea(corrienteTopada
            ? $"La corriente pactada ({ExplicacionDeCartera.Numero(e.CorrienteEA)} EA, diaria {ExplicacionDeCartera.Numero(diariaCorriente)}) supera el tope: se causa con el tope."
            : $"La corriente pactada ({ExplicacionDeCartera.Numero(e.CorrienteEA)} EA, diaria {ExplicacionDeCartera.Numero(diariaCorriente)}) no supera el tope.");
        explicacion.Linea(moraTopada
            ? $"La mora ({ExplicacionDeCartera.Numero(moraEA)} EA, diaria {ExplicacionDeCartera.Numero(diariaMora)}) supera la usura: se causa con el tope."
            : $"La mora ({ExplicacionDeCartera.Numero(moraEA)} EA, diaria {ExplicacionDeCartera.Numero(diariaMora)}) no supera la usura.");

        return new(
            e.CorrienteEA, corrienteTopada ? topeCorriente : diariaCorriente, corrienteTopada,
            moraEA, moraTopada ? topeMora : diariaMora, moraTopada,
            topeCorriente, topeMora, explicacion);
    }
}
