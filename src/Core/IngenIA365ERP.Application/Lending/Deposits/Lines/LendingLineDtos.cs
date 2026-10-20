using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Application.Lending.Deposits.Lines;

// Feature 014, US1 (T065, T066; contracts/api.md §3): lo que entra y sale de /api/lending/lines. Los nombres de bloque son los del contrato
// (DepositLineVersionInput, ContributionLineVersionInput); dentro, los de las columnas de data-model §2.1, §2.2 y §2.5 (tasas como fracción, montos en
// pesos, plazos del CDAT en días y del contractual en meses). Los enums viajan por nombre o por número. Las líneas que nombra una versión (la a la vista
// destino de un CDAT, la del ahorro permanente de los aportes) viajan por PublicId. (nuevo)

/// <summary>Un tramo de tasa (data-model §2.2): por saldo, o por plazo en días y monto en el CDAT; su efectiva anual (fija) o su margen (variable).</summary>
public sealed record DepositRateTierInput(
    decimal? BalanceFrom = null,
    decimal? BalanceTo = null,
    int? TermDaysFrom = null,
    int? TermDaysTo = null,
    decimal? AmountFrom = null,
    decimal? AmountTo = null,
    decimal? EffectiveAnnual = null,
    decimal? Margin = null);

/// <summary>La tasa: fija (efectiva anual o tramos) o variable sobre un índice con margen (como en el crédito), y el modo de los tramos.</summary>
public sealed record DepositRateInput
{
    public RateKind Kind { get; init; } = RateKind.Fixed;
    public decimal? EffectiveAnnual { get; init; }
    public VariableRateInput? Variable { get; init; }
    public RateTierMode? TierMode { get; init; }
    public IReadOnlyList<DepositRateTierInput> Tiers { get; init; } = [];
}

/// <summary>
/// US4 (T114; data-model §2.3; FR-128): las exoneraciones combinables de un cargo —cualquiera exonera—: saldo promedio o mínimo, antigüedad como asociado,
/// empleados, la cuenta marcada exenta del GMF, menores y oficinas.
/// </summary>
public sealed record DepositChargeExemptionsInput(
    decimal? AverageBalanceAtLeast = null,
    decimal? MinimumBalanceAtLeast = null,
    int? SeniorityMonthsAtLeast = null,
    bool Employees = false,
    bool GmfExemptAccount = false,
    bool Minors = false,
    IReadOnlyList<Guid>? OfficePublicIds = null);

