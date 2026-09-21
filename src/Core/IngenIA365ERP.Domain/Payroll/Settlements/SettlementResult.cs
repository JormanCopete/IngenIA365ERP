using IngenIA365ERP.Domain.Payroll.Calculation;

namespace IngenIA365ERP.Domain.Payroll.Settlements;

/// <summary>
/// Banderas del empleado en una liquidación especial. Los cinco primeros bits son los de
/// <c>RunEmployeeFlag</c> con los mismos valores; los dos últimos son los que la feature
/// 010 le suma a ese enum (data-model §1.2). Se declaran aquí para que el motor compile
/// solo; cuando el enum de la corrida tenga los dos bits, este tipo se convierte a él.
/// </summary>
[Flags]
public enum SettlementFlags
{
    None = 0,
    NegativeNet = 1,
    DeductionsOverMax = 2,
    MissingAffiliation = 4,
    ConceptWithoutAccounts = 8,
    WithholdingRateMissing = 16,

    /// <summary>Ingreso anterior al arranque de la nómina sin saldo inicial digitado (FR-007, edge case de la spec).</summary>
    OpeningBalanceMissing = 32,

    /// <summary>Los descuentos aplicados superan el neto antes de descuentos (FR-018a).</summary>
    DeductionOverNet = 64,
}

/// <summary>
/// Por qué un empleado o un rubro queda fuera. Son códigos estables para la pantalla y las
/// pruebas; el texto en español lo acompaña en <see cref="SettlementSkip.Text"/>.
/// </summary>
public static class SettlementReasonCodes
{
    public const string SalarioIntegral = "SalarioIntegral";
    public const string AprendizLectiva = "AprendizLectiva";
    public const string Pasante = "Pasante";
    public const string YaPagadaEnDefinitiva = "YaPagadaEnDefinitiva";
    public const string SinDiasEnElSemestre = "SinDiasEnElSemestre";
    public const string SinDiasEnElAnio = "SinDiasEnElAnio";
    public const string SinDiasPendientes = "SinDiasPendientes";
    public const string MotivoNoGeneraIndemnizacion = "MotivoNoGeneraIndemnizacion";
    public const string SinProvisionInformada = "SinProvisionInformada";
    public const string ConceptoSinVersionVigente = "ConceptoSinVersionVigente";
    public const string LoPagaLaNominaOrdinaria = "LoPagaLaNominaOrdinaria";
    public const string SinSaldoDeVacaciones = "SinSaldoDeVacaciones";
    public const string CompensacionExcedeMaximo = "CompensacionExcedeMaximo";
    public const string NoCotizaSobreEstaLiquidacion = "NoCotizaSobreEstaLiquidacion";

    /// <summary>D-28: la prima del semestre ya la pagó la corrida semestral aprobada antes de registrar el retiro; la definitiva no la repite.</summary>
    public const string YaPagadaEnCorridaSemestral = "YaPagadaEnCorridaSemestral";

    /// <summary>D-28: las cesantías del año ya las pagó la corrida anual aprobada hasta su corte y no quedan días después de él.</summary>
    public const string YaPagadaEnCorridaAnual = "YaPagadaEnCorridaAnual";

    /// <summary>D-28: una novedad del período pendiente cuya forma de cálculo necesita las bases de la nómina ordinaria (porcentaje sobre base, compuesto, tabla) y la definitiva no puede liquidar.</summary>
    public const string NovedadNoLiquidable = "NovedadNoLiquidable";
}

/// <summary>Un rubro que no produjo línea, con su código de motivo y el texto para la contadora.</summary>
public sealed record SettlementSkip(string ConceptCode, string ReasonCode, string Text);

/// <summary>Cómo quedaron las vacaciones al corte: causadas, consumidas y pendientes, en días hábiles.</summary>
public sealed record VacationBalance(decimal Accrued, decimal OpeningBalanceDays, decimal Consumed, decimal Pending)
{
    public static readonly VacationBalance Empty = new(0m, 0m, 0m, 0m);
}

/// <summary>
/// El resultado de una liquidación especial para un empleado: sus líneas explicadas, los
/// totales, las banderas, lo que se omitió y por qué, y las advertencias que la pantalla
/// muestra sin bloquear. Las líneas son <see cref="CalculationLine"/>, las mismas de la
/// nómina ordinaria, para que se vuelvan <c>PayrollRunLine</c> sin traducción (research R2).
/// </summary>
public sealed class SettlementResult
{
    public required Guid EmployeePublicId { get; init; }
    public required SettlementKind Kind { get; init; }
    public required DateTime CutoffDate { get; init; }

    /// <summary>Verdadero cuando el empleado no tiene derecho a esta liquidación y no hay ninguna línea.</summary>
    public bool Excluded => ExclusionReasonCode is not null;

    /// <summary>Código de <see cref="SettlementReasonCodes"/> cuando el empleado queda fuera.</summary>
    public string? ExclusionReasonCode { get; init; }
    public string? ExclusionReason { get; init; }

    public required IReadOnlyList<CalculationLine> Lines { get; init; }
    public required CalculationTotals Totals { get; init; }
    public required SettlementFlags Flags { get; init; }

    /// <summary>Rubros que no produjeron línea y por qué (no aplica, sin derecho, sin provisión).</summary>
    public IReadOnlyList<SettlementSkip> Skips { get; init; } = [];

    /// <summary>Lo que la responsable debe mirar sin que bloquee la aprobación (saldo inicial ausente, Cartera sin respuesta…).</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>Lo que el motor se negó a calcular, nombrando el motivo. Con negativas no se aprueba.</summary>
    public IReadOnlyList<string> Refusals { get; init; } = [];

    /// <summary>Cómo se formaron las bases y los días, paso a paso, para el detalle del empleado.</summary>
    public IReadOnlyList<ExplanationStep> BaseSteps { get; init; } = [];

    public VacationBalance Vacations { get; init; } = VacationBalance.Empty;

    public required string InputsHash { get; init; }

    public bool HasBlockers => Flags != SettlementFlags.None || Refusals.Count > 0;
}
