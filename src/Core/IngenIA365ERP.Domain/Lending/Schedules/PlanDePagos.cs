using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Schedules;

// Feature 013, US1 (T0349): lo que recibe y devuelve GeneradorDePlan. No son tablas: el plan guardado es
// LND_RepaymentScheduleVersions / LND_RepaymentInstallments, que lo copian. (nuevo)

/// <summary>Una cuota extraordinaria (prima) pactada: el número de cuota (de las que se pagan, desde 1) y su valor en pesos.</summary>
public sealed record CuotaExtraordinaria(int Cuota, decimal Valor);

/// <summary>
/// Un seguro del plan (data-model §1.6; D9): su base (saldo de capital al inicio del período o monto inicial), la tarifa por período
/// de cobro como fracción de la base, la periodicidad de cobro y si computa para la usura (la cooperativa retiene parte de la prima
/// o se autoasegura, FR-021).
/// </summary>
public sealed record SeguroDelPlan(string Codigo, InsuranceBase Base, decimal Tarifa, RepaymentPeriodicity PeriodicidadDeCobro, bool Computa);

/// <summary>
/// Lo que se proyecta (FR-022, FR-023; T29): monto y fecha de desembolso, número de períodos del plazo, periodicidad, sistema, tasa
/// EA con su convención y equivalencia, y el redondeo sellado; opcionales el primer vencimiento, el día de pago, las extraordinarias,
/// la gracia y los seguros.
/// </summary>
/// <param name="Periodos">
/// Los períodos del plazo. Con gracia a capital, los primeros <see cref="PeriodosDeGracia"/> son cuotas de sólo interés; con gracia
/// total, son períodos sin cuota y el plan tiene <c>Periodos − PeriodosDeGracia</c> cuotas.
/// </param>
public sealed record EntradaDePlan(
    decimal Monto,
    DateOnly FechaDesembolso,
    int Periodos,
    RepaymentPeriodicity Periodicidad,
    AmortizationSystem Sistema,
    decimal TasaEfectivaAnual,
    DayCountConvention Convencion,
    RateEquivalence Equivalencia,
    RedondeoDeCartera Redondeo)
{
    public DateOnly? PrimerVencimiento { get; init; }

    public int? DiaDePago { get; init; }

    public IReadOnlyList<CuotaExtraordinaria> Extraordinarias { get; init; } = [];

    public GraceKind Gracia { get; init; } = GraceKind.None;

    public int PeriodosDeGracia { get; init; }

    /// <summary>Con gracia total: el interés de la gracia va en la primera cuota (defecto) o distribuido en <see cref="CuotasDeDistribucion"/>.</summary>
    public GraceInterestTreatment? TratamientoDeGracia { get; init; }

    public int? CuotasDeDistribucion { get; init; }

    public IReadOnlyList<SeguroDelPlan> Seguros { get; init; } = [];
}

/// <summary>
/// Un tramo de interés de una cuota (US4, T0486): de <see cref="Desde"/> (excluida) a <see cref="Hasta"/> (incluida) causa
/// <see cref="Base"/> con <see cref="TasaDiaria"/>; cambia de tramo cuando un desembolso, un abono o un reajuste cambian la base o la
/// tasa a mitad del período. El interés de la cuota es el redondeo de la suma exacta de sus tramos.
/// </summary>
public sealed record TramoDeInteres(DateOnly Desde, DateOnly Hasta, decimal Base, decimal TasaDiaria);

/// <summary>Un período sin cuota de la gracia total: de <see cref="Desde"/> (excluida) a <see cref="Hasta"/> (incluida).</summary>
public sealed record PeriodoDeGracia(DateOnly Desde, DateOnly Hasta);

/// <summary>
/// Lo que regenera el plan desde una fecha (US4, T0486; D12, FR-067): la fecha valor del hecho, el cambio de capital (un tramo suma, un
/// abono resta) y, si cambió, la tasa EA nueva (reajuste). US5 (T0536) agrega la elección del deudor ante un abono extraordinario.
/// </summary>
public sealed record EntradaDeRegeneracion(DateOnly Fecha)
{
    public decimal CambioDeCapital { get; init; }

    public decimal? NuevaTasaEfectivaAnual { get; init; }

    /// <summary>
    /// La elección del deudor ante un abono (FR-075): <see cref="PrepaymentChoice.ReduceTerm"/> conserva la cuota y quita cuotas del final;
    /// <see cref="PrepaymentChoice.ReduceInstallment"/> (o nula: un tramo, un reajuste) conserva las cuotas y recalcula su valor.
    /// </summary>
    public PrepaymentChoice? Eleccion { get; init; }
}

