namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 014, US1 (T070; contracts/api.md §3 de la 014; research E42): lo que la pantalla del catálogo común manda y recibe por /api/lending/lines.
// Los pedidos son clases editables (las enlaza el editor) con los nombres del contrato; las tasas viajan como fracción (la pantalla las muestra en
// puntos) y los enums como número. Las respuestas leen los enums como int. (nuevo)

// --------------------------------------------------------------------------------------------------------------- pedidos --

public sealed class TramoDeCaptacionRequest
{
    public decimal? BalanceFrom { get; set; }
    public decimal? BalanceTo { get; set; }
    public int? TermDaysFrom { get; set; }
    public int? TermDaysTo { get; set; }
    public decimal? AmountFrom { get; set; }
    public decimal? AmountTo { get; set; }
    public decimal? EffectiveAnnual { get; set; }
    public decimal? Margin { get; set; }
}

public sealed class TasaVariableDeCaptacionRequest
{
    public string IndexCode { get; set; } = string.Empty;
    public string IndexKey { get; set; } = string.Empty;
    public decimal Margin { get; set; }
    public decimal? Floor { get; set; }
    public decimal? Ceiling { get; set; }
    public int? ResetPeriodicity { get; set; }
}

public sealed class TasaDeCaptacionRequest
{
    public int Kind { get; set; } = 1;
    public decimal? EffectiveAnnual { get; set; }
    public TasaVariableDeCaptacionRequest? Variable { get; set; }
    public int? TierMode { get; set; }
    public List<TramoDeCaptacionRequest> Tiers { get; set; } = [];
}

public sealed class MinimosDeCaptacionRequest
{
    public decimal ToOpen { get; set; }
    public decimal ToMaintain { get; set; }
    public decimal ToEarnInterest { get; set; }
}

public sealed class RedondeoDeCaptacionRequest
{
    public decimal Unit { get; set; } = 1m;
    public int Mode { get; set; } = 1;
    public int Moment { get; set; } = 3;
}

public sealed class ImpuestosDeCaptacionRequest
{
    public string? WithholdingRateParameterCode { get; set; }
    public bool GmfApplies { get; set; } = true;

    /// <summary>Respuesta 97 (E253): si la entrega por fallecimiento y el pago de no reclamados cobran GMF (defecto sí; pendiente de validar por la contadora).</summary>
    public bool GmfOnDeceasedAndUnclaimed { get; set; } = true;
}

public sealed class LimitesDeCaptacionRequest
{
    public decimal? MaxWithdrawalPerOperation { get; set; }
    public decimal? MaxWithdrawalPerDay { get; set; }
    public decimal? MaxCashPerOperation { get; set; }
    public decimal? MaxCashPerDay { get; set; }
}

public sealed class InactividadDeCaptacionRequest
{
    public int? Days { get; set; } = 360;
    public bool KeepsAccruing { get; set; } = true;
    public bool SuspendsOnTotalGarnishment { get; set; }
    public bool SuspendsOnDeceased { get; set; }
}

public sealed class RedencionAnticipadaRequest
{
    public bool Allowed { get; set; }
    public int? PenaltyMode { get; set; }
    public decimal? PenaltyRateEa { get; set; }
    public decimal? PenaltyDiscount { get; set; }
    public int? MinDaysBeforeRedemption { get; set; }
}

public sealed class CdatDeLaLineaRequest
{
    public int MinTermDays { get; set; } = 30;
    public int MaxTermDays { get; set; } = 360;
    public List<int> InterestPeriodicities { get; set; } = [0];
    public List<int> Timings { get; set; } = [1];
    public bool RolloverAutomatic { get; set; } = true;
    public int? MaxRollovers { get; set; }
    public int RolloverRateRule { get; set; } = 2;
    public bool RolloverCapitalizes { get; set; }
    public int GraceBusinessDays { get; set; } = 3;
    public bool GraceAccrues { get; set; }
    public int AfterMaturity { get; set; } = 1;
    public Guid? DemandLinePublicId { get; set; }
    public RedencionAnticipadaRequest EarlyRedemption { get; set; } = new();
}

public sealed class ContractualDeLaLineaRequest
{
    public int InstallmentPeriodicity { get; set; } = 30;
    public decimal MinInstallment { get; set; }
    public int TermMonthsFrom { get; set; } = 6;
    public int TermMonthsTo { get; set; } = 36;
    public decimal? BonusRateEa { get; set; }
    public decimal? BreachRateEa { get; set; }
    public int? ToleranceInstallments { get; set; }
    public int BreachEffect { get; set; } = 2;
    public int BonusRecognition { get; set; } = 1;

