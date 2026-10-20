using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Deposits;

// Feature 014, US1 (T060–T062; data-model §2.1, §2.2, §2.5): lo que reciben las fábricas de las versiones de las líneas de captación y de
// aportes. Viven fuera de Domain.Entities porque no son tablas. Los nombres de las propiedades son los de las columnas para que la aplicación
// copie sin traducir; las listas son de enums (la entidad las guarda como JSON). (nuevo)

/// <summary>
/// Un tramo de tasa de una versión de captación (data-model §2.2): por saldo en la vista, el contractual y el permanente; por plazo y monto en el
/// CDAT. Exactamente una de <see cref="AnnualRate"/> (tasa fija) y <see cref="Margin"/> (variable). (nuevo)
/// </summary>
public sealed record BorradorDeTramoDeCaptacion(
    int Order,
    decimal? MinBalance = null,
    decimal? MaxBalance = null,
    int? MinTermDays = null,
    int? MaxTermDays = null,
    decimal? MinAmount = null,
    decimal? MaxAmount = null,
    decimal? AnnualRate = null,
    decimal? Margin = null)
{
    /// <summary>¿Es un tramo por saldo? (sin plazo ni monto).</summary>
    public bool EsPorSaldo => MinTermDays is null && MaxTermDays is null && MinAmount is null && MaxAmount is null;
}

/// <summary>
/// Un cargo del catálogo de una versión de captación (feature 014, US4, T114; data-model §2.3; FR-128, D35): apagado salvo que se pida
/// <see cref="IsEnabled"/>. El código es el estable entre versiones (<c>CodigoDeCatalogo</c>, hasta 10 caracteres). (nuevo)
/// </summary>
public sealed record BorradorDeCargoDeCaptacion(string Code, string Name, ChargeKind Kind, ChargeTrigger Trigger, ChargeCalculation CalculationMode)
{
    public bool IsEnabled { get; init; }
    public RepaymentPeriodicity? Periodicity { get; init; }
    public int? FreeOperationsPerPeriod { get; init; }
    public RepaymentPeriodicity? FreePeriod { get; init; }
    public decimal? FixedAmount { get; init; }
    public decimal? Percentage { get; init; }
    public ChargeBase? PercentageBase { get; init; }
    public decimal? MinAmount { get; init; }
    public decimal? MaxAmount { get; init; }
    public decimal? ExemptIfAverageBalanceAtLeast { get; init; }
    public decimal? ExemptIfMinimumBalanceAtLeast { get; init; }
    public int? ExemptIfSeniorityMonthsAtLeast { get; init; }
    public bool ExemptEmployees { get; init; }
    public bool ExemptGmfExemptAccount { get; init; }
    public bool ExemptMinors { get; init; }
    public IReadOnlyList<Guid> ExemptBranchPublicIds { get; init; } = [];
    public ChargeShortfallPolicy ShortfallPolicy { get; init; } = ChargeShortfallPolicy.KeepPending;
    public bool IsVatTaxed { get; init; }
    public string? CommercialItemCode { get; init; }

    /// <summary>El cargo de una versión ya guardada.</summary>
    public static BorradorDeCargoDeCaptacion De(DepositLineCharge c) => new(c.Code, c.Name, c.Kind, c.Trigger, c.CalculationMode)
    {
        IsEnabled = c.IsEnabled,
        Periodicity = c.Periodicity,
        FreeOperationsPerPeriod = c.FreeOperationsPerPeriod,
        FreePeriod = c.FreePeriod,
        FixedAmount = c.FixedAmount,
        Percentage = c.Percentage,
        PercentageBase = c.PercentageBase,
        MinAmount = c.MinAmount,
        MaxAmount = c.MaxAmount,
        ExemptIfAverageBalanceAtLeast = c.ExemptIfAverageBalanceAtLeast,
        ExemptIfMinimumBalanceAtLeast = c.ExemptIfMinimumBalanceAtLeast,
        ExemptIfSeniorityMonthsAtLeast = c.ExemptIfSeniorityMonthsAtLeast,
        ExemptEmployees = c.ExemptEmployees,
        ExemptGmfExemptAccount = c.ExemptGmfExemptAccount,
        ExemptMinors = c.ExemptMinors,
        ExemptBranchPublicIds = c.LeerOficinasExoneradas(),
        ShortfallPolicy = c.ShortfallPolicy,
        IsVatTaxed = c.IsVatTaxed,
        CommercialItemCode = c.CommercialItemCode,
    };
}

