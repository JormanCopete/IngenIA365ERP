using System.Text.Json;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Configuration;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;

/// <summary>
/// <c>LND_DepositLineVersions</c> (feature 014, US1, T060; data-model §2.1; FR-007, FR-010, FR-028, FR-036, FR-038, FR-074): una versión de una
/// línea de captación con su fecha desde y su fecha de registro. Hecho de sólo inserción (<see cref="IHechoInmutable"/>) con sus tramos
/// (<see cref="DepositLineRateTier"/>); la corrige una versión nueva y se lee con <see cref="VigenciaBitemporal.VigenteDeCartera{T}"/>. La clase
/// (<see cref="DepositClass"/>) es igual en todas las versiones de la línea. Las cuentas a la vista, contractuales y permanentes usan cada día la
/// vigente salvo que la línea declare tasa pactada; el CDAT y el contractual sellan la suya al constituirse. Nunca pasa por la usura
/// (<c>LasCaptacionesNoUsanLaUsura</c>). Las listas (<see cref="InterestPeriodicities"/>, <see cref="InterestTimings"/>,
/// <see cref="BranchPublicIdsJson"/>) van en JSON canónico.
/// </summary>
public class DepositLineVersion : AuditableEntity, IHechoInmutable, IVersionDeCartera
{
    public int CreditLineId { get; init; }

    public int VersionNumber { get; init; }

    public DateOnly ValidFrom { get; init; }

    public DateTime RegisteredAt { get; init; }

    public string Reason { get; init; } = string.Empty;

    public DepositClass DepositClass { get; init; }

    // ------------------------------------------------------------------------------------------------ elegibilidad --

    /// <summary>FR-030: abrir y consignar exige asociado vigente; forzado a verdadero si el perfil capta sólo de asociados.</summary>
    public bool RequiresAssociate { get; init; }

    public bool AllowsCoHolders { get; init; }

    public bool AllowsMinors { get; init; }

    public bool AllowsLegalEntities { get; init; }

    /// <summary>Falso en el permanente (sólo al retiro del asociado) y en el CDAT.</summary>
    public bool AllowsPartialWithdrawals { get; init; }

    /// <summary>JSON con los <c>PublicId</c> de las oficinas que la ofrecen; nulo = todas.</summary>
    public string? BranchPublicIdsJson { get; init; }

    // -------------------------------------------------------------------------------------------------------- tasa --

    public RateKind RateKind { get; init; }

    /// <summary>EA fija o base sin tramos (fracción).</summary>
    public decimal? AnnualRate { get; init; }

    public string? IndexCode { get; init; }

    public string? IndexKey { get; init; }

    public decimal? Margin { get; init; }

    public decimal? RateFloor { get; init; }

    public decimal? RateCeiling { get; init; }

    public RepaymentPeriodicity? ResetPeriodicity { get; init; }

    /// <summary>Obligatorio con tramos; defecto <c>WholeBalance</c> (D8).</summary>
    public RateTierMode? RateTierMode { get; init; }

    /// <summary>Verdadero = la cuenta sella su tasa al abrir (FR-007); falso = usa cada día la versión vigente.</summary>
    public bool DeclaresAgreedRate { get; init; }

    public bool AllowsNegotiatedRate { get; init; }

    public bool KeepsNegotiatedMarginOnRollover { get; init; }

    /// <summary>Anticipación del aviso de cambio de tasa a los titulares (FR-038); 0 en el CDAT.</summary>
    public int RateChangeNoticeDays { get; init; }

    // ------------------------------------------------------------------------------------------------------ cálculo --

    public BalanceMethod BalanceMethod { get; init; }

    public DayCountConvention DayCount { get; init; }

    public RateEquivalence RateEquivalence { get; init; } = RateEquivalence.Compound;

    public decimal RoundingUnit { get; init; }

    public RoundingMode RoundingMode { get; init; }

    public RoundingMoment RoundingMoment { get; init; }

    public decimal MinOpeningAmount { get; init; }

    /// <summary>Mínimo para mantener: entra al cálculo del disponible (FR-057).</summary>
    public decimal MinBalance { get; init; }