    /// <summary>A dónde puede ir lo ahorrado al vencer (máscara: 1 traslado a la cuenta a la vista, 2 giro; 0 = ninguno, queda a disposición).</summary>
    public int MaturityDestinations { get; set; } = 3;

    public bool AdmiteDestino(int bit) => (MaturityDestinations & bit) != 0;

    public void FijarDestino(int bit, bool admite) => MaturityDestinations = admite ? MaturityDestinations | bit : MaturityDestinations & ~bit;
}

public sealed class PermanenteDeLaLineaRequest
{
    public bool WithdrawableOnlyOnMembershipWithdrawal { get; set; } = true;
}

/// <summary>Una versión de una línea de captación (<c>DepositLineVersionInput</c>).</summary>
public sealed class VersionDeCaptacionRequest
{
    public DateOnly ValidFrom { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string Reason { get; set; } = string.Empty;
    public int? DepositClass { get; set; }
    public List<Guid>? OfficePublicIds { get; set; }
    public bool RequiresMembership { get; set; } = true;
    public bool AllowsCoHolders { get; set; }
    public bool AllowsMinors { get; set; }
    public bool AllowsLegalEntities { get; set; }
    public bool AllowsPartialWithdrawals { get; set; } = true;
    public TasaDeCaptacionRequest Rate { get; set; } = new();
    public bool DeclaresAgreedRate { get; set; }
    public bool NegotiatedRateAllowed { get; set; }
    public bool KeepsNegotiatedMarginOnRollover { get; set; }
    public int RateChangeNoticeDays { get; set; }
    public int BalanceMethod { get; set; } = 1;
    public MinimosDeCaptacionRequest Minimums { get; set; } = new();
    public int DayCount { get; set; } = 2;
    public int RateEquivalence { get; set; } = 1;
    public RedondeoDeCaptacionRequest Rounding { get; set; } = new();
    public int SettlementPeriodicity { get; set; } = 30;
    public byte? SettlementDay { get; set; }
    public ImpuestosDeCaptacionRequest Tax { get; set; } = new();
    public LimitesDeCaptacionRequest Limits { get; set; } = new();
    public InactividadDeCaptacionRequest Inactivity { get; set; } = new();
    public CdatDeLaLineaRequest? TermDeposit { get; set; }
    public ContractualDeLaLineaRequest? Contractual { get; set; }
    public PermanenteDeLaLineaRequest? Permanent { get; set; }

    /// <summary>Deja los bloques de la clase: el del CDAT, el del contractual o el del permanente, y nada de los otros.</summary>
    public void AjustarALaClase(int clase)
    {
        TermDeposit = clase == TextosDeCaptaciones.Cdat ? TermDeposit ?? new CdatDeLaLineaRequest() : null;
        Contractual = clase == TextosDeCaptaciones.Contractual ? Contractual ?? new ContractualDeLaLineaRequest() : null;
        Permanent = clase == TextosDeCaptaciones.Permanente ? Permanent ?? new PermanenteDeLaLineaRequest() : null;
        if (clase is TextosDeCaptaciones.Cdat or TextosDeCaptaciones.Permanente) AllowsPartialWithdrawals = false;
        if (clase == TextosDeCaptaciones.Cdat)
        {
            BalanceMethod = 1;
            RateChangeNoticeDays = 0;
        }
    }
}

public sealed class CrearLineaDeCaptacionRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int DepositClass { get; set; } = TextosDeCaptaciones.ALaVista;
    public bool Active { get; set; } = true;
    public VersionDeCaptacionRequest FirstVersion { get; set; } = new();
}

public sealed class ValorDeAportesRequest
{
    public int Kind { get; set; } = 1;
    public decimal? Amount { get; set; }
    public decimal? Fraction { get; set; }
}

public sealed class CuotaDeAportesRequest
{
    public int Kind { get; set; } = 2;
    public decimal? Amount { get; set; }
    public decimal? Fraction { get; set; }
    public decimal RoundingUnit { get; set; } = 1m;
    public int RoundingMode { get; set; } = 1;
    public int Periodicity { get; set; } = 30;
    public byte? DueDay { get; set; }
    public bool MinimumFirst { get; set; } = true;
}

public sealed class RepartoConPermanenteRequest
{
    public Guid DepositLinePublicId { get; set; }
    public decimal Fraction { get; set; }
}