/// <summary>
/// US4 (T114; api.md §3 <c>charges[]</c>; data-model §2.3): un cargo del catálogo de la versión, <b>apagado</b> salvo <see cref="Enabled"/> (D35). El
/// disparador (<see cref="Trigger"/>) reemplaza al <c>event</c> del ejemplo del contrato (research §12).
/// </summary>
public sealed record DepositChargeInput
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public ChargeKind Kind { get; init; } = ChargeKind.Other;
    public ChargeTrigger Trigger { get; init; } = ChargeTrigger.Manual;
    public bool Enabled { get; init; }
    public ChargeCalculation Calculation { get; init; } = ChargeCalculation.FixedAmount;
    public decimal? FixedAmount { get; init; }
    public decimal? Percent { get; init; }
    public ChargeBase? PercentBase { get; init; }
    public decimal? Min { get; init; }
    public decimal? Max { get; init; }
    public RepaymentPeriodicity? Periodicity { get; init; }
    public int? FreePerPeriod { get; init; }
    public RepaymentPeriodicity? FreePeriod { get; init; }
    public bool TaxedWithVat { get; init; }
    public string? CommercialItemCode { get; init; }
    public ChargeShortfallPolicy UncollectedPolicy { get; init; } = ChargeShortfallPolicy.KeepPending;
    public DepositChargeExemptionsInput? Exemptions { get; init; }

    public BorradorDeCargoDeCaptacion Borrador() => new(Code ?? string.Empty, Name ?? string.Empty, Kind, Trigger, Calculation)
    {
        IsEnabled = Enabled,
        Periodicity = Periodicity,
        FreeOperationsPerPeriod = FreePerPeriod,
        FreePeriod = FreePeriod,
        FixedAmount = FixedAmount,
        Percentage = Percent,
        PercentageBase = PercentBase,
        MinAmount = Min,
        MaxAmount = Max,
        ExemptIfAverageBalanceAtLeast = Exemptions?.AverageBalanceAtLeast,
        ExemptIfMinimumBalanceAtLeast = Exemptions?.MinimumBalanceAtLeast,
        ExemptIfSeniorityMonthsAtLeast = Exemptions?.SeniorityMonthsAtLeast,
        ExemptEmployees = Exemptions?.Employees ?? false,
        ExemptGmfExemptAccount = Exemptions?.GmfExemptAccount ?? false,
        ExemptMinors = Exemptions?.Minors ?? false,
        ExemptBranchPublicIds = Exemptions?.OfficePublicIds ?? [],
        ShortfallPolicy = UncollectedPolicy,
        IsVatTaxed = TaxedWithVat,
        CommercialItemCode = CommercialItemCode,
    };

    public static DepositChargeInput De(DepositLineCharge c) => new()
    {
        Code = c.Code, Name = c.Name, Kind = c.Kind, Trigger = c.Trigger, Enabled = c.IsEnabled, Calculation = c.CalculationMode, FixedAmount = c.FixedAmount,
        Percent = c.Percentage, PercentBase = c.PercentageBase, Min = c.MinAmount, Max = c.MaxAmount, Periodicity = c.Periodicity,
        FreePerPeriod = c.FreeOperationsPerPeriod, FreePeriod = c.FreePeriod, TaxedWithVat = c.IsVatTaxed, CommercialItemCode = c.CommercialItemCode,
        UncollectedPolicy = c.ShortfallPolicy,
        Exemptions = new DepositChargeExemptionsInput(c.ExemptIfAverageBalanceAtLeast, c.ExemptIfMinimumBalanceAtLeast, c.ExemptIfSeniorityMonthsAtLeast,
            c.ExemptEmployees, c.ExemptGmfExemptAccount, c.ExemptMinors, c.LeerOficinasExoneradas()),
    };
}

/// <summary>Los mínimos para abrir, mantener (entra al disponible) y ganar interés.</summary>
public sealed record DepositMinimumsInput(decimal ToOpen = 0m, decimal ToMaintain = 0m, decimal ToEarnInterest = 0m);

/// <summary>El redondeo de la liquidación: por defecto al peso, al liquidar (D9).</summary>
public sealed record DepositRoundingInput(decimal Unit = 1m, RoundingMode Mode = RoundingMode.AwayFromZero, RoundingMoment Moment = RoundingMoment.PerTotal);

/// <summary>Retención (la clave de su tarifa; vacía, la de la clase) y GMF.</summary>
public sealed record DepositTaxInput(string? WithholdingRateParameterCode = null, bool GmfApplies = true);

public sealed record DepositLimitsInput(
    decimal? MaxWithdrawalPerOperation = null,
    decimal? MaxWithdrawalPerDay = null,
    decimal? MaxCashPerOperation = null,
    decimal? MaxCashPerDay = null);

/// <summary>Inactividad (D21: 360 días por defecto; nulo = no se inactiva) y suspensión de la causación.</summary>
public sealed record DepositInactivityInput(
    int? Days = 360,
    bool KeepsAccruing = true,
    bool SuspendsOnTotalGarnishment = false,
    bool SuspendsOnDeceased = false);

/// <summary>La redención anticipada del CDAT (D18): si se admite, su penalidad y los días mínimos.</summary>
public sealed record EarlyRedemptionInput(
    bool Allowed = false,
    EarlyRedemptionPenaltyMode? PenaltyMode = null,
    decimal? PenaltyRateEa = null,
    decimal? PenaltyDiscount = null,
    int? MinDaysBeforeRedemption = null);