    /// <summary>Bajo él, el día causa cero.</summary>
    public decimal MinBalanceForInterest { get; init; }

    public RepaymentPeriodicity SettlementPeriodicity { get; init; }

    public byte? SettlementDay { get; init; }

    // --------------------------------------------------------------------------------------------------- impuestos --

    /// <summary>Clave del parámetro de tarifa de retención de la clase (<c>Retencion.Tarifa…</c>).</summary>
    public string WithholdingRateParameterCode { get; init; } = string.Empty;

    public bool IsGmfSubject { get; init; }

    /// <summary>Respuesta 97 (E253): ¿la entrega por fallecimiento y el pago de no reclamados cobran GMF? Defecto sí; sólo muerde con <see cref="IsGmfSubject"/>.</summary>
    public bool GmfOnDeceasedAndUnclaimed { get; init; } = true;

    // ----------------------------------------------------------------------------------------------------- límites --

    public decimal? MaxWithdrawalPerOperation { get; init; }

    public decimal? MaxWithdrawalPerDay { get; init; }

    public decimal? MaxCashPerOperation { get; init; }

    public decimal? MaxCashPerDay { get; init; }

    // --------------------------------------------------------------------------------------- inactividad y suspensión --

    /// <summary>Defecto 360 (D21); nulo = no se inactiva.</summary>
    public int? InactivityDays { get; init; }

    public bool AccruesWhileInactive { get; init; }

    public bool SuspendsAccrualOnTotalGarnishment { get; init; }

    public bool SuspendsAccrualOnDeceased { get; init; }

    // -------------------------------------------------------------------------------------------------------- CDAT --

    public int? MinTermDays { get; init; }

    public int? MaxTermDays { get; init; }

    /// <summary>JSON con las <see cref="RepaymentPeriodicity"/> de pago de intereses admitidas (<c>SinglePayment</c> = al vencimiento).</summary>
    public string? InterestPeriodicities { get; init; }

    /// <summary>JSON con los <see cref="InterestTiming"/> admitidos.</summary>
    public string? InterestTimings { get; init; }

    public bool? AutoRollover { get; init; }

    public int? MaxRollovers { get; init; }

    public RolloverRateRule? RolloverRateRule { get; init; }

    public bool? CapitalizesOnRollover { get; init; }

    public int? GraceBusinessDays { get; init; }

    public bool? GraceAccrues { get; init; }

    public AfterMaturityTreatment? AfterMaturityTreatment { get; init; }

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

    /// <summary>Los destinos que la línea admite al vencer el contrato (E248); nulo = ambos (líneas anteriores a la decisión del 2026-10-21).</summary>
    public ContractualMaturityDestinations? MaturityDestinations { get; init; }

    // --------------------------------------------------------------------------------------------------- permanente --

    public bool? WithdrawableOnlyOnMembershipWithdrawal { get; init; }

    /// <summary>Huella canónica de la versión con sus tramos; la sella la cuenta.</summary>
    public string ContentSha256 { get; init; } = string.Empty;

    // ----------------------------------------------------------------------------------------------------- lectura --

    public IReadOnlyList<RepaymentPeriodicity> LeerPeriodicidadesDeInteres() => ListaDeEnums.Leer<RepaymentPeriodicity>(InterestPeriodicities);

    public IReadOnlyList<InterestTiming> LeerModalidadesDeInteres() => ListaDeEnums.Leer<InterestTiming>(InterestTimings);

    public IReadOnlyList<Guid> LeerOficinas() => string.IsNullOrWhiteSpace(BranchPublicIdsJson)
        ? []
        : JsonSerializer.Deserialize<List<Guid>>(BranchPublicIdsJson) ?? [];

    /// <summary>El redondeo de la liquidación, listo para el motor.</summary>
    public RedondeoDeCartera Redondeo() => new(RoundingUnit, RoundingMode, RoundingMoment);

    // ----------------------------------------------------------------------------------------------------- fábrica --

