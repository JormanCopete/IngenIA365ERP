using System.Globalization;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk.Normativa;

namespace IngenIA365ERP.Domain.Lending.Risk;

/// <summary>
/// Lo que el modelo sabe de la cooperativa al cierre: su tipo de organización (la clave de la PI), si es fondo de empleados (FE, VALCUOTA,
/// FONDPLAZO), el SMMLV vigente (VALCUOTA) y si aplica el modelo <b>voluntariamente</b> (entonces la PI es la de la clave
/// <see cref="Anexo2.ClaveVoluntaria"/>, Circular Externa 100).
/// </summary>
public sealed record ContextoDelModelo(string TipoDeOrganizacion, bool EsFondoDeEmpleados, decimal Smmlv, bool AplicacionVoluntaria);

/// <summary>
/// Un crédito para el puntaje: su segmento de pérdida esperada, la altura de mora al cierre y el historial <b>completo</b> de la mora máxima
/// mensual que termina en el mes de evaluación (<see cref="HistorialDeMora.Completar"/>), más los datos de las variables que no son de mora.
/// </summary>
public sealed record CreditoParaPuntaje(int LoanId, string Segmento, int DiasDeMora, IReadOnlyList<MoraMensualCompleta> Historial)
{
    /// <summary>EA: el asociado está activo en el mes de evaluación.</summary>
    public bool AsociadoActivo { get; init; }

    /// <summary>VALCUOTA: el valor de la cuota del mes de evaluación.</summary>
    public decimal ValorDeLaCuota { get; init; }

    /// <summary>FONDPLAZO: el plazo del crédito en meses.</summary>
    public int PlazoEnMeses { get; init; }

    /// <summary>¿Crédito a una sola cuota (el incumplimiento llega antes, <c>singleInstallmentDefaultDaysPastDue</c>)?</summary>
    public bool PagoUnico { get; init; }
}

/// <summary>El valor de una variable del modelo en un crédito: 1 o 0, su coeficiente y por qué.</summary>
public sealed record ValorDeVariable(string Codigo, int Valor, decimal Coeficiente, string Detalle);

/// <summary>
/// El puntaje del modelo de un crédito (Anexo 2, numeral 3): si está incumplido (y desde hace cuántos días), Z, el puntaje redondeado a la
/// escala del documento, la calificación del modelo y cada variable. El incumplido no se puntúa: su calificación es la del Anexo 1 y su PI
/// la del incumplimiento.
/// </summary>
public sealed record PuntajeDelModelo(
    int LoanId,
    string Segmento,
    int DiasDeMora,
    bool Incumplido,
    int DiasEnIncumplimiento,
    decimal? Z,
    decimal? Puntaje,
    RatingCategory? Calificacion,
    IReadOnlyList<ValorDeVariable> Variables,
    ExplicacionDeCartera Explicacion)
{
    /// <summary>La candidata del modelo para <see cref="MotorDeCalificacion"/> (<c>CreditoParaCalificar.CalificacionDelModelo</c>); nula si está incumplido.</summary>
    public RatingCategory? CandidataParaCalificar => Incumplido ? null : Calificacion;
}

/// <summary>
/// Lo que el cálculo de la pérdida esperada necesita además del puntaje: la calificación <b>final</b> (la máxima de
/// <see cref="MotorDeCalificacion"/>, con el arrastre que alinea consumo con y sin libranza), si queda incumplido por arrastre, la mora actual
/// del deudor en sus créditos alineados (para la homologación), los saldos, la suspensión, el tipo de garantía de la tabla de PDI y los aportes
/// (que no se descuentan mientras rija D19).
/// </summary>
public sealed record CreditoParaPerdidaEsperada(PuntajeDelModelo Puntaje, RatingCategory CalificacionFinal, SaldosParaDeterioro Saldos, bool Suspendido,
    string TipoDeGarantia)
{
    /// <summary>¿Otro crédito del deudor en la misma modalidad está incumplido? Lleva a éste al incumplimiento (Anexo 2, num. 4.1).</summary>
    public bool IncumplidoPorArrastre { get; init; }

    /// <summary>La mora máxima del deudor en todos sus créditos alineados por modalidad (Anexo 2, num. 5); nula = la del crédito.</summary>
    public int? MoraActualDelDeudor { get; init; }

    public decimal AportesAProrrata { get; init; }
}