/// <summary>Las condiciones del CDAT (FR-028; D17): plazos en días, intereses, prórroga, gracia, vencimiento y redención.</summary>
public sealed record TermDepositLineInput
{
    public int MinTermDays { get; init; }
    public int MaxTermDays { get; init; }
    public IReadOnlyList<RepaymentPeriodicity> InterestPeriodicities { get; init; } = [RepaymentPeriodicity.SinglePayment];
    public IReadOnlyList<InterestTiming> Timings { get; init; } = [InterestTiming.Arrears];
    public bool RolloverAutomatic { get; init; } = true;
    public int? MaxRollovers { get; init; }
    public RolloverRateRule RolloverRateRule { get; init; } = RolloverRateRule.CurrentTableRate;
    public bool RolloverCapitalizes { get; init; }
    public int GraceBusinessDays { get; init; } = 3;
    public bool GraceAccrues { get; init; }
    public AfterMaturityTreatment AfterMaturity { get; init; } = AfterMaturityTreatment.StopAccruing;
    public Guid? DemandLinePublicId { get; init; }
    public EarlyRedemptionInput EarlyRedemption { get; init; } = new();
}

/// <summary>Las condiciones del ahorro contractual (FR-024, FR-074, FR-075): cuota, plazo en meses, bonificación e incumplimiento.</summary>
public sealed record ContractualLineInput
{
    public RepaymentPeriodicity InstallmentPeriodicity { get; init; } = RepaymentPeriodicity.Monthly;
    public decimal MinInstallment { get; init; }
    public int TermMonthsFrom { get; init; }
    public int TermMonthsTo { get; init; }
    public decimal? BonusRateEa { get; init; }
    public decimal? BreachRateEa { get; init; }
    public int? ToleranceInstallments { get; init; }
    public ContractualBreachEffect BreachEffect { get; init; } = ContractualBreachEffect.MoveToDemand;
    public BonusRecognition BonusRecognition { get; init; } = BonusRecognition.AtFulfillment;

    /// <summary>A dónde puede ir lo ahorrado al vencer (E248): lo que la línea admite; el asociado elige entre ello en su contrato. Ninguno = queda a disposición.</summary>
    public ContractualMaturityDestinations MaturityDestinations { get; init; } = ContractualMaturityDestinations.All;
}

/// <summary>El ahorro permanente: por defecto sólo se retira al retiro del asociado.</summary>
public sealed record PermanentLineInput(bool WithdrawableOnlyOnMembershipWithdrawal = true);

/// <summary>
/// Una versión de una línea de captación (api.md §3 <c>DepositLineVersionInput</c>; data-model §2.1). <see cref="DepositClass"/> va sólo al versionar
/// y, si viene, tiene que ser la de la línea (la clase no cambia); al crear, la clase es la del alta.
/// </summary>
public sealed record DepositLineVersionInput
{
    public DateOnly ValidFrom { get; init; }
    public string Reason { get; init; } = string.Empty;
    public DepositClass? DepositClass { get; init; }
    public IReadOnlyList<Guid>? OfficePublicIds { get; init; }
    public bool RequiresMembership { get; init; } = true;
    public bool AllowsCoHolders { get; init; }
    public bool AllowsMinors { get; init; }
    public bool AllowsLegalEntities { get; init; }
    public bool AllowsPartialWithdrawals { get; init; }
    public DepositRateInput Rate { get; init; } = new();
    public bool DeclaresAgreedRate { get; init; }
    public bool NegotiatedRateAllowed { get; init; }
    public bool KeepsNegotiatedMarginOnRollover { get; init; }
    public int RateChangeNoticeDays { get; init; }
    public BalanceMethod BalanceMethod { get; init; } = BalanceMethod.DailyBalance;
    public DepositMinimumsInput Minimums { get; init; } = new();
    public DayCountConvention DayCount { get; init; } = DayCountConvention.Actual365;
    public RateEquivalence RateEquivalence { get; init; } = RateEquivalence.Compound;
    public DepositRoundingInput Rounding { get; init; } = new();
    public RepaymentPeriodicity SettlementPeriodicity { get; init; } = RepaymentPeriodicity.Monthly;
    public byte? SettlementDay { get; init; }
    public DepositTaxInput Tax { get; init; } = new();
    public DepositLimitsInput Limits { get; init; } = new();
    public DepositInactivityInput Inactivity { get; init; } = new();
    public TermDepositLineInput? TermDeposit { get; init; }
    public ContractualLineInput? Contractual { get; init; }
    public PermanentLineInput? Permanent { get; init; }

    /// <summary>US4 (T114): el catálogo de cargos de la versión; vacío = sin cargos (D35).</summary>
    public IReadOnlyList<DepositChargeInput>? Charges { get; init; }