/// <summary>Una versión de la línea de aportes (<c>ContributionLineVersionInput</c>).</summary>
public sealed class VersionDeAportesRequest
{
    public DateOnly ValidFrom { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string Reason { get; set; } = string.Empty;
    public ValorDeAportesRequest MinimumNonReducible { get; set; } = new();
    public CuotaDeAportesRequest Installment { get; set; } = new();
    public RepartoConPermanenteRequest? PermanentSavingsShare { get; set; }
    public bool AllowsExtraordinary { get; set; }
    public decimal? MaxParticipationFraction { get; set; }
    public int ExcessAction { get; set; } = 1;
    public bool AllowsTransferBetweenMembers { get; set; }
    public int RefundTermDays { get; set; }
    public bool LossDeductionEnabled { get; set; }
}

public sealed class CrearLineaDeAportesRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
    public VersionDeAportesRequest FirstVersion { get; set; } = new();
}

// ------------------------------------------------------------------------------------------------------------- respuestas --

public sealed record VersionVigenteDelCatalogoDto(Guid PublicId, int VersionNumber, DateOnly ValidFrom, string Summary);

/// <summary>Una fila del catálogo común (<c>LendingLineListItemDto</c>).</summary>
public sealed record LineaDelCatalogoDto(
    Guid PublicId,
    string Code,
    string Name,
    int Nature,
    int? DepositClass,
    int Status,
    int? ClosedReason,
    VersionVigenteDelCatalogoDto? CurrentVersion);

public sealed record ReferenciaDeLineaDto(Guid PublicId, string Code, string Name);

public sealed record ResumenDeVersionDelCatalogoDto(
    Guid PublicId,
    int VersionNumber,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    DateTime RegisteredAt,
    string? RegisteredBy,
    string Reason,
    bool IsCurrent);

public sealed record TramoDeCaptacionDto(int Order, decimal? BalanceFrom, decimal? BalanceTo, int? TermDaysFrom, int? TermDaysTo, decimal? AmountFrom,
    decimal? AmountTo, decimal? EffectiveAnnual, decimal? Margin);

public sealed record CdatDeLaLineaDto(int MinTermDays, int MaxTermDays, List<int> InterestPeriodicities, List<int> Timings, bool RolloverAutomatic,
    int? MaxRollovers, int RolloverRateRule, bool RolloverCapitalizes, int GraceBusinessDays, bool GraceAccrues, int AfterMaturity,
    Guid? DemandLinePublicId, RedencionAnticipadaDto EarlyRedemption);

public sealed record RedencionAnticipadaDto(bool Allowed, int? PenaltyMode, decimal? PenaltyRateEa, decimal? PenaltyDiscount, int? MinDaysBeforeRedemption);

public sealed record ContractualDeLaLineaDto(int InstallmentPeriodicity, decimal MinInstallment, int TermMonthsFrom, int TermMonthsTo, decimal? BonusRateEa,
    decimal? BreachRateEa, int? ToleranceInstallments, int BreachEffect, int BonusRecognition, int MaturityDestinations = 3);

public sealed record PermanenteDeLaLineaDto(bool WithdrawableOnlyOnMembershipWithdrawal);

