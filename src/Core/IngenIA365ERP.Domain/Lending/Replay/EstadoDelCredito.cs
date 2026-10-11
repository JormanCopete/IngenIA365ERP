using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Accrual;
using IngenIA365ERP.Domain.Lending.Delinquency;
using IngenIA365ERP.Domain.Lending.Rates;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Lending.Replay;

/// <summary>El saldo de un concepto en una cuota (o sin cuota) a la fecha.</summary>
public sealed record SaldoDeCuota(int? Cuota, LoanConcept Concepto, decimal Valor);

/// <summary>Una cuota a la fecha: vencimiento, fecha exigible (art. 829), capital pendiente y si su capital ya venció.</summary>
public sealed record EstadoDeCuota(int Numero, DateOnly Vencimiento, DateOnly FechaExigible, decimal CapitalPendiente, bool Vencida);

/// <summary>Una transición de la causación: el primer día del estado nuevo, cuál y por qué (si se suspendió).</summary>
public sealed record TransicionDeEstado(DateOnly Fecha, TransicionDeCausacion Clase, MotivoDeSuspension? Motivo);

/// <summary>
/// El estado de un crédito a la fecha D con la información hasta R (feature 013, US4, T0488; T24, R32; FR-068, FR-069): lo único de
/// donde salen saldos, cuotas, mora y suspensión. Saldos por cuota y concepto, capital vencido, altura de mora, suspensión, tasa del
/// día, anticipo, saldo a favor, plan, transiciones, el libro teórico y la explicación; <see cref="ValidaHasta"/> si se proyectó con una
/// usura no certificada (D11). La <see cref="Huella"/> es independiente del orden en que llegaron las operaciones y lleva el prefijo de
/// la versión de reglas. (nuevo)
/// </summary>
public sealed class EstadoDelCredito
{
    private static readonly LoanConcept[] Cobrables =
    [
        LoanConcept.Principal, LoanConcept.CurrentInterest, LoanConcept.DefaultInterest, LoanConcept.CurrentInterestInSuspense,
        LoanConcept.DefaultInterestInSuspense, LoanConcept.Insurance, LoanConcept.CollectionFee, LoanConcept.OtherCharge,
    ];

    private readonly Lazy<string> _huella;
    private readonly Func<ExplicacionDeCartera> _explicar;
    private ExplicacionDeCartera? _explicacion;

    internal EstadoDelCredito(
        DateOnly fecha, DateTime informacionHasta, string rulesVersion, DateOnly? origen, PlanDePagos? plan,
        IReadOnlyList<EstadoDeCuota> cuotas, IReadOnlyList<SaldoDeCuota> saldos, ResultadoDeAlturaDeMora altura,
        MotivoDeSuspension? motivo, ResultadoDeTasaDelDia? tasaDelDia, DateOnly? primerDiaSinUsura,
        IReadOnlyList<TransicionDeEstado> transiciones, LibroTeorico libro, Func<ExplicacionDeCartera> explicar)
    {
        Fecha = fecha;
        InformacionHasta = informacionHasta;
        RulesVersion = rulesVersion;
        Origen = origen;
        Plan = plan;
        Cuotas = cuotas;
        Saldos = saldos;
        AlturaDeMora = altura;
        MotivoDeSuspension = motivo;
        TasaDelDia = tasaDelDia;
        PrimerDiaSinUsura = primerDiaSinUsura;
        Transiciones = transiciones;
        Libro = libro;
        _explicar = explicar;
        _huella = new Lazy<string>(CalcularHuella);
    }

    /// <summary>D.</summary>
    public DateOnly Fecha { get; }

    /// <summary>R.</summary>
    public DateTime InformacionHasta { get; }

    public string RulesVersion { get; }

    /// <summary>La fecha valor del primer desembolso o el corte migrado; nula si a D no hay crédito.</summary>
    public DateOnly? Origen { get; }

    public PlanDePagos? Plan { get; }

    public IReadOnlyList<EstadoDeCuota> Cuotas { get; }

    /// <summary>Saldos por cuota y concepto distintos de cero, ordenados por cuota y concepto.</summary>
    public IReadOnlyList<SaldoDeCuota> Saldos { get; }

    public ResultadoDeAlturaDeMora AlturaDeMora { get; }