    /// <summary>
    /// El borrador del dominio para la clase <paramref name="clase"/>. <paramref name="lineaALaVistaId"/> es el Id interno de la línea a la vista
    /// destino del CDAT (la resolvió quien llama); <paramref name="soloAsociados"/> fuerza «exige asociado» (perfil que capta sólo de asociados).
    /// </summary>
    public BorradorDeVersionDeCaptacion Borrador(DepositClass clase, int? lineaALaVistaId, bool soloAsociados)
    {
        var tasa = Rate ?? new DepositRateInput();
        var cdat = TermDeposit;
        var contractual = Contractual;
        var redencion = cdat?.EarlyRedemption;
        return new BorradorDeVersionDeCaptacion(ValidFrom, Reason ?? string.Empty, clase)
        {
            RequiresAssociate = RequiresMembership || soloAsociados,
            AllowsCoHolders = AllowsCoHolders,
            AllowsMinors = AllowsMinors,
            AllowsLegalEntities = AllowsLegalEntities,
            AllowsPartialWithdrawals = AllowsPartialWithdrawals,
            BranchPublicIds = OfficePublicIds ?? [],
            RateKind = tasa.Kind,
            AnnualRate = tasa.Kind == RateKind.Fixed ? tasa.EffectiveAnnual : null,
            IndexCode = tasa.Variable?.IndexCode,
            IndexKey = tasa.Variable?.IndexKey,
            Margin = tasa.Variable is { } variable && (variable.Margin != 0m || tasa.Tiers.Count == 0) ? variable.Margin : null,
            RateFloor = tasa.Variable?.Floor,
            RateCeiling = tasa.Variable?.Ceiling,
            ResetPeriodicity = tasa.Variable?.ResetPeriodicity,
            RateTierMode = tasa.Tiers.Count == 0 ? null : tasa.TierMode ?? RateTierMode.WholeBalance,
            DeclaresAgreedRate = DeclaresAgreedRate,
            AllowsNegotiatedRate = NegotiatedRateAllowed,
            KeepsNegotiatedMarginOnRollover = KeepsNegotiatedMarginOnRollover,
            RateChangeNoticeDays = RateChangeNoticeDays,
            BalanceMethod = BalanceMethod,
            DayCount = DayCount,
            RateEquivalence = RateEquivalence,
            RoundingUnit = (Rounding ?? new()).Unit,
            RoundingMode = (Rounding ?? new()).Mode,
            RoundingMoment = (Rounding ?? new()).Moment,
            MinOpeningAmount = (Minimums ?? new()).ToOpen,
            MinBalance = (Minimums ?? new()).ToMaintain,
            MinBalanceForInterest = (Minimums ?? new()).ToEarnInterest,
            SettlementPeriodicity = SettlementPeriodicity,
            SettlementDay = SettlementDay,
            WithholdingRateParameterCode = string.IsNullOrWhiteSpace(Tax?.WithholdingRateParameterCode) ? null : Tax.WithholdingRateParameterCode.Trim(),
            IsGmfSubject = Tax?.GmfApplies ?? true,
            MaxWithdrawalPerOperation = Limits?.MaxWithdrawalPerOperation,
            MaxWithdrawalPerDay = Limits?.MaxWithdrawalPerDay,
            MaxCashPerOperation = Limits?.MaxCashPerOperation,
            MaxCashPerDay = Limits?.MaxCashPerDay,
            InactivityDays = Inactivity?.Days,
            AccruesWhileInactive = Inactivity?.KeepsAccruing ?? true,
            SuspendsAccrualOnTotalGarnishment = Inactivity?.SuspendsOnTotalGarnishment ?? false,
            SuspendsAccrualOnDeceased = Inactivity?.SuspendsOnDeceased ?? false,
            MinTermDays = cdat?.MinTermDays,
            MaxTermDays = cdat?.MaxTermDays,
            InterestPeriodicities = cdat?.InterestPeriodicities ?? [],
            InterestTimings = cdat?.Timings ?? [],
            AutoRollover = cdat?.RolloverAutomatic,
            MaxRollovers = cdat?.MaxRollovers,
            RolloverRateRule = cdat?.RolloverRateRule,
            CapitalizesOnRollover = cdat?.RolloverCapitalizes,
            GraceBusinessDays = cdat?.GraceBusinessDays,
            GraceAccrues = cdat?.GraceAccrues,
            AfterMaturityTreatment = cdat?.AfterMaturity,
            MoveToDemandLineId = lineaALaVistaId,
            EarlyRedemptionAllowed = redencion?.Allowed,
            EarlyRedemptionPenaltyMode = redencion is { Allowed: true } ? redencion.PenaltyMode : null,
            PenaltyAnnualRate = redencion is { Allowed: true } ? redencion.PenaltyRateEa : null,
            PenaltyDiscountFraction = redencion is { Allowed: true } ? redencion.PenaltyDiscount : null,
            MinDaysBeforeRedemption = redencion is { Allowed: true } ? redencion.MinDaysBeforeRedemption : null,
            InstallmentPeriodicity = contractual?.InstallmentPeriodicity,
            MinInstallmentAmount = contractual?.MinInstallment,
            MinTermMonths = contractual?.TermMonthsFrom,
            MaxTermMonths = contractual?.TermMonthsTo,
            BonusAnnualRate = contractual?.BonusRateEa,
            BreachAnnualRate = contractual?.BreachRateEa,
            ToleratedLateInstallments = contractual?.ToleranceInstallments,
            BreachEffect = contractual?.BreachEffect,
            BonusRecognition = contractual?.BonusRecognition,
            MaturityDestinations = contractual?.MaturityDestinations,
            WithdrawableOnlyOnMembershipWithdrawal = Permanent?.WithdrawableOnlyOnMembershipWithdrawal,
            RateTiers = tasa.Tiers.Select((t, i) => new BorradorDeTramoDeCaptacion(i + 1, t.BalanceFrom, t.BalanceTo, t.TermDaysFrom, t.TermDaysTo,
                t.AmountFrom, t.AmountTo, tasa.Kind == RateKind.Fixed ? t.EffectiveAnnual : null, tasa.Kind == RateKind.Variable ? t.Margin : null)).ToList(),
            Charges = (Charges ?? []).Select(c => c.Borrador()).ToList(),
        };
    }
}

