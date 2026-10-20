namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 014, US7 (T185; contracts/api.md §4 de la 014; FR-024, FR-074, FR-075): el ahorro contractual del lado del cliente. Las tasas viajan como fracción
// (0,06 = 6 % EA) y los enums salen de la API como número. (nuevo)

/// <summary>El contrato que se pide al abrir un ahorro contractual (<c>OpenAccountInput.contractual</c>): la cuota y el plazo en meses; el resto lo pone la línea.</summary>
public sealed class ContratoDeAhorroRequest
{
    public decimal Installment { get; set; }
    public int TermMonths { get; set; } = 12;
    public InstruccionDeContratoRequest? MaturityInstruction { get; set; }
}

/// <summary>La instrucción al vencimiento del contractual: 1 trasladar a una cuenta a la vista del titular, 2 girar (al titular si no se nombra otro).</summary>
public sealed record InstruccionDeContratoRequest(int Kind, Guid? TargetAccountPublicId = null, BeneficiarioDeGiroRequest? Payee = null);

/// <summary>El cuerpo de <c>PUT /api/lending/accounts/{id}/maturity-instruction</c> (api.md §4): la instrucción, plana, y el motivo.</summary>
public sealed record CambiarInstruccionDeContratoRequest(int Kind, Guid? TargetAccountPublicId, BeneficiarioDeGiroRequest? Payee, string Reason);

/// <summary>El contrato en la ficha de la cuenta (<c>MemberAccountDto.contractual</c>).</summary>
public sealed record ResumenDeContratoDto(decimal Installment, int Periodicity, int Installments, int PaidInstallments, int ArrearsInstallments, int Status,
    DateOnly? MaturityDate);

/// <summary>Una versión de las condiciones selladas del contrato: la de la apertura, la del incumplimiento o la migrada.</summary>
public sealed record CondicionesDelContratoDto(
    int Version,
    int Cause,
    DateOnly EffectiveDate,
    decimal Installment,
    int Periodicity,
    int Installments,
    int TermMonths,
    DateOnly StartDate,
    DateOnly MaturityDate,
    decimal BaseRate,
    decimal AppliedRate,
    decimal? BonusRate,
    decimal? BreachRate,
    int? ToleratedLateInstallments,
    int? BreachEffect,
    int? BonusRecognition,
    int? IssueTermBand,
    decimal CommittedAmount,
    decimal? ProjectedFinalAmount,
    int? MigratedPaidInstallments,
    string RulesVersion);

/// <summary>Una cuota del plan con su estado.</summary>
public sealed record CuotaDelContratoDto(int Number, DateOnly DueDate, decimal Amount, decimal Paid, decimal Pending, string Status, bool Late,
    Guid? InstallmentPublicId);

/// <summary>El cumplimiento a hoy.</summary>
public sealed record CumplimientoDelContratoDto(int Generated, int Paid, int Late, IReadOnlyList<int> LateNumbers, decimal LateAmount, int Tolerance,
    int GraceDays, bool ExceedsTolerance, bool Complete);

/// <summary>Lo que lleva el contrato y lo proyectado (FR-024).</summary>
public sealed record ProyeccionDelContratoDto(decimal ContributionsToDate, decimal InterestAtBaseToDate, decimal? BonusToDate, decimal ValueAtMaturityAtBase,
    decimal? ValueAtMaturityIfFulfilled, decimal? ProjectedBonus, decimal ProvisionedBonus, string Explanation);

/// <summary>La instrucción al vencimiento que rige.</summary>
public sealed record InstruccionDelContratoDto(int Kind, Guid? TargetAccountPublicId, string? TargetAccountNumber, PersonaDeCuentaDto? Payee, string? Reason,
    DateTime? GivenAt);

