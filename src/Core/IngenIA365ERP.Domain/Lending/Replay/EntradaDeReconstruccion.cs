using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Rates;
using IngenIA365ERP.Domain.Lending.Schedules;

namespace IngenIA365ERP.Domain.Lending.Replay;

// Feature 013, US4 (T0488; T24, R32, M3): lo que recibe ReconstruccionDeCredito. Lo arma CargadorDeReconstruccion (Application, 8B)
// en bloque con lo conocido en R; el motor vuelve a filtrar por R las operaciones y la usura, así una entrada con «de más» da lo mismo.
// No son tablas. (nuevo)

/// <summary>
/// Las condiciones selladas del crédito que usa la reconstrucción (data-model §4.3, <c>LoanTermsVersion</c>): su versión de reglas,
/// la corriente pactada (EA), la mora, la convención, la equivalencia, la periodicidad (para la equivalencia lineal) y el redondeo; lo
/// demás con su defecto. Lo regulatorio (usura, umbral de suspensión, tolerancia, festivos) no se sella: llega en series.
/// </summary>
public sealed record CondicionesDeCredito(
    string RulesVersion,
    decimal TasaEfectivaAnual,
    DefaultRateKind ClaseDeMora,
    DayCountConvention Convencion,
    RateEquivalence Equivalencia,
    RepaymentPeriodicity Periodicidad,
    RedondeoDeCartera Redondeo)
{
    public decimal? MoraFijaEA { get; init; }

    /// <summary>
    /// 2026-11-24 (feature 025, HU2): la corriente pactada en mensual vencida (<c>LoanTermsVersion.MonthlyRate</c>); nula = en E.A. Con valor manda
    /// ella y <see cref="CondicionesDeCredito.TasaEfectivaAnual"/> es la E.A. derivada (informativa). (nuevo)
    /// </summary>
    public decimal? TasaMensual { get; init; }

    /// <summary>La mora relativa: factor × la corriente pactada o reajustada, antes de topar.</summary>
    public decimal? FactorDeMora { get; init; }

    /// <summary>La sobretasa de cargos sellada al desembolsar (T28).</summary>
    public decimal SobretasaDeCargos { get; init; }

    /// <summary><c>Mora.SobreIntereses</c> sellado: la mora de una cuota vencida también corre sobre su corriente (nunca en vivienda).</summary>
    public bool MoraSobreIntereses { get; init; }

    public InsuranceAccrualTiming CausacionDeSeguros { get; init; } = InsuranceAccrualTiming.AtDueDate;

    /// <summary>
    /// 2026-11-16 (pedido del dueño; feature 024, fase 2): cuándo se causa la administración (<see cref="LoanConcept.Administration"/>): el momento del
    /// primer cargo de clase administración de la versión de la línea, por código; al vencer si no tiene. (nuevo)
    /// </summary>
    public InsuranceAccrualTiming CausacionDeAdministracion { get; init; } = InsuranceAccrualTiming.AtDueDate;

    /// <summary>Tasa variable (D10): margen en la expresión del índice, piso y techo en EA.</summary>
    public decimal Margen { get; init; }

    public decimal? Piso { get; init; }

    public decimal? Techo { get; init; }

    /// <summary>Las reestructuraciones (más de una suspende, T30).</summary>
    public int Reestructuraciones { get; init; }

    public DateOnly? InsolvenciaDesde { get; init; }

    /// <summary>
    /// K3 (T0886): el último día de la negociación de insolvencia que terminó —el fracaso (<c>FailedOn</c>) o el acuerdo—; nulo mientras sigue en
    /// negociación. Desde el día siguiente la causación vuelve a la regla de siempre (T30).
    /// </summary>
    public DateOnly? InsolvenciaHasta { get; init; }

    /// <summary>
    /// La fecha del castigo (<c>LND_Loans.WrittenOffOn</c>). Un pago con fecha valor desde ese día es recuperación; la causación se suspende desde
    /// el día siguiente (K3, T0908: como todo hecho con fecha valor v, rige desde v + 1; el día del castigo ya se causó normal).
    /// </summary>
    public DateOnly? CastigoDesde { get; init; }

    /// <summary>
    /// K3 (FR-097, T102): el día del fallecimiento del deudor cuando la causación <b>no</b> sigue tras él (<c>Causacion.ContinuaTrasFallecimiento =
    /// false</c> para la línea, leído a esa fecha); desde ese día se causa en suspenso. Nulo si no falleció o si la causación sigue (el defecto).
    /// </summary>
    public DateOnly? FallecimientoDesde { get; init; }

    /// <summary>Si la modalidad exige usura certificada para causar (sin ella la causación real se bloquea, D11).</summary>
    public bool UsuraRequerida { get; init; } = true;

    /// <summary>La prelación sellada (US5, T0533; data-model §1.8): la misma para simular y aplicar un pago.</summary>
    public Prelacion Prelacion { get; init; } = Prelacion.PorDefecto;
}

