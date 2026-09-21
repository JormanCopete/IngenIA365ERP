using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;

namespace IngenIA365ERP.Domain.Entities.Payroll;

/// <summary>
/// Maps to [dbo].[PAY_SeveranceFundDeposits]. La consignación de las cesantías anuales a un
/// fondo (feature 010, FR-012): por cada corrida <c>Severance</c> aprobada y cada fondo, la fecha
/// en que se consignó, quién lo marcó y la referencia del pago. Es lo que la pantalla muestra
/// como «consignado el …» en la relación por fondo, y lo que el aviso de la fecha límite
/// (<c>CESANTIAS_FECHA_LIMITE_CONSIGNACION</c>) mira para callarse. Una por
/// <c>(PayrollRunId, SeveranceFundId)</c>; volver a marcar responde
/// <c>Payroll.Severance.AlreadyDeposited</c>. La reversión de la corrida la deja con la corrida,
/// como historial: la consignación ocurrió aunque el asiento se haya reversado.
/// </summary>
public class SeveranceFundDeposit : AuditableEntity
{
    public int PayrollRunId { get; set; }

    /// <summary>Fila de <c>PAY_SeveranceProviders</c>.</summary>
    public int SeveranceFundId { get; set; }

    /// <summary>Fecha de la consignación, digitada al marcar.</summary>
    public DateOnly DepositedAt { get; set; }

    [MaxLength(100)]
    public string DepositedBy { get; set; } = string.Empty;

    /// <summary>Referencia del pago o de la planilla del fondo.</summary>
    [MaxLength(60)]
    public string? Reference { get; set; }

    /// <summary>Lo consignado, como quedó en la relación por fondo al marcar.</summary>
    public decimal Amount { get; set; }

    public PayrollRun? PayrollRun { get; set; }
    public SeveranceProvider? SeveranceFund { get; set; }
}
