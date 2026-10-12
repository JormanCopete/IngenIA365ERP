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

    /// <summary>La mora relativa: factor × la corriente pactada o reajustada, antes de topar.</summary>
    public decimal? FactorDeMora { get; init; }

    /// <summary>La sobretasa de cargos sellada al desembolsar (T28).</summary>
    public decimal SobretasaDeCargos { get; init; }

    /// <summary><c>Mora.SobreIntereses</c> sellado: la mora de una cuota vencida también corre sobre su corriente (nunca en vivienda).</summary>
    public bool MoraSobreIntereses { get; init; }

    public InsuranceAccrualTiming CausacionDeSeguros { get; init; } = InsuranceAccrualTiming.AtDueDate;

    /// <summary>Tasa variable (D10): margen en la expresión del índice, piso y techo en EA.</summary>
    public decimal Margen { get; init; }

    public decimal? Piso { get; init; }

    public decimal? Techo { get; init; }

    /// <summary>Las reestructuraciones (más de una suspende, T30).</summary>
    public int Reestructuraciones { get; init; }

    public DateOnly? InsolvenciaDesde { get; init; }

    public DateOnly? CastigoDesde { get; init; }

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
}