/// <summary>
/// La versión de una línea de captación que se quiere registrar, completa (data-model §2.1): una versión nueva se registra entera con sus
/// tramos. Las columnas de una clase sólo se llenan en esa clase (<see cref="ReglasDeLineaDeCaptacion"/>). (nuevo)
/// </summary>
public sealed record BorradorDeVersionDeCaptacion(DateOnly ValidFrom, string Reason, DepositClass DepositClass)
{
    // ------------------------------------------------------------------------------------------------ elegibilidad --
    public bool RequiresAssociate { get; init; } = true;
    public bool AllowsCoHolders { get; init; }
    public bool AllowsMinors { get; init; }
    public bool AllowsLegalEntities { get; init; }
    public bool AllowsPartialWithdrawals { get; init; }

    /// <summary>Las oficinas que la ofrecen; vacío = todas.</summary>
    public IReadOnlyList<Guid> BranchPublicIds { get; init; } = [];

    // -------------------------------------------------------------------------------------------------------- tasa --
    public RateKind RateKind { get; init; } = RateKind.Fixed;
    public decimal? AnnualRate { get; init; }
    public string? IndexCode { get; init; }
    public string? IndexKey { get; init; }
    public decimal? Margin { get; init; }
    public decimal? RateFloor { get; init; }
    public decimal? RateCeiling { get; init; }
    public RepaymentPeriodicity? ResetPeriodicity { get; init; }
    public RateTierMode? RateTierMode { get; init; }
    public bool DeclaresAgreedRate { get; init; }
    public bool AllowsNegotiatedRate { get; init; }
    public bool KeepsNegotiatedMarginOnRollover { get; init; }
    public int RateChangeNoticeDays { get; init; }

    // ------------------------------------------------------------------------------------------------------ cálculo --
    public BalanceMethod BalanceMethod { get; init; } = BalanceMethod.DailyBalance;
    public DayCountConvention DayCount { get; init; } = DayCountConvention.Actual365;
    public RateEquivalence RateEquivalence { get; init; } = RateEquivalence.Compound;
    public decimal RoundingUnit { get; init; } = 1m;
    public RoundingMode RoundingMode { get; init; } = RoundingMode.AwayFromZero;
    public RoundingMoment RoundingMoment { get; init; } = RoundingMoment.PerTotal;
    public decimal MinOpeningAmount { get; init; }
    public decimal MinBalance { get; init; }
    public decimal MinBalanceForInterest { get; init; }
    public RepaymentPeriodicity SettlementPeriodicity { get; init; } = RepaymentPeriodicity.Monthly;
    public byte? SettlementDay { get; init; }

    // --------------------------------------------------------------------------------------------------- impuestos --
    /// <summary>La clave del parámetro de tarifa de retención; vacía = la que sugiere la clase (<see cref="ReglasDeLineaDeCaptacion.TarifaDeRetencionDe"/>).</summary>
    public string? WithholdingRateParameterCode { get; init; }
    public bool IsGmfSubject { get; init; } = true;

    // ----------------------------------------------------------------------------------------------------- límites --
    public decimal? MaxWithdrawalPerOperation { get; init; }
    public decimal? MaxWithdrawalPerDay { get; init; }
    public decimal? MaxCashPerOperation { get; init; }
    public decimal? MaxCashPerDay { get; init; }

    // --------------------------------------------------------------------------------------- inactividad y suspensión --
    public int? InactivityDays { get; init; }
    public bool AccruesWhileInactive { get; init; } = true;
    public bool SuspendsAccrualOnTotalGarnishment { get; init; }
    public bool SuspendsAccrualOnDeceased { get; init; }