/// <summary>El ahorro contractual (<c>GET /api/lending/accounts/{id}/contractual</c>).</summary>
public sealed record ContratoDeAhorroDto(
    Guid AccountPublicId,
    string AccountNumber,
    int AccountStatus,
    int Status,
    CondicionesDelContratoDto Terms,
    IReadOnlyList<CondicionesDelContratoDto> History,
    IReadOnlyList<CuotaDelContratoDto> Installments,
    CumplimientoDelContratoDto Compliance,
    ProyeccionDelContratoDto Projection,
    InstruccionDelContratoDto? Instruction,
    DateOnly? MaturedOn,
    int AllowedMaturityDestinations = 3);

/// <summary>Los textos del ahorro contractual.</summary>
public static class TextosDeContrato
{
    public const int Vigente = 1;
    public const int Incumplido = 2;
    public const int Cumplido = 3;
    public const int PorTraslado = 1;
    public const int PorGiro = 2;

    /// <summary>Los estados del contrato (<c>ContractualStatus</c>).</summary>
    public static IReadOnlyList<(int Valor, string Texto)> Estados { get; } = [(Vigente, "Vigente"), (Incumplido, "Incumplido"), (Cumplido, "Cumplido")];

    /// <summary>Los efectos del incumplimiento de la línea (<c>ContractualBreachEffect</c>).</summary>
    public static IReadOnlyList<(int Valor, string Texto)> Efectos { get; } =
        [(1, "Sigue a la tasa de incumplimiento"), (2, "Pasa a la cuenta a la vista"), (3, "Se cancela y se entrega")];

    /// <summary>Cuándo se reconoce la bonificación (<c>BonusRecognition</c>).</summary>
    public static IReadOnlyList<(int Valor, string Texto)> Reconocimientos { get; } = [(1, "Al cumplir"), (2, "Provisionada en cada cuota")];

    /// <summary>Por qué cambió el contrato (<c>AccountTermsCause</c>, las que tocan al contractual).</summary>
    public static IReadOnlyList<(int Valor, string Texto)> Causas { get; } = [(4, "Migración"), (5, "Apertura del contrato"), (6, "Incumplimiento")];

    public static string Estado(int v) => De(Estados, v);
    public static string Efecto(int? v) => v is { } x ? De(Efectos, x) : "—";
    public static string Reconocimiento(int? v) => v is { } x ? De(Reconocimientos, x) : "—";
    public static string Causa(int v) => De(Causas, v);

    /// <summary>El estado de una cuota del plan como lo manda la API (<c>NotGenerated</c> o el de la cuota: <c>Pending</c>, <c>PartiallyPaid</c>…).</summary>
    public static string EstadoDeCuota(string v, bool atrasada) => v switch
    {
        "NotGenerated" => "Por generar",
        "Paid" => "Pagada",
        "Cancelled" => "Ya no se exige",
        _ when atrasada => "Atrasada",
        "Pending" => "Pendiente",
        "PartiallyPaid" => "Pagada en parte",
        "NotDeducted" => "No descontada",
        _ => v,
    };

    /// <summary>Una tasa en fracción como porcentaje EA.</summary>
    public static string Tasa(decimal? v) => v is { } x ? (x * 100m).ToString("0.####", System.Globalization.CultureInfo.GetCultureInfo("es-CO")) + " % EA" : "—";

    private static string De(IReadOnlyList<(int Valor, string Texto)> lista, int v) => lista.FirstOrDefault(x => x.Valor == v).Texto ?? v.ToString();
}

/// <summary>Una versión de condiciones selladas de una cuenta (<c>GET /api/lending/accounts/{id}/terms</c>), de un CDAT o de un contractual.</summary>
public sealed record CondicionesSelladasDto(
    int Version,
    int Cause,
    DateOnly EffectiveDate,
    DateTime RegisteredAt,
    decimal PrincipalAmount,
    int? TermDays,
    int? TermMonths,
    DateOnly? StartDate,
    DateOnly? MaturityDate,
    decimal TableRate,
    decimal? NegotiatedSpread,
    decimal AppliedRate,
    int? IssueTermBand,
    decimal? Installment,
    int? InstallmentPeriodicity,
    int? Installments,
    decimal? BonusRate,
    decimal? BreachRate,
    string RulesVersion);