/// <summary>Un valor de aportes: fijo en pesos, fracción del ingreso registrado o fracción del salario mínimo (FR-029).</summary>
public sealed record ContributionAmountInput(ContributionAmountMode Kind = ContributionAmountMode.FixedAmount, decimal? Amount = null, decimal? Fraction = null);

/// <summary>La cuota periódica (FR-040) con su redondeo, periodicidad y día de vencimiento.</summary>
public sealed record ContributionInstallmentInput
{
    public ContributionAmountMode Kind { get; init; } = ContributionAmountMode.FixedAmount;
    public decimal? Amount { get; init; }
    public decimal? Fraction { get; init; }
    public decimal RoundingUnit { get; init; } = 1m;
    public RoundingMode RoundingMode { get; init; } = RoundingMode.AwayFromZero;
    public RepaymentPeriodicity Periodicity { get; init; } = RepaymentPeriodicity.Monthly;
    public byte? DueDay { get; init; }
    public bool MinimumFirst { get; init; } = true;
}

/// <summary>El reparto de la cuota con el ahorro permanente (FR-049, D3): la línea permanente y la parte de la cuota que va a ella.</summary>
public sealed record PermanentSavingsShareInput(Guid DepositLinePublicId, decimal Fraction);

/// <summary>Una versión de la línea de aportes (api.md §3 <c>ContributionLineVersionInput</c>; data-model §2.5). Sin tasa.</summary>
public sealed record ContributionLineVersionInput
{
    public DateOnly ValidFrom { get; init; }
    public string Reason { get; init; } = string.Empty;
    public ContributionAmountInput MinimumNonReducible { get; init; } = new();
    public ContributionInstallmentInput Installment { get; init; } = new();
    public PermanentSavingsShareInput? PermanentSavingsShare { get; init; }
    public bool AllowsExtraordinary { get; init; }
    public decimal? MaxParticipationFraction { get; init; }
    public ExcessContributionAction ExcessAction { get; init; } = ExcessContributionAction.Reject;
    public bool AllowsTransferBetweenMembers { get; init; }
    public int RefundTermDays { get; init; }
    public bool LossDeductionEnabled { get; init; }