    // -------------------------------------------------------------------------------------------------------- CDAT --
    public int? MinTermDays { get; init; }
    public int? MaxTermDays { get; init; }
    public IReadOnlyList<RepaymentPeriodicity> InterestPeriodicities { get; init; } = [];
    public IReadOnlyList<InterestTiming> InterestTimings { get; init; } = [];
    public bool? AutoRollover { get; init; }
    public int? MaxRollovers { get; init; }
    public RolloverRateRule? RolloverRateRule { get; init; }
    public bool? CapitalizesOnRollover { get; init; }
    public int? GraceBusinessDays { get; init; }
    public bool? GraceAccrues { get; init; }
    public AfterMaturityTreatment? AfterMaturityTreatment { get; init; }

    /// <summary>La línea a la vista destino (Id interno; la aplicación la resuelve por su PublicId).</summary>
    public int? MoveToDemandLineId { get; init; }
    public bool? EarlyRedemptionAllowed { get; init; }
    public EarlyRedemptionPenaltyMode? EarlyRedemptionPenaltyMode { get; init; }
    public decimal? PenaltyAnnualRate { get; init; }
    public decimal? PenaltyDiscountFraction { get; init; }
    public int? MinDaysBeforeRedemption { get; init; }

    // -------------------------------------------------------------------------------------------------- contractual --
    public RepaymentPeriodicity? InstallmentPeriodicity { get; init; }
    public decimal? MinInstallmentAmount { get; init; }
    public int? MinTermMonths { get; init; }
    public int? MaxTermMonths { get; init; }
    public decimal? BonusAnnualRate { get; init; }
    public decimal? BreachAnnualRate { get; init; }
    public int? ToleratedLateInstallments { get; init; }
    public ContractualBreachEffect? BreachEffect { get; init; }
    public BonusRecognition? BonusRecognition { get; init; }
    public ContractualMaturityDestinations? MaturityDestinations { get; init; }

    // --------------------------------------------------------------------------------------------------- permanente --
    public bool? WithdrawableOnlyOnMembershipWithdrawal { get; init; }

    // -------------------------------------------------------------------------------------------------------- hijas --
    public IReadOnlyList<BorradorDeTramoDeCaptacion> RateTiers { get; init; } = [];

    /// <summary>US4 (T114): el catálogo de cargos de la versión (FR-128); vacío = sin cargos (el defecto, D35).</summary>
    public IReadOnlyList<BorradorDeCargoDeCaptacion> Charges { get; init; } = [];