/// <summary>
/// La pérdida esperada de un crédito (data-model §23.2, columnas K6a): PI, PDI y exposición (VEA) por concepto, el deterioro por concepto y la
/// calificación contable homologada, con la explicación por componente (FR-106). Los nombres de PI, PDI y exposición son los que piden los
/// formatos del SICSES de pérdida esperada (9083 «cálculo de deterioro de cartera»; 9027 y 9081 cambian, pendiente del bloque 3).
/// </summary>
public sealed record PerdidaEsperadaDeCredito(
    int LoanId,
    string Segmento,
    bool Incumplido,
    int DiasEnIncumplimiento,
    RatingCategory CalificacionFinal,
    string ClaveDePi,
    decimal ProbabilidadDeIncumplimiento,
    string TipoDeGarantia,
    decimal PerdidaDadoElIncumplimiento,
    decimal ExposicionCapital,
    decimal ExposicionIntereses,
    decimal ExposicionOtros,
    decimal DeterioroCapital,
    decimal DeterioroIntereses,
    decimal DeterioroOtros,
    RatingCategory CalificacionContable,
    bool Homologada,
    ExplicacionDeCartera Explicacion)
{
    /// <summary>VEA: el saldo con intereses y otros conceptos, sin aportes mientras rija D19 (<c>ExposureAtDefault</c>).</summary>
    public decimal Exposicion => ExposicionCapital + ExposicionIntereses + ExposicionOtros;

    public decimal Total => DeterioroCapital + DeterioroIntereses + DeterioroOtros;

    /// <summary>La causa de la calificación contable: <see cref="RatingCause.Homologation"/> si salió de la tabla de homologación.</summary>
    public RatingCause? CausaContable => Homologada ? RatingCause.Homologation : null;
}

/// <summary>Un crédito para la alineación por deudor y modalidad (segmento SARC).</summary>
public sealed record CreditoParaAlinear(int LoanId, int DeudorId, string SegmentoSarc, int DiasDeMora, bool Incumplido);

