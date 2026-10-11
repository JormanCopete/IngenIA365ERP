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
    public decimal TotalCapital => Cuotas.Sum(c => c.Capital);

    public decimal TotalInteres => Cuotas.Sum(c => c.Interes);

    public decimal TotalSeguro => Cuotas.Sum(c => c.Seguro);

    public decimal TotalAPagar => Cuotas.Sum(c => c.Total);
}