/// <summary>Una versión de captación completa (<c>DepositLineVersionDto</c>).</summary>
public sealed record VersionDeCaptacionDto(
    Guid PublicId,
    int VersionNumber,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    DateTime RegisteredAt,
    string Reason,
    int DepositClass,
    List<Guid> OfficePublicIds,
    bool RequiresMembership,
    bool AllowsCoHolders,
    bool AllowsMinors,
    bool AllowsLegalEntities,
    bool AllowsPartialWithdrawals,
    int RateKind,
    decimal? EffectiveAnnual,
    string? IndexCode,
    string? IndexKey,
    decimal? Margin,
    decimal? RateFloor,
    decimal? RateCeiling,
    int? ResetPeriodicity,
    int? TierMode,
    List<TramoDeCaptacionDto> Tiers,
    bool DeclaresAgreedRate,
    bool NegotiatedRateAllowed,
    bool KeepsNegotiatedMarginOnRollover,
    int RateChangeNoticeDays,
    int BalanceMethod,
    int DayCount,
    int RateEquivalence,
    decimal RoundingUnit,
    int RoundingMode,
    int RoundingMoment,
    decimal MinOpeningAmount,
    decimal MinBalance,
    decimal MinBalanceForInterest,
    int SettlementPeriodicity,
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
    CdatDeLaLineaDto? TermDeposit,
    ReferenciaDeLineaDto? DemandLine,
    ContractualDeLaLineaDto? Contractual,
    PermanenteDeLaLineaDto? Permanent,
    string ContentSha256)
{
    /// <summary>Respuesta 97 (E253): si la entrega por fallecimiento y el pago de no reclamados cobran GMF en esta versión.</summary>
    public bool GmfOnDeceasedAndUnclaimed { get; init; } = true;

    /// <summary>El pedido de una versión nueva que parte de ésta (fecha hoy y motivo en blanco).</summary>
    public VersionDeCaptacionRequest ComoPedido() => new()
    {
        Reason = string.Empty,
        OfficePublicIds = OfficePublicIds.Count == 0 ? null : [.. OfficePublicIds],
        RequiresMembership = RequiresMembership,
        AllowsCoHolders = AllowsCoHolders,
        AllowsMinors = AllowsMinors,
        AllowsLegalEntities = AllowsLegalEntities,
        AllowsPartialWithdrawals = AllowsPartialWithdrawals,
        Rate = new TasaDeCaptacionRequest
        {
            Kind = RateKind,
            EffectiveAnnual = EffectiveAnnual,
            Variable = RateKind == 2
                ? new TasaVariableDeCaptacionRequest
                {
                    IndexCode = IndexCode ?? string.Empty, IndexKey = IndexKey ?? string.Empty, Margin = Margin ?? 0m, Floor = RateFloor,
                    Ceiling = RateCeiling, ResetPeriodicity = ResetPeriodicity,
                }
                : null,
            TierMode = TierMode,
            Tiers = [.. Tiers.Select(t => new TramoDeCaptacionRequest
            {
                BalanceFrom = t.BalanceFrom, BalanceTo = t.BalanceTo, TermDaysFrom = t.TermDaysFrom, TermDaysTo = t.TermDaysTo, AmountFrom = t.AmountFrom,
                AmountTo = t.AmountTo, EffectiveAnnual = t.EffectiveAnnual, Margin = t.Margin,
            })],
        },
        DeclaresAgreedRate = DeclaresAgreedRate,
        NegotiatedRateAllowed = NegotiatedRateAllowed,
        KeepsNegotiatedMarginOnRollover = KeepsNegotiatedMarginOnRollover,
        RateChangeNoticeDays = RateChangeNoticeDays,
        BalanceMethod = BalanceMethod,
        Minimums = new MinimosDeCaptacionRequest { ToOpen = MinOpeningAmount, ToMaintain = MinBalance, ToEarnInterest = MinBalanceForInterest },
        DayCount = DayCount,
        RateEquivalence = RateEquivalence,
        Rounding = new RedondeoDeCaptacionRequest { Unit = RoundingUnit, Mode = RoundingMode, Moment = RoundingMoment },
        SettlementPeriodicity = SettlementPeriodicity,
        SettlementDay = SettlementDay,
        Tax = new ImpuestosDeCaptacionRequest { WithholdingRateParameterCode = WithholdingRateParameterCode, GmfApplies = GmfApplies, GmfOnDeceasedAndUnclaimed = GmfOnDeceasedAndUnclaimed },
        Limits = new LimitesDeCaptacionRequest
        {
            MaxWithdrawalPerOperation = MaxWithdrawalPerOperation, MaxWithdrawalPerDay = MaxWithdrawalPerDay, MaxCashPerOperation = MaxCashPerOperation,
            MaxCashPerDay = MaxCashPerDay,
        },
        Inactivity = new InactividadDeCaptacionRequest
        {
            Days = InactivityDays, KeepsAccruing = AccruesWhileInactive, SuspendsOnTotalGarnishment = SuspendsAccrualOnTotalGarnishment,
            SuspendsOnDeceased = SuspendsAccrualOnDeceased,
        },
        TermDeposit = TermDeposit is { } c
            ? new CdatDeLaLineaRequest
            {
                MinTermDays = c.MinTermDays, MaxTermDays = c.MaxTermDays, InterestPeriodicities = [.. c.InterestPeriodicities], Timings = [.. c.Timings],
                RolloverAutomatic = c.RolloverAutomatic, MaxRollovers = c.MaxRollovers, RolloverRateRule = c.RolloverRateRule,
                RolloverCapitalizes = c.RolloverCapitalizes, GraceBusinessDays = c.GraceBusinessDays, GraceAccrues = c.GraceAccrues,
                AfterMaturity = c.AfterMaturity, DemandLinePublicId = c.DemandLinePublicId,
                EarlyRedemption = new RedencionAnticipadaRequest
                {
                    Allowed = c.EarlyRedemption.Allowed, PenaltyMode = c.EarlyRedemption.PenaltyMode, PenaltyRateEa = c.EarlyRedemption.PenaltyRateEa,
                    PenaltyDiscount = c.EarlyRedemption.PenaltyDiscount, MinDaysBeforeRedemption = c.EarlyRedemption.MinDaysBeforeRedemption,
                },
            }
            : null,
        Contractual = Contractual is { } k
            ? new ContractualDeLaLineaRequest
            {
                InstallmentPeriodicity = k.InstallmentPeriodicity, MinInstallment = k.MinInstallment, TermMonthsFrom = k.TermMonthsFrom,
                TermMonthsTo = k.TermMonthsTo, BonusRateEa = k.BonusRateEa, BreachRateEa = k.BreachRateEa, ToleranceInstallments = k.ToleranceInstallments,
                BreachEffect = k.BreachEffect, BonusRecognition = k.BonusRecognition, MaturityDestinations = k.MaturityDestinations,
            }
            : null,
        Permanent = Permanent is { } p ? new PermanenteDeLaLineaRequest { WithdrawableOnlyOnMembershipWithdrawal = p.WithdrawableOnlyOnMembershipWithdrawal } : null,
    };
}

