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
/// abono resta) y, si cambió, la tasa EA nueva (reajuste). La elección del deudor ante un abono (plazo o cuota) la agrega US5.
/// </summary>
public sealed record EntradaDeRegeneracion(DateOnly Fecha)
{
    public decimal CambioDeCapital { get; init; }

    public decimal? NuevaTasaEfectivaAnual { get; init; }
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
}