/// <summary>Un valor de usura de la modalidad del crédito, tal como se replicó (bitemporal, T38).</summary>
public sealed record ValorDeUsura(DateOnly ValidFrom, decimal Ea, DateTime ConocidaEn, long Sequence, Guid PublicId) : IVersionBitemporal
{
    public DateOnly? ValidTo { get; init; }

    public Guid? SupersedesPublicId { get; init; }
}

/// <summary>Un valor que rige desde una fecha (umbral de suspensión, tolerancia), ya resuelto con lo conocido en R.</summary>
public sealed record ValorConVigencia<T>(DateOnly Desde, T Valor);

/// <summary>
/// 2026-11-16 (pedido del dueño; feature 024, fase 1): una vigencia de la <b>tasa general de mora</b> de la empresa (<c>Mora.TasaGeneral</c>), tal como
/// se registró: desde cuándo rige, la efectiva anual (nula = sin tasa general: rige la mora sellada del crédito), cuándo se supo (R = su registro) y un
/// ordinal que desempata. Sigue la regla de las versiones propias de Cartera (<see cref="VigenciaBitemporal.VigenteDeCartera{T}"/>): rige
/// hasta la víspera de la siguiente conocida en R. Mientras rige reemplaza la mora de <b>todos</b> los créditos —también los ya desembolsados—,
/// siempre topada en la usura del día. El escritor de parámetros no admite vigencias con fecha pasada, así que nunca cambia lo ya causado. (nuevo)
/// </summary>
public sealed record TasaGeneralDeMora(DateOnly ValidFrom, decimal? Ea, DateTime RegisteredAt, int VersionNumber) : IVersionDeCartera;

/// <summary>Un saldo inicial por concepto y, si la carga la trae, por cuota (FR-140).</summary>
public sealed record SaldoInicialDeCredito(LoanConcept Concepto, int? Cuota, decimal Monto);

/// <summary>
/// Una operación del crédito (<c>LND_LoanOperations</c>) con su fecha valor, cuándo se registró (R) y su <c>Sequence</c>. Lo que la
/// reconstrucción necesita de cada clase: el monto del tramo (<c>Disbursement</c>); los saldos y las cuotas cargadas
/// (<c>OpeningBalance</c>); el índice publicado o la tasa ya calculada (<c>RateReset</c>); la operación que reversa
/// (<c>DisbursementReversal</c> y los reversos de US5).
/// </summary>
public sealed record EntradaDeOperacion(string Id, LoanOperationKind Clase, DateOnly FechaValor, DateTime RegistradoEn, long Sequence)
{
    public decimal? Monto { get; init; }

    public IReadOnlyList<SaldoInicialDeCredito> Saldos { get; init; } = [];

    public IReadOnlyList<CuotaMigrada> CuotasMigradas { get; init; } = [];

    public IndiceDeReferencia? Indice { get; init; }

    public decimal? TasaEfectivaAnual { get; init; }

    /// <summary>El <see cref="Id"/> de la operación que esta reversa.</summary>
    public string? Reversa { get; init; }

    /// <summary>
    /// Lo que traiga una clase que suman US5 y US6 (su intérprete sabe leerlo): <see cref="ContenidoDePago"/> en <c>Payment</c> y
    /// <c>ProvisionalCollection</c>, <see cref="ContenidoDeResolucion"/> en <c>CreditBalanceResolution</c>.
    /// </summary>
    public object? Contenido { get; init; }
}

/// <summary>
/// Lo que la pagaduría descontó de una cuota y todavía no giró (feature 013, K2, T0826; T30, FR-114; data-model §24.8): un resultado
/// <c>Deducted</c> o <c>PartiallyDeducted</c> de <c>LND_PayrollDeductionResults</c>, no reemplazado ni girado, con la fecha del descuento, el
/// vencimiento de la ventana de giro y el umbral de mora de la pagaduría <b>sellados</b> al recibirlo, y cuándo se supo (R = su registro). Lo
/// carga <c>CargadorDeReconstruccion</c> (T0839). Mientras la mora de la pagaduría no supere el umbral, la cuota no cuenta para la altura de
/// mora por lo descontado ni causa mora sobre ello; superado, cuenta desde su vencimiento. Umbral nulo: nunca se imputa (C71). (nuevo)
/// </summary>
public sealed record CuotaPendienteDeGiro(int Cuota, DateOnly FechaDelDescuento, DateOnly VencimientoDelGiro, decimal Monto, DateTime ConocidaEn)
{
    public int? UmbralDias { get; init; }

    /// <summary>
    /// La fecha valor del pago que lo giró: desde ese día ya no cubre la cuota. Nula mientras siga pendiente. Girado, lo descontado sigue en la
    /// entrada para los días anteriores: si saliera, la reconstrucción causaría mora que el libro no causó.
    /// </summary>
    public DateOnly? GiradoDesde { get; init; }

    /// <summary>El <see cref="EntradaDeOperacion.Id"/> del pago que lo giró: si la entrada trae su reverso, vuelve a estar pendiente.</summary>
    public string? GiradoPor { get; init; }
}