/// <summary>
/// El motor de pérdida esperada del Anexo 2 (feature 013, K6a, T1029; T46, R55; FR-099, FR-105, FR-106; SC-021, SC-023), en tres pasos
/// que el cierre encadena con <see cref="MotorDeCalificacion"/> en medio:
/// <list type="number">
/// <item><see cref="Puntuar"/>: si el crédito está incumplido (mora mayor que el umbral de su segmento, o pago único con mora mayor que el suyo),
/// no se puntúa; si no, cada variable del documento vale 1 o 0 (EA, FE, VALCUOTA, FONDPLAZO y las de mora sobre el historial completo), Z es el
/// intercepto más la suma de los coeficientes, el puntaje es <see cref="FuncionLogistica"/> redondeado a la escala del documento —el único
/// redondeo, pregunta E9— y la calificación del modelo la da el primer corte que lo alcanza.</item>
/// <item>El cierre califica con <see cref="MotorDeCalificacion"/> y la candidata del modelo (<see cref="PuntajeDelModelo.CandidataParaCalificar"/>):
/// la calificación final es la máxima (Anexo 1, numeral 3) y el arrastre por deudor y segmento SARC hace la alineación de consumo con y sin
/// libranza. <see cref="IncumplidosPorArrastre"/> y <see cref="MoraActualPorDeudor"/> dan lo que la alineación del Anexo 2 añade.</item>
/// <item><see cref="Calcular"/>: PI de la tabla del tipo de organización (o la voluntaria) en la calificación final, o la del incumplido; PDI
/// del tipo de garantía —la del segmento mientras no hay incumplimiento si el documento la trae, si no el primer tramo; en incumplimiento, el
/// tramo de los días que lleva—; VEA por concepto; deterioro = PI × PDI × VEA por concepto, al centavo o al peso
/// (<c>Riesgo.RedondeoDeterioro</c>), y lo causado de un crédito suspendido al 100 % si el documento lo dice; y la homologación a A–E
/// (<see cref="HomologacionDeCalificacion.DePerdidaEsperada"/>).</item>
/// </list>
/// Puro: sin IO, sin reloj, sólo <c>decimal</c> y <c>DateOnly</c>, sin valores legales (todo llega en el <see cref="Anexo2"/> y el contexto). (nuevo)
/// </summary>
public static class MotorDePerdidaEsperada
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ------------------------------------------------------------------------------------------------- puntaje --

    public static PuntajeDelModelo Puntuar(Anexo2 anexo, CreditoParaPuntaje c, ContextoDelModelo contexto)
    {
        ArgumentNullException.ThrowIfNull(anexo);
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(contexto);
        if (c.DiasDeMora < 0) throw new ArgumentOutOfRangeException(nameof(c), c.DiasDeMora, "Los días de mora no son negativos.");
        var segmento = anexo.SegmentoObligatorio(c.Segmento);
        var explicacion = new ExplicacionDeCartera();

        var diasEnIncumplimiento = -1;
        if (c.DiasDeMora > segmento.DiasDeMoraDeIncumplimiento)
        {
            diasEnIncumplimiento = c.DiasDeMora - segmento.DiasDeMoraDeIncumplimiento;
            explicacion.Linea($"Incumplido: {E(c.DiasDeMora)} días de mora, más de {E(segmento.DiasDeMoraDeIncumplimiento)} en {segmento.Codigo}; "
                + $"lleva {E(diasEnIncumplimiento)} días en incumplimiento.");
        }
        if (c.PagoUnico && anexo.DiasDeIncumplimientoDePagoUnico is { } umbral && c.DiasDeMora > umbral)
        {
            diasEnIncumplimiento = Math.Max(diasEnIncumplimiento, c.DiasDeMora - umbral);
            explicacion.Linea($"Incumplido: crédito a una sola cuota con {E(c.DiasDeMora)} días de mora, más de {E(umbral)}.");
        }
        if (diasEnIncumplimiento >= 0)
        {
            explicacion.Linea("El incumplido no se puntúa: su calificación es la del Anexo 1 y su PI la del incumplimiento.");
            return new(c.LoanId, segmento.Codigo, c.DiasDeMora, true, diasEnIncumplimiento, null, null, null, [], explicacion);
        }

        if (c.Historial.Count == 0) throw new ArgumentException("El puntaje necesita el historial de mora, al menos el mes de evaluación.", nameof(c));
        var historial = c.Historial.OrderBy(m => m.Anio).ThenBy(m => m.Mes).ToList();
        var variables = segmento.Terminos.Select(t => Evaluar(anexo.Variable(t.Variable), t.Coeficiente, c, historial, contexto)).ToList();
        var z = segmento.Intercepto + variables.Sum(v => v.Coeficiente * v.Valor);
        var puntaje = decimal.Round(FuncionLogistica.Evaluar(z), anexo.DecimalesDelPuntaje, MidpointRounding.AwayFromZero);
        var calificacion = anexo.CalificacionPorPuntaje(segmento.Codigo, puntaje);
        var corte = segmento.Cortes.First(x => x.Categoria == calificacion);

        foreach (var v in variables) explicacion.Linea($"{v.Codigo} = {E(v.Valor)} (coeficiente {N(v.Coeficiente)}): {v.Detalle}.");
        explicacion.Linea($"Z = {N(segmento.Intercepto)}"
            + string.Concat(variables.Where(v => v.Valor != 0).Select(v => $" {(v.Coeficiente < 0m ? "−" : "+")} {N(Math.Abs(v.Coeficiente))}·{v.Codigo}"))
            + $" = {N(z)}.");
        explicacion.Linea($"Puntaje = 1 / (1 + e^(−Z)) = {N(puntaje)} ({E(anexo.DecimalesDelPuntaje)} decimales): {calificacion} en {segmento.Codigo} "
            + $"(límite superior {N(corte.Hasta)}).");
        return new(c.LoanId, segmento.Codigo, c.DiasDeMora, false, 0, z, puntaje, calificacion, variables, explicacion);
    }

    private static ValorDeVariable Evaluar(VariableDelModelo v, decimal coeficiente, CreditoParaPuntaje c, List<MoraMensualCompleta> historial,
        ContextoDelModelo contexto)
    {
        switch (v.Tipo)
        {
            case TipoDeVariableDelModelo.ActiveAssociate:
                return new(v.Codigo, c.AsociadoActivo ? 1 : 0, coeficiente, c.AsociadoActivo ? "asociado activo" : "asociado no activo");
            case TipoDeVariableDelModelo.EmployeeFund:
                return new(v.Codigo, contexto.EsFondoDeEmpleados ? 1 : 0, coeficiente,
                    contexto.EsFondoDeEmpleados ? "la cooperativa es fondo de empleados" : $"{contexto.TipoDeOrganizacion} no es fondo de empleados");
            case TipoDeVariableDelModelo.InstallmentAtMostSmmlvFraction:
            {
                var tope = contexto.Smmlv * (v.Fraccion ?? 0m);
                if (v.SoloFondoDeEmpleados && !contexto.EsFondoDeEmpleados) return new(v.Codigo, 0, coeficiente, "sólo aplica a fondos de empleados");
                var cumple = c.ValorDeLaCuota <= tope;
                return new(v.Codigo, cumple ? 1 : 0, coeficiente, $"cuota {N(c.ValorDeLaCuota)} {(cumple ? "≤" : ">")} {N(v.Fraccion ?? 0m)} × SMMLV {N(contexto.Smmlv)} = {N(tope)}");
            }
            case TipoDeVariableDelModelo.TermAtMostMonths:
            {
                if (v.SoloFondoDeEmpleados && !contexto.EsFondoDeEmpleados) return new(v.Codigo, 0, coeficiente, "sólo aplica a fondos de empleados");
                var cumple = c.PlazoEnMeses <= (v.Meses ?? 0);
                return new(v.Codigo, cumple ? 1 : 0, coeficiente, $"plazo {E(c.PlazoEnMeses)} meses {(cumple ? "≤" : ">")} {E(v.Meses ?? 0)}");
            }
            case TipoDeVariableDelModelo.ArrearsHistory:
            {
                var detalles = new List<string>();
                var cumple = true;
                foreach (var condicion in v.Condiciones)
                {
                    var ventana = historial.Skip(Math.Max(0, historial.Count - condicion.Meses)).ToList();
                    bool parcial;
                    if (condicion.Medida == MedidaDeMora.Max)
                    {
                        var maxima = ventana.Max(m => m.MaxDiasDeMora);
                        parcial = condicion.Contiene(maxima);
                        detalles.Add($"mora máxima de {E(ventana.Count)} meses {E(maxima)} días {(parcial ? "en" : "fuera de")} {Rango(condicion)}");
                    }
                    else
                    {
                        var meses = ventana.Count(m => condicion.Contiene(m.MaxDiasDeMora));
                        parcial = meses > 0;
                        detalles.Add($"{E(meses)} de {E(ventana.Count)} meses con mora en {Rango(condicion)}");
                    }
                    cumple &= parcial;
                }
                return new(v.Codigo, cumple ? 1 : 0, coeficiente, string.Join("; ", detalles));
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(v), v.Tipo, "Tipo de variable desconocido.");
        }
    }

    private static string Rango(CondicionDeMora c) => c.DiasHasta is { } h ? $"{E(c.DiasDesde)}–{E(h)}" : $"{E(c.DiasDesde)} o más";

    // ------------------------------------------------------------------------------------------------ alineación --

    /// <summary>
    /// Los créditos que quedan incumplidos por arrastre (Anexo 2, num. 4.1): los de un deudor que tiene otro crédito incumplido en la misma
    /// modalidad (segmento SARC), que no lo están por sí mismos.
    /// </summary>
    public static IReadOnlySet<int> IncumplidosPorArrastre(IEnumerable<CreditoParaAlinear> creditos)
    {
        var lista = creditos.ToList();
        var deudores = lista.Where(c => c.Incumplido).Select(c => (c.DeudorId, c.SegmentoSarc)).ToHashSet();
        return lista.Where(c => !c.Incumplido && deudores.Contains((c.DeudorId, c.SegmentoSarc))).Select(c => c.LoanId).ToHashSet();
    }

    /// <summary>La mora actual de cada crédito para la homologación: la máxima del deudor en sus créditos de la misma modalidad.</summary>
    public static IReadOnlyDictionary<int, int> MoraActualPorDeudor(IEnumerable<CreditoParaAlinear> creditos)
    {
        var lista = creditos.ToList();
        var maxima = lista.GroupBy(c => (c.DeudorId, c.SegmentoSarc)).ToDictionary(g => g.Key, g => g.Max(c => c.DiasDeMora));
        return lista.ToDictionary(c => c.LoanId, c => maxima[(c.DeudorId, c.SegmentoSarc)]);
    }

    // -------------------------------------------------------------------------------------------- pérdida esperada --

    /// <exception cref="InvalidOperationException">El documento no trae la PI del tipo de organización (o la voluntaria) o la PDI de la garantía:
    /// el motor se niega en vez de inventarla.</exception>
    /// <exception cref="NotSupportedException">Aportes por descontar sin D19: su cálculo es de la feature de Aportes.</exception>
    public static PerdidaEsperadaDeCredito Calcular(Anexo2 anexo, CreditoParaPerdidaEsperada c, ContextoDelModelo contexto, ContextoDeAportes aportes,
        RedondeoDeDeterioro redondeo)
    {
        ArgumentNullException.ThrowIfNull(anexo);
        ArgumentNullException.ThrowIfNull(c);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(aportes);
        if (c.Saldos.Capital < 0m || c.Saldos.InteresCorriente < 0m || c.Saldos.InteresDeMora < 0m || c.Saldos.Otros < 0m)
            throw new ArgumentOutOfRangeException(nameof(c), "Los saldos no son negativos.");
        if (!aportes.RigeD19 && c.AportesAProrrata > 0m)
            throw new NotSupportedException("El descuento de aportes del VEA es de la feature de Aportes (D19); hasta entonces no se calcula.");

        var p = c.Puntaje;
        var segmento = anexo.SegmentoObligatorio(p.Segmento);
        var incumplido = p.Incumplido || c.IncumplidoPorArrastre;
        var diasEnIncumplimiento = p.Incumplido ? p.DiasEnIncumplimiento : 0;
        var explicacion = new ExplicacionDeCartera().Agregar(p.Explicacion);
        if (!p.Incumplido && c.IncumplidoPorArrastre)
            explicacion.Linea("Incumplido por arrastre: otro crédito del deudor en la misma modalidad está incumplido.");

        var clave = contexto.AplicacionVoluntaria ? Anexo2.ClaveVoluntaria : contexto.TipoDeOrganizacion;
        var tabla = anexo.TablaDePi(segmento.Codigo, clave) ?? throw new InvalidOperationException(contexto.AplicacionVoluntaria
            ? $"El Anexo 2 vigente no trae la PI de la aplicación voluntaria ({Anexo2.ClaveVoluntaria}) en {segmento.Codigo}."
            : $"El Anexo 2 vigente no trae la PI del tipo de organización {clave} en {segmento.Codigo}.");
        var pi = incumplido ? tabla.Incumplido : tabla.De(c.CalificacionFinal);
        explicacion.Linea(incumplido
            ? $"PI = {N(pi)}: la del incumplido ({clave}, {segmento.Codigo})."
            : $"PI = {N(pi)}: calificación final {c.CalificacionFinal} ({clave}, {segmento.Codigo}).");

        var garantia = anexo.Pdi(c.TipoDeGarantia)
                       ?? throw new InvalidOperationException($"El Anexo 2 vigente no trae la PDI del tipo de garantía {c.TipoDeGarantia}.");
        decimal pdi;
        if (incumplido)
        {
            pdi = garantia.EnIncumplimiento(diasEnIncumplimiento);
            var tramo = garantia.Tramos.Last(t => t.DiasDesde <= diasEnIncumplimiento);
            explicacion.Linea($"PDI = {N(pdi)}: {garantia.Nombre} con {E(diasEnIncumplimiento)} días en incumplimiento (tramo desde {E(tramo.DiasDesde)}).");
        }
        else if (segmento.PdiSinIncumplimiento.FirstOrDefault(x => x.TipoDeGarantia == garantia.TipoDeGarantia) is { } propia)
        {
            pdi = propia.Fraccion;
            explicacion.Linea($"PDI = {N(pdi)}: {garantia.Nombre} sin incumplimiento en {segmento.Codigo}.");
        }
        else
        {
            pdi = garantia.Tramos[0].Fraccion;
            explicacion.Linea($"PDI = {N(pdi)}: {garantia.Nombre} sin incumplimiento (primer tramo).");
        }

        var intereses = c.Saldos.InteresCorriente + c.Saldos.InteresDeMora;
        explicacion.Linea($"VEA = capital {N(c.Saldos.Capital)} + intereses {N(intereses)} + otros {N(c.Saldos.Otros)} = "
            + $"{N(c.Saldos.Capital + intereses + c.Saldos.Otros)}; aportes no descontados ("
            + (aportes.RigeD19 ? "rige D19: Aportes y Ahorros no ofrecen su consulta" : "no hay aportes a prorrata") + ").");

        var factor = pi * pdi;
        var capital = MotorDeDeterioroIndividual.Redondear(c.Saldos.Capital * factor, redondeo);
        explicacion.Linea($"Capital: {N(c.Saldos.Capital)} × {N(pi)} × {N(pdi)} = {N(capital)}.");
        decimal deterioroIntereses, otros;
        if (c.Suspendido && anexo.SuspendidosAlCien)
        {
            deterioroIntereses = MotorDeDeterioroIndividual.Redondear(intereses, redondeo);
            otros = MotorDeDeterioroIndividual.Redondear(c.Saldos.Otros, redondeo);
            explicacion.Linea($"Causación suspendida: intereses {N(deterioroIntereses)} y otros conceptos {N(otros)} al 100 %.");
        }
        else
        {
            deterioroIntereses = MotorDeDeterioroIndividual.Redondear(intereses * factor, redondeo);
            otros = MotorDeDeterioroIndividual.Redondear(c.Saldos.Otros * factor, redondeo);
            explicacion.Linea($"Intereses: {N(intereses)} × {N(pi)} × {N(pdi)} = {N(deterioroIntereses)}; otros: {N(c.Saldos.Otros)} × {N(pi)} × {N(pdi)} = {N(otros)}.");
        }
        explicacion.Linea($"Pérdida esperada = {N(capital + deterioroIntereses + otros)} (redondeo al {redondeo.ToString().ToLowerInvariant()}).");

        var homologacion = HomologacionDeCalificacion.DePerdidaEsperada(anexo, segmento.Codigo, c.CalificacionFinal, p.CandidataParaCalificar, incumplido,
            c.MoraActualDelDeudor ?? p.DiasDeMora);
        explicacion.Linea(homologacion.Detalle);

        return new(p.LoanId, segmento.Codigo, incumplido, diasEnIncumplimiento, c.CalificacionFinal, clave, pi, garantia.TipoDeGarantia, pdi,
            c.Saldos.Capital, intereses, c.Saldos.Otros, capital, deterioroIntereses, otros, homologacion.Contable, homologacion.Homologada, explicacion);
    }

    private static string N(decimal valor) => ExplicacionDeCartera.Numero(valor);

    private static string E(int valor) => valor.ToString(Inv);
}