    /// <summary>La versión completa a partir de una ya guardada y sus tramos (para versionar desde la vigente o verificar la huella).</summary>
    public static BorradorDeVersionDeCaptacion De(DepositLineVersion v, IEnumerable<DepositLineRateTier> tramos,
        IEnumerable<DepositLineCharge>? cargos = null) =>
        new(v.ValidFrom, v.Reason, v.DepositClass)
        {
            RequiresAssociate = v.RequiresAssociate,
            AllowsCoHolders = v.AllowsCoHolders,
            AllowsMinors = v.AllowsMinors,
            AllowsLegalEntities = v.AllowsLegalEntities,
            AllowsPartialWithdrawals = v.AllowsPartialWithdrawals,
            BranchPublicIds = v.LeerOficinas(),
            RateKind = v.RateKind,
            AnnualRate = v.AnnualRate,
            IndexCode = v.IndexCode,
            IndexKey = v.IndexKey,
            Margin = v.Margin,
            RateFloor = v.RateFloor,
            RateCeiling = v.RateCeiling,
            ResetPeriodicity = v.ResetPeriodicity,
            RateTierMode = v.RateTierMode,
            DeclaresAgreedRate = v.DeclaresAgreedRate,
            AllowsNegotiatedRate = v.AllowsNegotiatedRate,
            KeepsNegotiatedMarginOnRollover = v.KeepsNegotiatedMarginOnRollover,
            RateChangeNoticeDays = v.RateChangeNoticeDays,
            BalanceMethod = v.BalanceMethod,
            DayCount = v.DayCount,
            RateEquivalence = v.RateEquivalence,
            RoundingUnit = v.RoundingUnit,
            RoundingMode = v.RoundingMode,
            RoundingMoment = v.RoundingMoment,
            MinOpeningAmount = v.MinOpeningAmount,
            MinBalance = v.MinBalance,
            MinBalanceForInterest = v.MinBalanceForInterest,
            SettlementPeriodicity = v.SettlementPeriodicity,
            SettlementDay = v.SettlementDay,
            WithholdingRateParameterCode = v.WithholdingRateParameterCode,
            IsGmfSubject = v.IsGmfSubject,
            MaxWithdrawalPerOperation = v.MaxWithdrawalPerOperation,
            MaxWithdrawalPerDay = v.MaxWithdrawalPerDay,
            MaxCashPerOperation = v.MaxCashPerOperation,
            MaxCashPerDay = v.MaxCashPerDay,
            InactivityDays = v.InactivityDays,
            AccruesWhileInactive = v.AccruesWhileInactive,
            SuspendsAccrualOnTotalGarnishment = v.SuspendsAccrualOnTotalGarnishment,
            SuspendsAccrualOnDeceased = v.SuspendsAccrualOnDeceased,
            MinTermDays = v.MinTermDays,
            MaxTermDays = v.MaxTermDays,
            InterestPeriodicities = v.LeerPeriodicidadesDeInteres(),
            InterestTimings = v.LeerModalidadesDeInteres(),
            AutoRollover = v.AutoRollover,
            MaxRollovers = v.MaxRollovers,
            RolloverRateRule = v.RolloverRateRule,
            CapitalizesOnRollover = v.CapitalizesOnRollover,
            GraceBusinessDays = v.GraceBusinessDays,
            GraceAccrues = v.GraceAccrues,
            AfterMaturityTreatment = v.AfterMaturityTreatment,
            MoveToDemandLineId = v.MoveToDemandLineId,
            EarlyRedemptionAllowed = v.EarlyRedemptionAllowed,
            EarlyRedemptionPenaltyMode = v.EarlyRedemptionPenaltyMode,
            PenaltyAnnualRate = v.PenaltyAnnualRate,
            PenaltyDiscountFraction = v.PenaltyDiscountFraction,
            MinDaysBeforeRedemption = v.MinDaysBeforeRedemption,
            InstallmentPeriodicity = v.InstallmentPeriodicity,
            MinInstallmentAmount = v.MinInstallmentAmount,
            MinTermMonths = v.MinTermMonths,
            MaxTermMonths = v.MaxTermMonths,
            BonusAnnualRate = v.BonusAnnualRate,
            BreachAnnualRate = v.BreachAnnualRate,
            ToleratedLateInstallments = v.ToleratedLateInstallments,
            BreachEffect = v.BreachEffect,
            BonusRecognition = v.BonusRecognition,
            MaturityDestinations = v.MaturityDestinations,
            WithdrawableOnlyOnMembershipWithdrawal = v.WithdrawableOnlyOnMembershipWithdrawal,
            RateTiers = tramos.OrderBy(t => t.Order).Select(t => new BorradorDeTramoDeCaptacion(t.Order, t.MinBalance, t.MaxBalance, t.MinTermDays,
                t.MaxTermDays, t.MinAmount, t.MaxAmount, t.AnnualRate, t.Margin)).ToList(),
            Charges = (cargos ?? []).OrderBy(c => c.Code, StringComparer.Ordinal).Select(BorradorDeCargoDeCaptacion.De).ToList(),
        };

    /// <summary>El modo de los tramos que rige: nulo sin tramos; con tramos, el declarado o «todo el saldo» (D8).</summary>
    public RateTierMode? ModoDeTramosEfectivo => RateTiers.Count == 0 ? null : RateTierMode ?? Enums.Lending.RateTierMode.WholeBalance;