    /// <summary>El borrador del dominio; <paramref name="lineaPermanenteId"/> es el Id interno de la línea del ahorro permanente (la resolvió quien llama).</summary>
    public BorradorDeVersionDeAportes Borrador(int? lineaPermanenteId)
    {
        var minimo = MinimumNonReducible ?? new ContributionAmountInput();
        var cuota = Installment ?? new ContributionInstallmentInput();
        return new BorradorDeVersionDeAportes(ValidFrom, Reason ?? string.Empty)
        {
            MinimumMode = minimo.Kind,
            MinimumAmount = minimo.Amount,
            MinimumFraction = minimo.Fraction,
            InstallmentMode = cuota.Kind,
            InstallmentAmount = cuota.Amount,
            InstallmentFraction = cuota.Fraction,
            InstallmentRoundingUnit = cuota.RoundingUnit,
            InstallmentRoundingMode = cuota.RoundingMode,
            InstallmentPeriodicity = cuota.Periodicity,
            InstallmentDueDay = cuota.DueDay,
            MinimumFirst = cuota.MinimumFirst,
            PermanentSavingsLineId = PermanentSavingsShare is null ? null : lineaPermanenteId,
            PermanentSavingsFraction = PermanentSavingsShare?.Fraction,
            AllowsExtraordinary = AllowsExtraordinary,
            MaxParticipationFraction = MaxParticipationFraction,
            ExcessAction = ExcessAction,
            AllowsTransferBetweenMembers = AllowsTransferBetweenMembers,
            RefundTermDays = RefundTermDays,
            LossDeductionEnabled = LossDeductionEnabled,
        };
    }
}

// ------------------------------------------------------------------------------------------------------------------- salida --

/// <summary>Una referencia a otra línea del catálogo (la destino de un CDAT, la del ahorro permanente).</summary>
public sealed record LendingLineRefDto(Guid PublicId, string Code, string Name);

/// <summary>Una versión resumida en el historial, con su fin de vigencia implícito (la víspera de la siguiente).</summary>
public sealed record LendingLineVersionSummaryDto(
    Guid PublicId,
    int VersionNumber,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    DateTime RegisteredAt,
    string? RegisteredBy,
    string Reason,
    bool IsCurrent);

/// <summary>La versión vigente en la lista (api.md §3): su resumen legible para la grilla.</summary>
public sealed record LendingLineCurrentVersionDto(Guid PublicId, int VersionNumber, DateOnly ValidFrom, string Summary);

/// <summary>Una fila del catálogo común (api.md §3 <c>LendingLineListItemDto</c>).</summary>
public sealed record LendingLineListItemDto(
    Guid PublicId,
    string Code,
    string Name,
    LineNature Nature,
    DepositClass? DepositClass,
    CreditLineStatus Status,
    CreditLineClosedReason? ClosedReason,
    LendingLineCurrentVersionDto? CurrentVersion);

public sealed record DepositRateTierDto(
    int Order,
    decimal? BalanceFrom,
    decimal? BalanceTo,
    int? TermDaysFrom,
    int? TermDaysTo,
    decimal? AmountFrom,
    decimal? AmountTo,
    decimal? EffectiveAnnual,
    decimal? Margin);

