using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_SettlementDeductions]. Un descuento propuesto en la definitiva (feature
/// 010, FR-018a): el saldo de un préstamo de la cooperativa leído de Cartera, las cuotas
/// causadas y no descontadas de una libranza, u otro. La responsable sólo puede <b>bajarlo</b>
/// sobre lo propuesto, nunca subirlo, y bajarlo exige motivo; cada cambio queda en auditoría
/// (<c>Payroll.Settlement.DeductionAdjusted</c>). Al aprobar se aplica en Cartera por el contrato de
/// obligaciones (feature 013, T92) y aquí queda el comprobante; al reversar se reversa por el contrato y queda <c>Reverted</c>.
///
/// <para>
/// Las referencias a Cartera (<see cref="ObligationPublicId"/>; el heredado <see cref="LoanPortfolioId"/>) y a la libranza
/// (<see cref="RecurringNoveltyId"/>) son claves sin navegación: la FK se declara en la
/// configuración para que el modelo de pruebas en memoria no arrastre el módulo de Cartera.
/// </para>
/// </summary>
public class SettlementDeduction : AuditableEntity
{
    public int TerminationId { get; set; }

    public SettlementDeductionKind Kind { get; set; }

    /// <summary>
    /// <c>CooperativeLoan</c> heredado: FK <c>LND_LoanPortfolios</c>. Feature 013 (T0156, CH37): se conserva hasta
    /// <c>RetiroDeLaCarteraHeredada</c>, pero ningún código nuevo la lee; la obligación es <see cref="ObligationPublicId"/>.
    /// </summary>
    public int? LoanPortfolioId { get; set; }

    /// <summary>
    /// Feature 013 (T0156, data-model parte 3 §7.2): la obligación de Cartera que el descuento paga, sin FK (FR-008). Única por
    /// terminación entre las filas vivas (<c>UK_PAY_SettlementDeductions_Termination_Obligation</c>).
    /// </summary>
    public Guid? ObligationPublicId { get; set; }

    /// <summary>Feature 013: copia del número visible de la obligación.</summary>
    [MaxLength(30)]
    public string? ObligationNumber { get; set; }

    /// <summary>Feature 013 (R105): la operación de reverso que devolvió el contrato al reversar la definitiva.</summary>
    public Guid? CarteraReversalPublicId { get; set; }

    /// <summary><c>ThirdPartyLibranza</c>: FK <c>PAY_RecurringNovelties</c>.</summary>
    public int? RecurringNoveltyId { get; set; }

    /// <summary>Número de obligación, tercero.</summary>
    [MaxLength(200)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Saldo total (préstamo) o cuotas causadas no descontadas (libranza).</summary>
    public decimal ProposedAmount { get; set; }

    /// <summary>JSON <c>{ capital, intereses, mora, cuotasPendientes, cuotasCausadas }</c> leído de Cartera al proponer.</summary>
    public string? ProposedBreakdownJson { get; set; }

    /// <summary><c>0 ≤ Applied ≤ Proposed</c>.</summary>
    public decimal AppliedAmount { get; set; }

    /// <summary>Obligatorio si <c>Applied &lt; Proposed</c>.</summary>
    [MaxLength(300)]
    public string? AdjustmentReason { get; set; }

    [MaxLength(100)]
    public string? AdjustedBy { get; set; }
    public DateTime? AdjustedAt { get; set; }

    public SettlementDeductionStatus Status { get; set; } = SettlementDeductionStatus.Proposed;

    /// <summary>El comprobante de aplicación que devolvió el contrato de obligaciones al aprobar (feature 013, T92).</summary>
    public Guid? CarteraTransactionPublicId { get; set; }

    /// <summary>El saldo para cancelar que queda en Cartera tras aplicar (<c>BalancesAfter.PayoffAmount</c>), o el que quedaría según la simulación del ajuste.</summary>
    public decimal? RemainingBalanceAfter { get; set; }

    public EmploymentTermination? Termination { get; set; }

    public bool FueAjustado => AppliedAmount < ProposedAmount;
}