/// <summary>
/// Lo que regenera el plan por una <b>novedad</b> (K3, T0874; FR-091, T29): el mecanismo, la fecha de la decisión (desde la que corre el plan
/// nuevo), las condiciones selladas de las que parte (<see cref="Base"/>: sistema, convención, equivalencia, redondeo, seguros, periodicidad,
/// tasa y día de pago que no cambian), el saldo de capital del crédito a esa fecha y las cuotas nuevas. Opcionales: el interés que se
/// capitaliza, los saldos de los créditos que recoge una refinanciación, la tasa, la periodicidad, el día de pago, el primer vencimiento y la
/// gracia nuevos, y la primera cuota del plan nuevo cuando el crédito conserva su número (prórroga, cambio de fecha o de tasa: la siguiente a
/// la última que ya venció; en un crédito enlazado, 1). <see cref="Costos"/> son los de tramitarla: nunca entran al plan del deudor.
/// </summary>
public sealed record EntradaDeNovedadDePlan(
    NoveltyMechanism Mecanismo,
    DateOnly Fecha,
    EntradaDePlan Base,
    decimal SaldoDeCapital,
    int Cuotas)
{
    public decimal InteresCapitalizado { get; init; }

    /// <summary>Refinanciación: el saldo de capital de cada crédito que recoge (el principal va en <see cref="SaldoDeCapital"/>).</summary>
    public IReadOnlyList<decimal> SaldosRecogidos { get; init; } = [];

    public decimal Costos { get; init; }

    public decimal? TasaEfectivaAnual { get; init; }

    public RepaymentPeriodicity? Periodicidad { get; init; }

    public int? DiaDePago { get; init; }

    public DateOnly? PrimerVencimiento { get; init; }

    public GraceKind? Gracia { get; init; }

    public int PeriodosDeGracia { get; init; }

    public GraceInterestTreatment? TratamientoDeGracia { get; init; }

    public int? CuotasDeDistribucion { get; init; }

    public int PrimeraCuota { get; init; } = 1;

    /// <summary>El capital del plan nuevo: el saldo, lo recogido y lo capitalizado (los costos no).</summary>
    public decimal Capital => SaldoDeCapital + SaldosRecogidos.Sum() + InteresCapitalizado;
}

/// <summary>Una cuota de un plan migrado (D5): número, vencimiento y capital pendiente al corte; el inicio de su período si se conoce.</summary>
public sealed record CuotaMigrada(int Numero, DateOnly Vencimiento, decimal Capital)
{
    public DateOnly? Desde { get; init; }
}

/// <summary>
/// El plan de un crédito migrado (US4, T0486; D5, FR-140): el corte, el saldo de capital al corte, las cuotas cargadas con su capital
/// (que se conserva) y las condiciones con que el motor causa desde el corte.
/// </summary>
public sealed record EntradaDePlanMigrado(
    DateOnly Corte,
    decimal SaldoDeCapital,
    IReadOnlyList<CuotaMigrada> Cuotas,
    RepaymentPeriodicity Periodicidad,
    AmortizationSystem Sistema,
    decimal TasaEfectivaAnual,
    DayCountConvention Convencion,
    RateEquivalence Equivalencia,
    RedondeoDeCartera Redondeo);

/// <summary>Lo causado de un concepto en una cuota hasta una fecha (proyección con pagos a tiempo, US4).</summary>
public sealed record CausadoDeCuota(int Cuota, LoanConcept Concepto, decimal Valor);

/// <summary>Lo que cobra un seguro en una cuota.</summary>
public sealed record SeguroDeCuota(string Codigo, decimal Valor, bool Computa);