    /// <summary>¿Cambia la tasa respecto de <paramref name="otra"/>? Clase, tasa base, índice, margen, piso, techo, modo y tramos (FR-038).</summary>
    public bool CambiaLaTasaRespectoDe(BorradorDeVersionDeCaptacion otra)
    {
        ArgumentNullException.ThrowIfNull(otra);
        if (RateKind != otra.RateKind || AnnualRate != otra.AnnualRate || !string.Equals(IndexCode, otra.IndexCode, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(IndexKey, otra.IndexKey, StringComparison.Ordinal) || Margin != otra.Margin || RateFloor != otra.RateFloor
            || RateCeiling != otra.RateCeiling || ModoDeTramosEfectivo != otra.ModoDeTramosEfectivo)
            return true;
        var estos = RateTiers.OrderBy(t => t.Order).Select(t => t with { Order = 0 }).ToList();
        var esos = otra.RateTiers.OrderBy(t => t.Order).Select(t => t with { Order = 0 }).ToList();
        return !estos.SequenceEqual(esos);
    }
}

/// <summary>
/// La versión de la línea de aportes que se quiere registrar (data-model §2.5). Sin tasa: los aportes no causan intereses (R02). El ahorro
/// permanente que acompaña la cuota se nombra por el Id interno de su línea (la aplicación lo resuelve). (nuevo)
/// </summary>
public sealed record BorradorDeVersionDeAportes(DateOnly ValidFrom, string Reason)
{
    // --------------------------------------------------------------------------------------------- mínimo no reducible --
    public ContributionAmountMode MinimumMode { get; init; } = ContributionAmountMode.FixedAmount;
    public decimal? MinimumAmount { get; init; }
    public decimal? MinimumFraction { get; init; }

    // ---------------------------------------------------------------------------------------------------- cuota periódica --
    public ContributionAmountMode InstallmentMode { get; init; } = ContributionAmountMode.FixedAmount;
    public decimal? InstallmentAmount { get; init; }
    public decimal? InstallmentFraction { get; init; }
    public decimal InstallmentRoundingUnit { get; init; } = 1m;
    public RoundingMode InstallmentRoundingMode { get; init; } = RoundingMode.AwayFromZero;
    public RepaymentPeriodicity InstallmentPeriodicity { get; init; } = RepaymentPeriodicity.Monthly;
    public byte? InstallmentDueDay { get; init; }
    public bool MinimumFirst { get; init; } = true;

    // ----------------------------------------------------------------------------------------- reparto con ahorro permanente --
    public int? PermanentSavingsLineId { get; init; }
    public decimal? PermanentSavingsFraction { get; init; }

    // ------------------------------------------------------------------------------------------- extraordinarios y límites --
    public bool AllowsExtraordinary { get; init; }
    public decimal? MaxParticipationFraction { get; init; }
    public ExcessContributionAction ExcessAction { get; init; } = ExcessContributionAction.Reject;
    public bool AllowsTransferBetweenMembers { get; init; }

    // ---------------------------------------------------------------------------------------------------------- devolución --
    public int RefundTermDays { get; init; }
    public bool LossDeductionEnabled { get; init; }

    /// <summary>La versión completa a partir de una ya guardada.</summary>
    public static BorradorDeVersionDeAportes De(ContributionLineVersion v) => new(v.ValidFrom, v.Reason)
    {
        MinimumMode = v.MinimumMode,
        MinimumAmount = v.MinimumAmount,
        MinimumFraction = v.MinimumFraction,
        InstallmentMode = v.InstallmentMode,
        InstallmentAmount = v.InstallmentAmount,
        InstallmentFraction = v.InstallmentFraction,
        InstallmentRoundingUnit = v.InstallmentRoundingUnit,
        InstallmentRoundingMode = v.InstallmentRoundingMode,
        InstallmentPeriodicity = v.InstallmentPeriodicity,
        InstallmentDueDay = v.InstallmentDueDay,
        MinimumFirst = v.MinimumFirst,
        PermanentSavingsLineId = v.PermanentSavingsLineId,
        PermanentSavingsFraction = v.PermanentSavingsFraction,
        AllowsExtraordinary = v.AllowsExtraordinary,
        MaxParticipationFraction = v.MaxParticipationFraction,
        ExcessAction = v.ExcessAction,
        AllowsTransferBetweenMembers = v.AllowsTransferBetweenMembers,
        RefundTermDays = v.RefundTermDays,
        LossDeductionEnabled = v.LossDeductionEnabled,
    };

    /// <summary>La fracción de la cuota que va a aportes: todo, salvo la parte del ahorro permanente (FR-049).</summary>
    public decimal FraccionAAportes => 1m - (PermanentSavingsFraction ?? 0m);
}