/// <summary>Una versión de captación completa (api.md §3 <c>DepositLineVersionDto</c>): las columnas de data-model §2.1 y sus tramos.</summary>
public sealed record DepositLineVersionDto(
    Guid PublicId,
    int VersionNumber,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    DateTime RegisteredAt,
    string Reason,
    DepositClass DepositClass,
    IReadOnlyList<Guid> OfficePublicIds,
    bool RequiresMembership,
    bool AllowsCoHolders,
    bool AllowsMinors,
    bool AllowsLegalEntities,
    bool AllowsPartialWithdrawals,
    RateKind RateKind,
    decimal? EffectiveAnnual,
    string? IndexCode,
    string? IndexKey,
    decimal? Margin,
    decimal? RateFloor,
    decimal? RateCeiling,
    RepaymentPeriodicity? ResetPeriodicity,
    RateTierMode? TierMode,
    IReadOnlyList<DepositRateTierDto> Tiers,
    bool DeclaresAgreedRate,
    bool NegotiatedRateAllowed,
    bool KeepsNegotiatedMarginOnRollover,
    int RateChangeNoticeDays,
    BalanceMethod BalanceMethod,
    DayCountConvention DayCount,
    RateEquivalence RateEquivalence,
    decimal RoundingUnit,
    RoundingMode RoundingMode,
    RoundingMoment RoundingMoment,
    decimal MinOpeningAmount,
    decimal MinBalance,
    decimal MinBalanceForInterest,
    RepaymentPeriodicity SettlementPeriodicity,
    byte? SettlementDay,
    string WithholdingRateParameterCode,
    bool GmfApplies,
    decimal? MaxWithdrawalPerOperation,
    decimal? MaxWithdrawalPerDay,
    decimal? MaxCashPerOperation,
    decimal? MaxCashPerDay,
    int? InactivityDays,
    bool AccruesWhileInactive,
    bool SuspendsAccrualOnTotalGarnishment,
    bool SuspendsAccrualOnDeceased,
    TermDepositLineInput? TermDeposit,
    LendingLineRefDto? DemandLine,
    ContractualLineInput? Contractual,
    PermanentLineInput? Permanent,
    string ContentSha256)
{
    /// <summary>US4 (T114): el catálogo de cargos de la versión.</summary>
    public IReadOnlyList<DepositChargeInput> Charges { get; init; } = [];

    public static DepositLineVersionDto De(DepositLineVersion v, IEnumerable<DepositLineRateTier> tramos, DateOnly? vigenteHasta, LendingLineRefDto? destino,
        IEnumerable<DepositLineCharge>? cargos = null)
    {
        var cdat = v.DepositClass == Domain.Enums.Lending.DepositClass.TermDeposit
            ? new TermDepositLineInput
            {
                MinTermDays = v.MinTermDays ?? 0,
                MaxTermDays = v.MaxTermDays ?? 0,
                InterestPeriodicities = v.LeerPeriodicidadesDeInteres(),
                Timings = v.LeerModalidadesDeInteres(),
                RolloverAutomatic = v.AutoRollover ?? false,
                MaxRollovers = v.MaxRollovers,
                RolloverRateRule = v.RolloverRateRule ?? Domain.Enums.Lending.RolloverRateRule.CurrentTableRate,
                RolloverCapitalizes = v.CapitalizesOnRollover ?? false,
                GraceBusinessDays = v.GraceBusinessDays ?? 0,
                GraceAccrues = v.GraceAccrues ?? false,
                AfterMaturity = v.AfterMaturityTreatment ?? AfterMaturityTreatment.StopAccruing,
                DemandLinePublicId = destino?.PublicId,
                EarlyRedemption = new EarlyRedemptionInput(v.EarlyRedemptionAllowed ?? false, v.EarlyRedemptionPenaltyMode, v.PenaltyAnnualRate,
                    v.PenaltyDiscountFraction, v.MinDaysBeforeRedemption),
            }
            : null;
        var contractual = v.DepositClass == Domain.Enums.Lending.DepositClass.Contractual
            ? new ContractualLineInput
            {
                InstallmentPeriodicity = v.InstallmentPeriodicity ?? RepaymentPeriodicity.Monthly,
                MinInstallment = v.MinInstallmentAmount ?? 0m,
                TermMonthsFrom = v.MinTermMonths ?? 0,
                TermMonthsTo = v.MaxTermMonths ?? 0,
                BonusRateEa = v.BonusAnnualRate,
                BreachRateEa = v.BreachAnnualRate,
                ToleranceInstallments = v.ToleratedLateInstallments,
                BreachEffect = v.BreachEffect ?? ContractualBreachEffect.MoveToDemand,
                BonusRecognition = v.BonusRecognition ?? Domain.Enums.Lending.BonusRecognition.AtFulfillment,
                MaturityDestinations = v.MaturityDestinations ?? ContractualMaturityDestinations.All,
            }
            : null;
        return new DepositLineVersionDto(v.PublicId, v.VersionNumber, v.ValidFrom, vigenteHasta, v.RegisteredAt, v.Reason, v.DepositClass, v.LeerOficinas(),
            v.RequiresAssociate, v.AllowsCoHolders, v.AllowsMinors, v.AllowsLegalEntities, v.AllowsPartialWithdrawals, v.RateKind, v.AnnualRate, v.IndexCode,
            v.IndexKey, v.Margin, v.RateFloor, v.RateCeiling, v.ResetPeriodicity, v.RateTierMode,
            tramos.OrderBy(t => t.Order).Select(t => new DepositRateTierDto(t.Order, t.MinBalance, t.MaxBalance, t.MinTermDays, t.MaxTermDays, t.MinAmount,
                t.MaxAmount, t.AnnualRate, t.Margin)).ToList(),
            v.DeclaresAgreedRate, v.AllowsNegotiatedRate, v.KeepsNegotiatedMarginOnRollover, v.RateChangeNoticeDays, v.BalanceMethod, v.DayCount,
            v.RateEquivalence, v.RoundingUnit, v.RoundingMode, v.RoundingMoment, v.MinOpeningAmount, v.MinBalance, v.MinBalanceForInterest,
            v.SettlementPeriodicity, v.SettlementDay, v.WithholdingRateParameterCode, v.IsGmfSubject, v.MaxWithdrawalPerOperation, v.MaxWithdrawalPerDay,
            v.MaxCashPerOperation, v.MaxCashPerDay, v.InactivityDays, v.AccruesWhileInactive, v.SuspendsAccrualOnTotalGarnishment, v.SuspendsAccrualOnDeceased,
            cdat, destino, contractual,
            v.DepositClass == Domain.Enums.Lending.DepositClass.Permanent ? new PermanentLineInput(v.WithdrawableOnlyOnMembershipWithdrawal ?? true) : null,
            v.ContentSha256)
        {
            Charges = (cargos ?? []).OrderBy(c => c.Code, StringComparer.Ordinal).Select(DepositChargeInput.De).ToList(),
        };
    }
}

