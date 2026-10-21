using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Deposits.Projections;

/// <summary>
/// <c>LND_MemberAccountDailyPositions</c> (feature 014, K7; data-model §4.3; I-6, FR-094, FR-109): instantánea versionada por cuenta y día.
/// Alimenta la consulta de aportes, los informes, los saldos diarios y la liquidez; <b>nunca es la autoridad</b> (lo es el libro). Hecho de
/// sólo inserción (<see cref="IHechoInmutable"/>, <c>bigint</c>): una operación con fecha valor anterior agrega una <see cref="Version"/> nueva
/// por día cambiado; «a la fecha con información hasta R» toma la versión mayor con <see cref="RegisteredAt"/> ≤ R. La escribe sólo
/// <c>RegistroDeCuentas</c>. Sin diferencias de auditoría (§0.8): el evento resumen es el de la tanda.
/// </summary>
[SinDiffDeAuditoria]
public class MemberAccountDailyPosition : AuditableEntityLong, IHechoInmutable
{
    public int MemberAccountId { get; init; }

    /// <summary>Navegación para nacer en el mismo guardado que la cuenta.</summary>
    public Accounts.MemberAccount? MemberAccount { get; init; }

    public DateOnly PositionDate { get; init; }

    public int Version { get; init; }

    /// <summary>R de la instantánea.</summary>
    public DateTime RegisteredAt { get; init; }

    // ------------------------------------------------------------------------- dimensiones de alcance e informe --

    public MemberAccountStatus Status { get; init; }

    public int BranchId { get; init; }

    public int CreditLineId { get; init; }

    public LineNature Nature { get; init; }

    public DepositClass? DepositClass { get; init; }

    public IssueTermBand? IssueTermBand { get; init; }

    // -------------------------------------------------------------------------------- saldos por concepto --

    public decimal Principal { get; init; }

    public decimal ContributionMinimum { get; init; }

    public decimal ContributionOrdinary { get; init; }

    public decimal ContributionExtraordinary { get; init; }

    public decimal ContributionRevaluation { get; init; }

    public decimal Bonus { get; init; }

    public decimal ChargeReceivable { get; init; }

    /// <summary>Interés causado por pagar, hasta 6 decimales (alias <c>MontoCausado</c>).</summary>
    public decimal InterestPayable { get; init; }

    // ------------------------------------------------------------------------------ disponible desglosado (FR-057) --

    public decimal HeldAmount { get; init; }

    public decimal GarnishedAmount { get; init; }

    public decimal ClearingAmount { get; init; }

    public decimal MinimumBalance { get; init; }

    public decimal AvailableBalance { get; init; }

    // ---------------------------------------------------------------------------------------------- memoria --

    public decimal WithholdingYearToDate { get; init; }

    public decimal GmfMonthToDate { get; init; }

    public decimal? AppliedAnnualRate { get; init; }

    /// <summary>CDAT: madurez residual (la usa el fondo de liquidez).</summary>
    public int? DaysToMaturity { get; init; }

    /// <summary>Cuotas exigibles vencidas y no pagadas (FR-094).</summary>
    public int InstallmentsInArrears { get; init; }

    public decimal ArrearsAmount { get; init; }

    public bool IsInactive { get; init; }

    public bool IsGarnished { get; init; }

    public bool IsGmfExempt { get; init; }

    public bool IsAccrualSuspended { get; init; }

    /// <summary>Huella de la reconstrucción.</summary>
    public string InputsSha256 { get; init; } = string.Empty;
}
