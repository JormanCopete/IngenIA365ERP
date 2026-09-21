using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_SettlementDeductions]. Un descuento propuesto en la definitiva (feature
/// 010, FR-018a): el saldo de un préstamo de la cooperativa leído de Cartera, las cuotas
/// causadas y no descontadas de una libranza, u otro. La responsable sólo puede <b>bajarlo</b>
/// sobre lo propuesto, nunca subirlo, y bajarlo exige motivo; cada cambio queda en auditoría
/// (<c>Payroll.Settlement.DeductionAdjusted</c>). Al aprobar se aplica en Cartera por
/// <c>ProcessPaymentCommand</c> y aquí queda el recaudo; al reversar, <c>Reverted</c>.
///
/// <para>
/// Las referencias a Cartera (<see cref="LoanPortfolioId"/>) y a la libranza
/// (<see cref="RecurringNoveltyId"/>) son claves sin navegación: la FK se declara en la
/// configuración para que el modelo de pruebas en memoria no arrastre el módulo de Cartera.
/// </para>
/// </summary>
public class SettlementDeduction : AuditableEntity
{
    public int TerminationId { get; set; }

    public SettlementDeductionKind Kind { get; set; }

    /// <summary><c>CooperativeLoan</c>: FK <c>LND_LoanPortfolios</c>.</summary>
    public int? LoanPortfolioId { get; set; }

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

    /// <summary>El recaudo que dejó <c>ProcessPaymentCommand</c> al aprobar.</summary>
    public Guid? CarteraTransactionPublicId { get; set; }

    /// <summary><c>PaymentResultDto.Remaining</c> tras aplicar.</summary>
    public decimal? RemainingBalanceAfter { get; set; }

    public EmploymentTermination? Termination { get; set; }

    public bool FueAjustado => AppliedAmount < ProposedAmount;
}