    public MotivoDeSuspension? MotivoDeSuspension { get; }

    public bool Suspendida => MotivoDeSuspension is not null;

    /// <summary>La tasa del día D, topada; nula si a D no hay crédito.</summary>
    public ResultadoDeTasaDelDia? TasaDelDia { get; }

    public bool UsuraTopada => TasaDelDia?.Topada ?? false;

    /// <summary>El primer día hasta D que se causó sin usura certificada (proyectado con la última conocida).</summary>
    public DateOnly? PrimerDiaSinUsura { get; }

    /// <summary>Hasta cuándo vale la liquidación si algún día se proyectó sin usura certificada (D11): la víspera de ese día.</summary>
    public DateOnly? ValidaHasta => PrimerDiaSinUsura?.AddDays(-1);

    public IReadOnlyList<TransicionDeEstado> Transiciones { get; }

    public LibroTeorico Libro { get; }

    /// <summary>Los tramos de causación (días, tasa del día, base, tope y suspensión) y lo que hizo el motor, en palabras.</summary>
    public ExplicacionDeCartera Explicacion => _explicacion ??= _explicar();

    /// <summary>El saldo de un concepto: de una cuota, o de todas si es nula.</summary>
    public decimal Saldo(LoanConcept concepto, int? cuota = null) =>
        Saldos.Where(s => s.Concepto == concepto && (cuota is null || s.Cuota == cuota)).Sum(s => s.Valor);

    /// <summary>Los saldos por concepto (sólo los distintos de cero), en el orden del enum.</summary>
    public IReadOnlyDictionary<LoanConcept, decimal> SaldosPorConcepto =>
        Saldos.GroupBy(s => s.Concepto).Where(g => g.Sum(s => s.Valor) != 0m).OrderBy(g => (int)g.Key)
            .ToDictionary(g => g.Key, g => g.Sum(s => s.Valor));

    public decimal CapitalVencido => Cuotas.Where(c => c.Vencida).Sum(c => c.CapitalPendiente);

    public decimal Anticipo => Saldo(LoanConcept.Advance);

    public decimal SaldoAFavor => Saldo(LoanConcept.CreditBalance);

    /// <summary>
    /// El valor para cancelar a D (FR-068): todo lo cobrable —capital, corriente, mora, sus suspensos, seguros, gastos y otros cargos—
    /// menos el anticipo del mismo crédito; el saldo a favor no se aplica solo (D7).
    /// </summary>
    public decimal ValorParaCancelar => Cobrables.Sum(c => Saldo(c)) - Anticipo;

    /// <summary>
    /// La huella SHA-256 de la liquidación (T24, M15): el texto canónico de <see cref="HuellaDeCartera"/> con el prefijo
    /// <c>{RulesVersion}|</c>, sobre D, los saldos por cuota y concepto, la altura, la suspensión, el valor para cancelar, hasta cuándo
    /// vale y el plan. Misma entrada (en cualquier orden) y misma D: misma huella.
    /// </summary>
    public string Huella => _huella.Value;

    /// <summary>El texto canónico que se firma (con su prefijo).</summary>
    public string TextoCanonico => $"{RulesVersion}|{Canonico()}";

    private string CalcularHuella() => HuellaDeCartera.DeTexto(TextoCanonico);

    private string Canonico() => new HuellaDeCartera()
        .Fecha("fecha", Fecha)
        .Lista("saldos", Saldos, (h, s) => h.Entero("cuota", s.Cuota).Enumeracion("concepto", (LoanConcept?)s.Concepto).Decimal("valor", s.Valor))
        .Entero("diasDeMora", AlturaDeMora.Dias)
        .Entero("cuotaMasAntigua", AlturaDeMora.CuotaMasAntigua)
        .Enumeracion("suspension", MotivoDeSuspension)
        .Decimal("valorParaCancelar", ValorParaCancelar)
        .Fecha("validaHasta", ValidaHasta)
        .Lista("plan", Plan?.Cuotas ?? [], (h, c) => h.Entero("numero", c.Numero).Fecha("vencimiento", c.Vencimiento)
            .Decimal("capital", c.Capital).Decimal("interes", c.Interes).Decimal("seguro", c.Seguro))
        .Canonico();
}
