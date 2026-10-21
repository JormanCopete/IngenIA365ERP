using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;

/// <summary>
/// <c>LND_ContributionLineVersions</c> (feature 014, US1, T062; data-model §2.5; FR-029, FR-040 a FR-049): una versión de la línea de aportes con
/// su fecha desde y su fecha de registro. Hecho de sólo inserción (<see cref="IHechoInmutable"/>) leído con
/// <see cref="VigenciaBitemporal.VigenteDeCartera{T}"/>. <b>No tiene tasa</b>: los aportes no causan intereses (R02). Dice el mínimo no reducible
/// por asociado, la cuota periódica (valor fijo, fracción del ingreso o del salario mínimo) con su redondeo y periodicidad, el reparto con el
/// ahorro permanente, los extraordinarios y el límite de participación, y el plazo estatutario de devolución.
/// </summary>
public class ContributionLineVersion : AuditableEntity, IHechoInmutable, IVersionDeCartera
{
    public int CreditLineId { get; init; }

    public int VersionNumber { get; init; }

    public DateOnly ValidFrom { get; init; }

    public DateTime RegisteredAt { get; init; }

    public string Reason { get; init; } = string.Empty;

    // --------------------------------------------------------------------------------------------- mínimo no reducible --

    public ContributionAmountMode MinimumMode { get; init; }

    public decimal? MinimumAmount { get; init; }

    /// <summary>Del ingreso registrado de la persona o del <c>SMMLV</c> vigente, según el modo.</summary>
    public decimal? MinimumFraction { get; init; }

    // ---------------------------------------------------------------------------------------------------- cuota periódica --

    public ContributionAmountMode InstallmentMode { get; init; }

    public decimal? InstallmentAmount { get; init; }

    public decimal? InstallmentFraction { get; init; }

    public decimal InstallmentRoundingUnit { get; init; }

    public RoundingMode InstallmentRoundingMode { get; init; }

    public RepaymentPeriodicity InstallmentPeriodicity { get; init; }

    public byte? InstallmentDueDay { get; init; }

    /// <summary>Lo pagado completa el mínimo antes de ir a ordinarios.</summary>
    public bool MinimumFirst { get; init; }

    // ----------------------------------------------------------------------------------------- reparto con ahorro permanente --

    /// <summary>La línea de captación de clase permanente que recibe su parte de la cuota (FR-049, D3).</summary>
    public int? PermanentSavingsLineId { get; init; }

    /// <summary>La parte de la cuota que va a ahorro permanente; la de aportes no baja de la fracción mínima legal.</summary>
    public decimal? PermanentSavingsFraction { get; init; }

    // ------------------------------------------------------------------------------------------- extraordinarios y límites --

    public bool AllowsExtraordinary { get; init; }

    /// <summary>Límite propio de participación, nunca menos estricto que el legal (FR-043).</summary>
    public decimal? MaxParticipationFraction { get; init; }

    public ExcessContributionAction ExcessAction { get; init; }

    public bool AllowsTransferBetweenMembers { get; init; }

    // ---------------------------------------------------------------------------------------------------------- devolución --

    /// <summary>Plazo estatutario de devolución en días (FR-079).</summary>
    public int RefundTermDays { get; init; }

    /// <summary>Descuenta pérdidas al devolver (D15, FR-078); falso por defecto.</summary>
    public bool LossDeductionEnabled { get; init; }

    public string ContentSha256 { get; init; } = string.Empty;

    /// <summary>
    /// Registra la versión <paramref name="versionNumber"/> de la línea de aportes: la valida con
    /// <see cref="ReglasDeLineaDeCaptacion.ValidarVersionDeAportes"/> (lanza si quien llama no lo hizo) y sella <see cref="ContentSha256"/>.
    /// </summary>
    public static ContributionLineVersion Registrar(int creditLineId, BorradorDeVersionDeAportes b, int versionNumber, DateOnly hoy, DateTime registeredAt)
    {
        ReglaDeCarteraException.SiHay(ReglasDeLineaDeCaptacion.ValidarVersionDeAportes(b, hoy));
        ArgumentOutOfRangeException.ThrowIfLessThan(creditLineId, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(versionNumber, 1);
        return new ContributionLineVersion
        {
            CreditLineId = creditLineId,
            VersionNumber = versionNumber,
            ValidFrom = b.ValidFrom,
            RegisteredAt = registeredAt,
            Reason = b.Reason.Trim(),
            MinimumMode = b.MinimumMode,
            MinimumAmount = b.MinimumAmount,
            MinimumFraction = b.MinimumFraction,
            InstallmentMode = b.InstallmentMode,
            InstallmentAmount = b.InstallmentAmount,
            InstallmentFraction = b.InstallmentFraction,
            InstallmentRoundingUnit = b.InstallmentRoundingUnit,
            InstallmentRoundingMode = b.InstallmentRoundingMode,
            InstallmentPeriodicity = b.InstallmentPeriodicity,
            InstallmentDueDay = b.InstallmentDueDay,
            MinimumFirst = b.MinimumFirst,
            PermanentSavingsLineId = b.PermanentSavingsLineId,
            PermanentSavingsFraction = b.PermanentSavingsFraction,
            AllowsExtraordinary = b.AllowsExtraordinary,
            MaxParticipationFraction = b.MaxParticipationFraction,
            ExcessAction = b.ExcessAction,
            AllowsTransferBetweenMembers = b.AllowsTransferBetweenMembers,
            RefundTermDays = b.RefundTermDays,
            LossDeductionEnabled = b.LossDeductionEnabled,
            ContentSha256 = Huella(b),
        };
    }

    /// <summary>La huella canónica de la versión (el motivo y el número no son contenido).</summary>
    public static string Huella(BorradorDeVersionDeAportes b) =>
        new HuellaDeCartera()
            .Fecha("validFrom", b.ValidFrom)
            .Enumeracion("minimumMode", (ContributionAmountMode?)b.MinimumMode)
            .Decimal("minimumAmount", b.MinimumAmount, 2)
            .Decimal("minimumFraction", b.MinimumFraction, 6)
            .Enumeracion("installmentMode", (ContributionAmountMode?)b.InstallmentMode)
            .Decimal("installmentAmount", b.InstallmentAmount, 2)
            .Decimal("installmentFraction", b.InstallmentFraction, 6)
            .Decimal("installmentRoundingUnit", b.InstallmentRoundingUnit, 2)
            .Enumeracion("installmentRoundingMode", (RoundingMode?)b.InstallmentRoundingMode)
            .Enumeracion("installmentPeriodicity", (RepaymentPeriodicity?)b.InstallmentPeriodicity)
            .Entero("installmentDueDay", b.InstallmentDueDay)
            .Logico("minimumFirst", b.MinimumFirst)
            .Entero("permanentSavingsLineId", b.PermanentSavingsLineId)
            .Decimal("permanentSavingsFraction", b.PermanentSavingsFraction, 6)
            .Logico("allowsExtraordinary", b.AllowsExtraordinary)
            .Decimal("maxParticipationFraction", b.MaxParticipationFraction, 6)
            .Enumeracion("excessAction", (ExcessContributionAction?)b.ExcessAction)
            .Logico("allowsTransferBetweenMembers", b.AllowsTransferBetweenMembers)
            .Entero("refundTermDays", b.RefundTermDays)
            .Logico("lossDeductionEnabled", b.LossDeductionEnabled)
            .Calcular();

    /// <summary>La huella de esta versión ya guardada, para verificar que nadie la cambió.</summary>
    public string HuellaDeLoGuardado() => Huella(BorradorDeVersionDeAportes.De(this));
}