    /// <summary>
    /// Registra la versión <paramref name="versionNumber"/> de la línea: la valida entera con <see cref="ReglasDeLineaDeCaptacion.ValidarVersion"/>
    /// (lanza <see cref="ReglaDeCarteraException"/> si quien llama no lo hizo) y sella <see cref="ContentSha256"/>. Los tramos se arman con el Id
    /// que la base le asigne (<see cref="Tramos"/>), en la misma transacción.
    /// </summary>
    public static DepositLineVersion Registrar(int creditLineId, BorradorDeVersionDeCaptacion b, int versionNumber, DateOnly hoy, DateTime registeredAt)
    {
        ReglaDeCarteraException.SiHay(ReglasDeLineaDeCaptacion.ValidarVersion(b, hoy));
        ArgumentOutOfRangeException.ThrowIfLessThan(creditLineId, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(versionNumber, 1);
        var oficinas = Oficinas(b);
        return new DepositLineVersion
        {
            CreditLineId = creditLineId,
            VersionNumber = versionNumber,
            ValidFrom = b.ValidFrom,
            RegisteredAt = registeredAt,
            Reason = b.Reason.Trim(),
            DepositClass = b.DepositClass,
            RequiresAssociate = b.RequiresAssociate,
            AllowsCoHolders = b.AllowsCoHolders,
            AllowsMinors = b.AllowsMinors,
            AllowsLegalEntities = b.AllowsLegalEntities,
            AllowsPartialWithdrawals = b.AllowsPartialWithdrawals,
            BranchPublicIdsJson = oficinas.Count == 0 ? null : JsonSerializer.Serialize(oficinas),
            RateKind = b.RateKind,
            AnnualRate = b.AnnualRate,
            IndexCode = b.IndexCode?.Trim().ToUpperInvariant(),
            IndexKey = b.IndexKey?.Trim(),
            Margin = b.Margin,
            RateFloor = b.RateFloor,
            RateCeiling = b.RateCeiling,
            ResetPeriodicity = b.ResetPeriodicity,
            RateTierMode = b.ModoDeTramosEfectivo,
            DeclaresAgreedRate = b.DeclaresAgreedRate,
            AllowsNegotiatedRate = b.AllowsNegotiatedRate,
            KeepsNegotiatedMarginOnRollover = b.KeepsNegotiatedMarginOnRollover,
            RateChangeNoticeDays = b.RateChangeNoticeDays,
            BalanceMethod = b.BalanceMethod,
            DayCount = b.DayCount,
            RateEquivalence = b.RateEquivalence,
            RoundingUnit = b.RoundingUnit,
            RoundingMode = b.RoundingMode,
            RoundingMoment = b.RoundingMoment,
            MinOpeningAmount = b.MinOpeningAmount,
            MinBalance = b.MinBalance,
            MinBalanceForInterest = b.MinBalanceForInterest,
            SettlementPeriodicity = b.SettlementPeriodicity,
            SettlementDay = b.SettlementDay,
            WithholdingRateParameterCode = ReglasDeLineaDeCaptacion.TarifaDeRetencionDe(b),
            IsGmfSubject = b.IsGmfSubject,
            GmfOnDeceasedAndUnclaimed = b.GmfOnDeceasedAndUnclaimed,
            MaxWithdrawalPerOperation = b.MaxWithdrawalPerOperation,
            MaxWithdrawalPerDay = b.MaxWithdrawalPerDay,
            MaxCashPerOperation = b.MaxCashPerOperation,
            MaxCashPerDay = b.MaxCashPerDay,
            InactivityDays = b.InactivityDays,
            AccruesWhileInactive = b.AccruesWhileInactive,
            SuspendsAccrualOnTotalGarnishment = b.SuspendsAccrualOnTotalGarnishment,
            SuspendsAccrualOnDeceased = b.SuspendsAccrualOnDeceased,
            MinTermDays = b.MinTermDays,
            MaxTermDays = b.MaxTermDays,
            InterestPeriodicities = b.InterestPeriodicities.Count == 0 ? null : ListaDeEnums.Conjunto(b.InterestPeriodicities),
            InterestTimings = b.InterestTimings.Count == 0 ? null : ListaDeEnums.Conjunto(b.InterestTimings),
            AutoRollover = b.AutoRollover,
            MaxRollovers = b.MaxRollovers,
            RolloverRateRule = b.RolloverRateRule,
            CapitalizesOnRollover = b.CapitalizesOnRollover,
            GraceBusinessDays = b.GraceBusinessDays,
            GraceAccrues = b.GraceAccrues,
            AfterMaturityTreatment = b.AfterMaturityTreatment,
            MoveToDemandLineId = b.MoveToDemandLineId,
            EarlyRedemptionAllowed = b.EarlyRedemptionAllowed,
            EarlyRedemptionPenaltyMode = b.EarlyRedemptionPenaltyMode,
            PenaltyAnnualRate = b.PenaltyAnnualRate,
            PenaltyDiscountFraction = b.PenaltyDiscountFraction,
            MinDaysBeforeRedemption = b.MinDaysBeforeRedemption,
            InstallmentPeriodicity = b.InstallmentPeriodicity,
            MinInstallmentAmount = b.MinInstallmentAmount,
            MinTermMonths = b.MinTermMonths,
            MaxTermMonths = b.MaxTermMonths,
            BonusAnnualRate = b.BonusAnnualRate,
            BreachAnnualRate = b.BreachAnnualRate,
            ToleratedLateInstallments = b.ToleratedLateInstallments,
            BreachEffect = b.BreachEffect,
            BonusRecognition = b.BonusRecognition,
            MaturityDestinations = b.MaturityDestinations,
            WithdrawableOnlyOnMembershipWithdrawal = b.WithdrawableOnlyOnMembershipWithdrawal,
            ContentSha256 = Huella(b),
        };
    }

    /// <summary>Los tramos de la versión ya guardada (con su Id), en orden.</summary>
    public static IEnumerable<DepositLineRateTier> Tramos(int depositLineVersionId, BorradorDeVersionDeCaptacion b) =>
        b.RateTiers.OrderBy(t => t.Order).Select(t => DepositLineRateTier.Nuevo(depositLineVersionId, t));

    /// <summary>
    /// La huella canónica de la versión con sus tramos: todo su contenido en orden fijo (las oficinas ordenadas, los tramos por orden). El motivo y
    /// el número de versión no son contenido. La sella la cuenta.
    /// </summary>
    public static string Huella(BorradorDeVersionDeCaptacion b) =>
        new HuellaDeCartera()
            .Fecha("validFrom", b.ValidFrom)
            .Enumeracion("depositClass", (DepositClass?)b.DepositClass)
            .Logico("requiresAssociate", b.RequiresAssociate)
            .Logico("allowsCoHolders", b.AllowsCoHolders)
            .Logico("allowsMinors", b.AllowsMinors)
            .Logico("allowsLegalEntities", b.AllowsLegalEntities)
            .Logico("allowsPartialWithdrawals", b.AllowsPartialWithdrawals)
            .Textos("branchPublicIds", Oficinas(b).Select(o => o.ToString("N")))
            .Enumeracion("rateKind", (RateKind?)b.RateKind)
            .Decimal("annualRate", b.AnnualRate, 6)
            .Texto("indexCode", b.IndexCode?.Trim().ToUpperInvariant())
            .Texto("indexKey", b.IndexKey?.Trim())
            .Decimal("margin", b.Margin, 6)
            .Decimal("rateFloor", b.RateFloor, 6)
            .Decimal("rateCeiling", b.RateCeiling, 6)
            .Enumeracion("resetPeriodicity", b.ResetPeriodicity)
            .Enumeracion("rateTierMode", b.ModoDeTramosEfectivo)
            .Logico("declaresAgreedRate", b.DeclaresAgreedRate)
            .Logico("allowsNegotiatedRate", b.AllowsNegotiatedRate)
            .Logico("keepsNegotiatedMarginOnRollover", b.KeepsNegotiatedMarginOnRollover)
            .Entero("rateChangeNoticeDays", b.RateChangeNoticeDays)
            .Enumeracion("balanceMethod", (BalanceMethod?)b.BalanceMethod)
            .Enumeracion("dayCount", (DayCountConvention?)b.DayCount)
            .Enumeracion("rateEquivalence", (RateEquivalence?)b.RateEquivalence)
            .Decimal("roundingUnit", b.RoundingUnit, 2)
            .Enumeracion("roundingMode", (RoundingMode?)b.RoundingMode)
            .Enumeracion("roundingMoment", (RoundingMoment?)b.RoundingMoment)
            .Decimal("minOpeningAmount", b.MinOpeningAmount, 2)
            .Decimal("minBalance", b.MinBalance, 2)
            .Decimal("minBalanceForInterest", b.MinBalanceForInterest, 2)
            .Enumeracion("settlementPeriodicity", (RepaymentPeriodicity?)b.SettlementPeriodicity)
            .Entero("settlementDay", b.SettlementDay)
            .Texto("withholdingRateParameterCode", ReglasDeLineaDeCaptacion.TarifaDeRetencionDe(b))
            .Logico("isGmfSubject", b.IsGmfSubject)
            .Logico("gmfOnDeceasedAndUnclaimed", b.GmfOnDeceasedAndUnclaimed)
            .Decimal("maxWithdrawalPerOperation", b.MaxWithdrawalPerOperation, 2)
            .Decimal("maxWithdrawalPerDay", b.MaxWithdrawalPerDay, 2)
            .Decimal("maxCashPerOperation", b.MaxCashPerOperation, 2)
            .Decimal("maxCashPerDay", b.MaxCashPerDay, 2)
            .Entero("inactivityDays", b.InactivityDays)
            .Logico("accruesWhileInactive", b.AccruesWhileInactive)
            .Logico("suspendsAccrualOnTotalGarnishment", b.SuspendsAccrualOnTotalGarnishment)
            .Logico("suspendsAccrualOnDeceased", b.SuspendsAccrualOnDeceased)
            .Entero("minTermDays", b.MinTermDays)
            .Entero("maxTermDays", b.MaxTermDays)
            .Texto("interestPeriodicities", b.InterestPeriodicities.Count == 0 ? null : ListaDeEnums.Conjunto(b.InterestPeriodicities))
            .Texto("interestTimings", b.InterestTimings.Count == 0 ? null : ListaDeEnums.Conjunto(b.InterestTimings))
            .Logico("autoRollover", b.AutoRollover)
            .Entero("maxRollovers", b.MaxRollovers)
            .Enumeracion("rolloverRateRule", b.RolloverRateRule)
            .Logico("capitalizesOnRollover", b.CapitalizesOnRollover)
            .Entero("graceBusinessDays", b.GraceBusinessDays)
            .Logico("graceAccrues", b.GraceAccrues)
            .Enumeracion("afterMaturityTreatment", b.AfterMaturityTreatment)
            .Entero("moveToDemandLineId", b.MoveToDemandLineId)
            .Logico("earlyRedemptionAllowed", b.EarlyRedemptionAllowed)
            .Enumeracion("earlyRedemptionPenaltyMode", b.EarlyRedemptionPenaltyMode)
            .Decimal("penaltyAnnualRate", b.PenaltyAnnualRate, 6)
            .Decimal("penaltyDiscountFraction", b.PenaltyDiscountFraction, 6)
            .Entero("minDaysBeforeRedemption", b.MinDaysBeforeRedemption)
            .Enumeracion("installmentPeriodicity", b.InstallmentPeriodicity)
            .Decimal("minInstallmentAmount", b.MinInstallmentAmount, 2)
            .Entero("minTermMonths", b.MinTermMonths)
            .Entero("maxTermMonths", b.MaxTermMonths)
            .Decimal("bonusAnnualRate", b.BonusAnnualRate, 6)
            .Decimal("breachAnnualRate", b.BreachAnnualRate, 6)
            .Entero("toleratedLateInstallments", b.ToleratedLateInstallments)
            .Enumeracion("breachEffect", b.BreachEffect)
            .Enumeracion("bonusRecognition", b.BonusRecognition)
            .Enumeracion("maturityDestinations", b.MaturityDestinations)
            .Logico("withdrawableOnlyOnMembershipWithdrawal", b.WithdrawableOnlyOnMembershipWithdrawal)
            .Lista("rateTiers", b.RateTiers.OrderBy(t => t.Order), (h, t) => h
                .Entero("order", t.Order)
                .Decimal("minBalance", t.MinBalance, 2)
                .Decimal("maxBalance", t.MaxBalance, 2)
                .Entero("minTermDays", t.MinTermDays)
                .Entero("maxTermDays", t.MaxTermDays)
                .Decimal("minAmount", t.MinAmount, 2)
                .Decimal("maxAmount", t.MaxAmount, 2)
                .Decimal("annualRate", t.AnnualRate, 6)
                .Decimal("margin", t.Margin, 6))
            // US4 (T114): los cargos entran a la huella sólo si hay (las versiones sin cargos conservan la huella con que se sellaron).
            .Si(b.Charges.Count > 0, h => h.Lista("charges", b.Charges.OrderBy(c => c.Code.Trim().ToUpperInvariant(), StringComparer.Ordinal), (x, c) => x
                .Texto("code", c.Code.Trim().ToUpperInvariant())
                .Texto("name", c.Name.Trim())
                .Enumeracion("kind", (ChargeKind?)c.Kind)
                .Logico("isEnabled", c.IsEnabled)
                .Enumeracion("trigger", (ChargeTrigger?)c.Trigger)
                .Enumeracion("periodicity", c.Periodicity)
                .Entero("freeOperationsPerPeriod", c.FreeOperationsPerPeriod)
                .Enumeracion("freePeriod", c.FreePeriod)
                .Enumeracion("calculationMode", (ChargeCalculation?)c.CalculationMode)
                .Decimal("fixedAmount", c.FixedAmount, 2)
                .Decimal("percentage", c.Percentage, 6)
                .Enumeracion("percentageBase", c.PercentageBase)
                .Decimal("minAmount", c.MinAmount, 2)
                .Decimal("maxAmount", c.MaxAmount, 2)
                .Decimal("exemptIfAverageBalanceAtLeast", c.ExemptIfAverageBalanceAtLeast, 2)
                .Decimal("exemptIfMinimumBalanceAtLeast", c.ExemptIfMinimumBalanceAtLeast, 2)
                .Entero("exemptIfSeniorityMonthsAtLeast", c.ExemptIfSeniorityMonthsAtLeast)
                .Logico("exemptEmployees", c.ExemptEmployees)
                .Logico("exemptGmfExemptAccount", c.ExemptGmfExemptAccount)
                .Logico("exemptMinors", c.ExemptMinors)
                .Textos("exemptBranchPublicIds", c.ExemptBranchPublicIds.Distinct().Order().Select(o => o.ToString("N")))
                .Enumeracion("shortfallPolicy", (ChargeShortfallPolicy?)c.ShortfallPolicy)
                .Logico("isVatTaxed", c.IsVatTaxed)
                .Texto("commercialItemCode", string.IsNullOrWhiteSpace(c.CommercialItemCode) ? null : c.CommercialItemCode.Trim())))
            .Calcular();

    /// <summary>US4 (T114): los cargos de la versión ya guardada (con su Id), por código.</summary>
    public static IEnumerable<DepositLineCharge> Cargos(int depositLineVersionId, BorradorDeVersionDeCaptacion b) =>
        b.Charges.OrderBy(c => c.Code.Trim().ToUpperInvariant(), StringComparer.Ordinal).Select(c => DepositLineCharge.Nuevo(depositLineVersionId, c));

    /// <summary>La huella de esta versión ya guardada con sus tramos (y sus cargos), para verificar que nadie la cambió.</summary>
    public string HuellaDeLoGuardado(IEnumerable<DepositLineRateTier> tramos, IEnumerable<DepositLineCharge>? cargos = null) =>
        Huella(BorradorDeVersionDeCaptacion.De(this, tramos, cargos));

    private static List<Guid> Oficinas(BorradorDeVersionDeCaptacion b) => b.BranchPublicIds.Distinct().Order().ToList();
}