public sealed record ValorDeAportesDto(int Kind, decimal? Amount, decimal? Fraction);

public sealed record CuotaDeAportesDto(int Kind, decimal? Amount, decimal? Fraction, decimal RoundingUnit, int RoundingMode, int Periodicity, byte? DueDay,
    bool MinimumFirst);

/// <summary>Una versión de aportes completa (<c>ContributionLineVersionDto</c>).</summary>
public sealed record VersionDeAportesDto(
    Guid PublicId,
    int VersionNumber,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    DateTime RegisteredAt,
    string Reason,
    ValorDeAportesDto MinimumNonReducible,
    CuotaDeAportesDto Installment,
    ReferenciaDeLineaDto? PermanentSavingsLine,
    decimal? PermanentSavingsFraction,
    bool AllowsExtraordinary,
    decimal? MaxParticipationFraction,
    int ExcessAction,
    bool AllowsTransferBetweenMembers,
    int RefundTermDays,
    bool LossDeductionEnabled,
    string ContentSha256)
{
    /// <summary>El pedido de una versión nueva que parte de ésta.</summary>
    public VersionDeAportesRequest ComoPedido() => new()
    {
        MinimumNonReducible = new ValorDeAportesRequest { Kind = MinimumNonReducible.Kind, Amount = MinimumNonReducible.Amount, Fraction = MinimumNonReducible.Fraction },
        Installment = new CuotaDeAportesRequest
        {
            Kind = Installment.Kind, Amount = Installment.Amount, Fraction = Installment.Fraction, RoundingUnit = Installment.RoundingUnit,
            RoundingMode = Installment.RoundingMode, Periodicity = Installment.Periodicity, DueDay = Installment.DueDay, MinimumFirst = Installment.MinimumFirst,
        },
        PermanentSavingsShare = PermanentSavingsLine is { } p && PermanentSavingsFraction is { } f
            ? new RepartoConPermanenteRequest { DepositLinePublicId = p.PublicId, Fraction = f }
            : null,
        AllowsExtraordinary = AllowsExtraordinary,
        MaxParticipationFraction = MaxParticipationFraction,
        ExcessAction = ExcessAction,
        AllowsTransferBetweenMembers = AllowsTransferBetweenMembers,
        RefundTermDays = RefundTermDays,
        LossDeductionEnabled = LossDeductionEnabled,
    };
}

/// <summary>Una línea del catálogo con su versión vigente a la fecha consultada y su historial (<c>LendingLineDto</c>).</summary>
public sealed record LineaDeCaptacionDto(
    Guid PublicId,
    string Code,
    string Name,
    int Nature,
    int? DepositClass,
    int Status,
    int? ClosedReason,
    DateTime? ClosedAt,
    string? CloseNotes,
    DateOnly Date,
    VersionDeCaptacionDto? DepositVersion,
    VersionDeAportesDto? ContributionVersion,
    List<ResumenDeVersionDelCatalogoDto> Versions);