/// <summary>
/// Todo lo que la reconstrucción de un crédito necesita (T24): las condiciones selladas, el plan pactado del primer tramo (para un
/// migrado, el plan sale de su <c>OpeningBalance</c>), las operaciones, R y las series regulatorias de la modalidad (usura, festivos
/// nacionales, umbral de suspensión, tolerancia de saldo vencido).
/// </summary>
public sealed record EntradaDeReconstruccion(
    CondicionesDeCredito Condiciones,
    EntradaDePlan? Plan,
    IReadOnlyList<EntradaDeOperacion> Operaciones,
    DateTime InformacionHasta)
{
    public IReadOnlyList<ValorDeUsura> Usura { get; init; } = [];

    public IReadOnlyList<DateOnly> Festivos { get; init; } = [];

    public IReadOnlyList<ValorConVigencia<int>> UmbralesDeSuspension { get; init; } = [];

    public IReadOnlyList<ValorConVigencia<decimal>> Tolerancias { get; init; } = [];

    /// <summary>
    /// K2 (T0826): lo descontado por la pagaduría y pendiente de giro, por cuota. Sin pendientes la reconstrucción es la de K1 (no cambia la
    /// versión de reglas).
    /// </summary>
    public IReadOnlyList<CuotaPendienteDeGiro> PendientesDeGiro { get; init; } = [];

    /// <summary>
    /// K6b (T1053; FR-024, D18): el tope de tasa de vivienda (<c>TOPE_TASA_VIVIENDA</c>), bitemporal como la usura. Sólo lo mira un crédito con las
    /// reglas de vivienda: el tope del día es el menor entre él y la usura que llegue en <see cref="Usura"/>.
    /// </summary>
    public IReadOnlyList<ValorDeUsura> TopeDeVivienda { get; init; } = [];

    /// <summary>
    /// 2026-11-16 (pedido del dueño; feature 024, fase 1): la serie de la tasa general de mora de la empresa (<c>Mora.TasaGeneral</c>), completa en el
    /// rango. El día que rige una con valor, la mora de este crédito se causa con ella en vez de la sellada (fija o relativa a la corriente), topada en la
    /// usura del día —en vivienda, en el menor entre la usura y el tope de vivienda—; sin vigencia o con una vacía, la sellada. Sus vigencias son cortes
    /// de ventana, como las de la usura. Vacía: la reconstrucción es la de siempre.
    /// </summary>
    public IReadOnlyList<TasaGeneralDeMora> MoraGeneral { get; init; } = [];

    /// <summary>
    /// 2026-11-16 (pedido del dueño; feature 024, fase 4): los reconocimientos de la cuota del período al inicio de su ciclo (las filas
    /// <c>PeriodRecognition</c> del libro, con su R). La reconstrucción los pone en el libro teórico y, día a día, la causación de la cuota los consume
    /// (filas <c>Accrual</c> negativas en el concepto reconocido); al vencer la cuota —o el día que una operación cancela el crédito— devuelve lo que no se
    /// causó (<c>RecognitionRelease</c>). El cálculo de la causación no cambia. Vacía: la reconstrucción es la de siempre.
    /// </summary>
    public IReadOnlyList<ReconocimientoDeCuota> Reconocimientos { get; init; } = [];

    /// <summary>
    /// 2026-11-24 (feature 025, fase 3, HU1): los ajustes del corte de un crédito cargado (las filas <c>CutoverInterestAdjustment</c> del libro, con su R).
    /// La reconstrucción suma cada uno al interés corriente de su cuota el día de su fecha, fuera de la causación; sin ellos, la conciliación de la noche
    /// lo revertiría. Vacía: la reconstrucción es la de siempre. (nuevo)
    /// </summary>
    public IReadOnlyList<AjusteDelCorte> AjustesDelCorte { get; init; } = [];

    /// <summary>
    /// 2026-11-27 (feature 028, D13 ajustada): sólo para la proyección del interés proporcional de la cuota que se deja
    /// (<see cref="ReconstruccionDeCredito.InteresPorCausarSobreSuCapital"/>): desde el día siguiente a <c>Desde</c>, en la base de la corriente sólo entra el
    /// capital de <c>Cuota</c>. Nula —toda reconstrucción real— es la de siempre. (nuevo)
    /// </summary>
    internal (int Cuota, DateOnly Desde)? SoloSuCapital { get; init; }

    /// <summary>
    /// K6b (T1054; FR-024, D18): la serie diaria de la <c>UVR</c> de un crédito <b>en UVR</b>; nula en un crédito en pesos. Con ella, la
    /// reconstrucción lleva plan y saldos en UVR y devuelve el estado valorado en pesos con la UVR del día (<c>Housing.ReconstruccionEnUvr</c>).
    /// Los montos de la entrada (desembolsos, pagos, saldos iniciales, tolerancias) siguen en pesos: la reconstrucción los convierte a su fecha.
    /// </summary>
    public Housing.SerieUvr? Uvr { get; init; }
}