/// <summary>
/// Una cuota del plan: su período (de <see cref="Desde"/> excluida a <see cref="Vencimiento"/> incluida, <see cref="Dias"/> de la
/// convención y la tasa de esos días), el saldo de capital al empezar, lo que paga de capital, interés (con la porción de interés de
/// la gracia total, que también va en <see cref="InteresDeGracia"/>) y seguros, y el saldo al terminar.
/// </summary>
public sealed record CuotaDelPlan(
    int Numero,
    DateOnly Desde,
    DateOnly Vencimiento,
    int Dias,
    decimal TasaDelPeriodo,
    decimal SaldoInicial,
    decimal Capital,
    decimal Interes,
    decimal InteresDeGracia,
    IReadOnlyList<SeguroDeCuota> Seguros,
    decimal SaldoFinal,
    bool SoloInteres)
{
    /// <summary>La cuota extraordinaria (prima) que lleva, a la unidad sellada; cero si no lleva (US4: la regeneración la conserva).</summary>
    public decimal Extraordinaria { get; init; }

    /// <summary>
    /// Los tramos de base y tasa con que causa su interés suponiendo pagos a tiempo (US4, T0486). Nulo: uno solo, de
    /// <see cref="Desde"/> a <see cref="Vencimiento"/> sobre <see cref="SaldoInicial"/> con la tasa diaria del plan. Una cuota
    /// regenerada a mitad de su período (un tramo, un reajuste) lleva los suyos; una migrada ya vencida al corte, ninguno.
    /// </summary>
    public IReadOnlyList<TramoDeInteres>? Tramos { get; init; }

    /// <summary>Los tramos de interés de la cuota: los suyos o el único del período con <paramref name="tasaDiaria"/>.</summary>
    public IReadOnlyList<TramoDeInteres> TramosDeInteres(decimal tasaDiaria) =>
        Tramos ?? [new TramoDeInteres(Desde, Vencimiento, SaldoInicial, tasaDiaria)];

    public decimal Seguro => Seguros.Sum(s => s.Valor);

    /// <summary>Lo que de los seguros computa para la usura.</summary>
    public decimal SeguroComputable => Seguros.Where(s => s.Computa).Sum(s => s.Valor);

    public decimal Total => Capital + Interes + Seguro;
}

/// <summary>
/// El plan proyectado (T29, R37): la versión de reglas que lo calculó, las tasas (diaria con su única aproximación y nominal del
/// período, informativa), la cuota nivelada de referencia, las cuotas y la explicación; <see cref="ResiduoDeLaUltimaCuota"/> es lo
/// que la última cuota absorbió frente a la teórica (SC-017: Σ capital = monto).
/// </summary>
public sealed record PlanDePagos(
    string RulesVersion,
    EntradaDePlan Entrada,
    decimal TasaDiaria,
    decimal TasaNominalDelPeriodo,
    decimal? CuotaNivelada,
    decimal InteresDeGracia,
    decimal ResiduoDeLaUltimaCuota,
    IReadOnlyList<CuotaDelPlan> Cuotas,
    ExplicacionDeCartera Explicacion)
{
    /// <summary>
    /// Los períodos sin cuota de una gracia total, en orden (US4): cada uno causa sobre el monto sin interés sobre interés y lo causado
    /// va a la primera cuota. Vacío sin gracia total.
    /// </summary>
    public IReadOnlyList<PeriodoDeGracia> PeriodosDeGracia { get; init; } = [];

    /// <summary>El número de la primera cuota que esta versión regeneró (1 si nació entera; US4, data-model §4.4).</summary>
    public int PrimeraCuotaRegenerada { get; init; } = 1;

    public decimal TotalCapital => Cuotas.Sum(c => c.Capital);

    public decimal TotalInteres => Cuotas.Sum(c => c.Interes);

    public decimal TotalSeguro => Cuotas.Sum(c => c.Seguro);

    public decimal TotalAPagar => Cuotas.Sum(c => c.Total);

    /// <summary>¿La gracia total reparte su interés en varias cuotas (<see cref="GraceInterestTreatment.Distributed"/>)?</summary>
    public bool RepartaLaGracia => PeriodosDeGracia.Count > 0 && Cuotas.Skip(1).Any(c => c.InteresDeGracia != 0m);

    /// <summary>
    /// A qué cuota pertenece el interés corriente que el libro lleva en la primera (US5, T0533): la causación pone todo el interés de una
    /// gracia total en la cuota 1, pero con <see cref="GraceInterestTreatment.Distributed"/> el plan lo cobra repartido. Del
    /// <paramref name="saldoDeLaPrimera"/> se atribuye a cada cuota j ≥ 2, empezando por la última, a lo sumo su porción
    /// (<see cref="CuotaDelPlan.InteresDeGracia"/>), y el resto a la cuota 1: lo que ya se pagó fue lo más antiguo, así que lo que queda es
    /// de las cuotas más nuevas. Así la prelación y la altura de mora cobran cada porción con su cuota. Sin reparto, todo es de la 1.
    /// </summary>
    public IReadOnlyDictionary<int, decimal> AtribuirInteresDeLaPrimera(decimal saldoDeLaPrimera)
    {
        var salida = new Dictionary<int, decimal>();
        var queda = saldoDeLaPrimera;
        if (RepartaLaGracia && queda > 0m)
            for (var j = Cuotas.Count - 1; j >= 1; j--)
            {
                var porcion = Math.Min(Cuotas[j].InteresDeGracia, queda);
                if (porcion <= 0m) continue;
                salida[Cuotas[j].Numero] = porcion;
                queda -= porcion;
            }
        salida[Cuotas[0].Numero] = queda;
        return salida;
    }
}
