using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Common.Integration.Accounting;

/// <summary>
/// Lo único de Contabilidad que Cartera conoce (feature 013, T59, T90, C106; FR-012, FR-041, FR-047, FR-107, FR-127, FR-130, FR-141,
/// FR-142; contracts/contabilidad.md §16.1; interfaces.md §9.1). Declara <b>exactamente</b> estos cinco métodos y su firma es la
/// definitiva: US7 cambia la implementación, no la firma. Hasta US7 la registrada es <see cref="ContabilidadParaCarteraProvisional"/>;
/// US7 la reemplaza por el adaptador real <c>ContabilidadParaCartera</c> (<c>Application/Accounting/Lending</c>), que corre en proceso
/// y delega en las consultas de la 009. El puerto no pide permisos: lo usa Cartera dentro de una operación ya autorizada. (nuevo)
/// </summary>
public interface IContabilidadParaCartera
{
    /// <summary>FR-127: ¿las líneas que generarían estos hechos tienen regla y pasan las reglas de la 009?</summary>
    Task<Result<ResultadoDeEvaluacionContableDto>> EvaluarAsync(IReadOnlyList<MensajeContableDto> hechos, CancellationToken ct);

    /// <summary>FR-130, FR-141: saldo contable, por conjunto de cuentas, de las combinaciones de Cartera a un corte.</summary>
    Task<Result<SaldosMapeadosDeCarteraDto>> SaldosDeCuentasMapeadasAsync(
        DateOnly corte, IReadOnlyList<CombinacionDeSaldo> combinaciones, CancellationToken ct);

    /// <summary>FR-012, FR-107, FR-142: contabilidad iniciada, estado del mes de la fecha y primer mes abierto desde ella.</summary>
    Task<Result<EstadoDelPeriodoDto>> EstadoDelPeriodoAsync(DateOnly fecha, CancellationToken ct);

    /// <summary>FR-041, FR-100, FR-103: resultado, pérdidas, patrimonio técnico y solvencia a una fecha.</summary>
    Task<Result<IndicadoresDeLaEntidadDto>> IndicadoresDeLaEntidadAsync(DateOnly fecha, CancellationToken ct);

    /// <summary>FR-047: cuentas bancarias del plan, de un banco si se da, para elegir la de origen del archivo de desembolsos.</summary>
    Task<Result<IReadOnlyList<CuentaBancariaDeOrigenDto>>> CuentasBancariasAsync(Guid? bancoPublicId, CancellationToken ct);
}

/// <summary>Lo que respondió la evaluación «¿es contabilizable?» (§13). (nuevo)</summary>
public sealed record ResultadoDeEvaluacionContableDto(
    bool IsPostable,
    IReadOnlyList<HallazgoDeCarteraDto> Errors,
    IReadOnlyList<HallazgoDeCarteraDto> Warnings);

/// <summary>Un hallazgo de la evaluación: el hecho, el rol, la combinación, la cuenta, la regla y quién lo corrige (§13.3). (nuevo)</summary>
public sealed record HallazgoDeCarteraDto(
    string FactType,
    string? Role,
    IReadOnlyDictionary<string, string>? Combination,
    string? AccountCode,
    string Rule,
    string Message,
    QuienCorrigeDto WhoFixes);

/// <summary>Entrada (sin sufijo, §2.1 de las transversales): una combinación de saldo; el hecho es siempre «*». (nuevo)</summary>
public sealed record CombinacionDeSaldo(
    string Role,
    string? CreditLineCode,
    string? ModalityCode,
    string? SubsegmentCode,
    string? RiskCategory,
    string? ConceptCode,
    bool? IsEmployee,
    bool? InGracePeriod,
    bool? OnBehalfOfAssociate,
    bool? Restructured,
    string? CollectorClass,
    string? ReasonCode,
    Guid BranchPublicId,
    string? LineNature = null,
    string? DepositClass = null,
    string? IssueTermBand = null,
    string? ContributionClass = null,
    string? AccountFlag = null);

/// <summary>Los saldos por conjunto de cuentas de las combinaciones pedidas y las que no resolvieron regla (índices de la entrada). (nuevo)</summary>
public sealed record SaldosMapeadosDeCarteraDto(
    IReadOnlyList<ConjuntoDeCuentasDeCarteraDto> Sets,
    IReadOnlyList<int> UnresolvedCombinations);

/// <summary>Un conjunto de cuentas de una familia de roles, con las combinaciones que lo usan (índices de la entrada). (nuevo)</summary>
public sealed record ConjuntoDeCuentasDeCarteraDto(
    string RoleFamily,
    IReadOnlyList<int> Combinations,
    IReadOnlyList<CuentaDelConjuntoDto> Accounts,
    decimal Balance);

/// <summary>El estado de un mes en Contabilidad: abierto, cerrado o inexistente. (nuevo)</summary>
public enum EstadoDePeriodoContable
{
    Open = 1,
    Closed = 2,
    NotFound = 3,
}

/// <summary>
/// Contabilidad iniciada, el estado del mes de la fecha pedida y el primer día del primer mes abierto desde ella (un mes que no existe no
/// se salta: §8). Sin contabilidad iniciada, <see cref="State"/> es <see cref="EstadoDePeriodoContable.NotFound"/> y
/// <see cref="FirstOpenMonthFrom"/> es nulo. (nuevo)
/// </summary>
public sealed record EstadoDelPeriodoDto(bool AccountingInitialized, EstadoDePeriodoContable State, DateOnly? FirstOpenMonthFrom);

/// <summary>Resultado del ejercicio, pérdidas acumuladas, patrimonio técnico y solvencia a una fecha (§15). (nuevo)</summary>
public sealed record IndicadoresDeLaEntidadDto(
    DateOnly Date,
    decimal YearResult,
    decimal AccumulatedLosses,
    IndicadorRegistradoDto? TechnicalCapital,
    IndicadorRegistradoDto? SolvencyRatio);

/// <summary>Un indicador regulatorio registrado en Contabilidad (<c>ACC_RegulatoryIndicators</c>). (nuevo)</summary>
public sealed record IndicadorRegistradoDto(Guid RegulatoryIndicatorPublicId, decimal Value, string Unit, DateOnly CutoffDate, string Source);

/// <summary>Una cuenta bancaria del plan que puede ser origen del archivo de desembolsos (FR-047). (nuevo)</summary>
public sealed record CuentaBancariaDeOrigenDto(
    Guid AccountPublicId,
    string AccountCode,
    string AccountName,
    Guid BankPublicId,
    string BankName,
    string? AccountNumber,
    string? TransferCode);