/// <summary>Una versión de aportes completa (api.md §3 <c>ContributionLineVersionDto</c>; data-model §2.5).</summary>
public sealed record ContributionLineVersionDto(
    Guid PublicId,
    int VersionNumber,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    DateTime RegisteredAt,
    string Reason,
    ContributionAmountInput MinimumNonReducible,
    ContributionInstallmentInput Installment,
    LendingLineRefDto? PermanentSavingsLine,
    decimal? PermanentSavingsFraction,
    bool AllowsExtraordinary,
    decimal? MaxParticipationFraction,
    ExcessContributionAction ExcessAction,
    bool AllowsTransferBetweenMembers,
    int RefundTermDays,
    bool LossDeductionEnabled,
    string ContentSha256)
{
    public static ContributionLineVersionDto De(ContributionLineVersion v, DateOnly? vigenteHasta, LendingLineRefDto? permanente) => new(
        v.PublicId, v.VersionNumber, v.ValidFrom, vigenteHasta, v.RegisteredAt, v.Reason,
        new ContributionAmountInput(v.MinimumMode, v.MinimumAmount, v.MinimumFraction),
        new ContributionInstallmentInput
        {
            Kind = v.InstallmentMode, Amount = v.InstallmentAmount, Fraction = v.InstallmentFraction, RoundingUnit = v.InstallmentRoundingUnit,
            RoundingMode = v.InstallmentRoundingMode, Periodicity = v.InstallmentPeriodicity, DueDay = v.InstallmentDueDay, MinimumFirst = v.MinimumFirst,
        },
        permanente, v.PermanentSavingsFraction, v.AllowsExtraordinary, v.MaxParticipationFraction, v.ExcessAction, v.AllowsTransferBetweenMembers,
        v.RefundTermDays, v.LossDeductionEnabled, v.ContentSha256);
}

/// <summary>
/// Una línea del catálogo común con la versión vigente en <see cref="Date"/> según su naturaleza y su historial (api.md §3, <c>GET /{id}</c>). Las de
/// crédito sólo traen la identidad y el historial: su versión se lee por <c>/api/lending/credit-lines</c>.
/// </summary>
public sealed record LendingLineDto(
    Guid PublicId,
    string Code,
    string Name,
    LineNature Nature,
    DepositClass? DepositClass,
    CreditLineStatus Status,
    CreditLineClosedReason? ClosedReason,
    DateTime? ClosedAt,
    string? CloseNotes,
    DateOnly Date,
    DepositLineVersionDto? DepositVersion,
    ContributionLineVersionDto? ContributionVersion,
    IReadOnlyList<LendingLineVersionSummaryDto> Versions);
