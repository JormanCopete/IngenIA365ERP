using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Entities.Lending.Payroll;

/// <summary>
/// <c>LND_PayrollDeductionAccountResults</c> (feature 014, T091; D36; mensajes.md §5.7): lo que tocó a una cuota de cuenta del archivo al repartir lo que la
/// pagaduría informó por su código en <c>ResultadoDeDescuentos</c> v2. Lo descontado queda <b>pendiente de giro</b> mientras ningún resultado posterior lo
/// reemplace y ninguna consignación vigente lo nombre (<c>LND_MemberAccountOperations.PayrollDeductionAccountResultId</c>); lo no descontado queda como no
/// descontado de la cuota (sigue exigible). Hecho de sólo inserción. (nuevo)
/// </summary>
public class PayrollDeductionAccountResult : AuditableEntity, IHechoInmutable
{
    public int PayrollDeductionFileAccountLineId { get; init; }

    public int PayrollDeductionFileId { get; init; }

    public int MemberAccountId { get; init; }

    /// <summary>El recibo del <c>ResultadoDeDescuentos</c>; único con la línea.</summary>
    public int InboundMessageId { get; init; }

    public PayrollResultOutcome Outcome { get; init; }

    public decimal DeductedAmount { get; init; }

    public decimal NotDeductedAmount { get; init; }

    /// <summary>Fecha del descuento = fecha valor de la consignación que lo gire.</summary>
    public DateOnly? DeductionDate { get; init; }

    public string? PaymasterResultCode { get; init; }

    public NonDeductionReason? NonDeductionReason { get; init; }

    /// <summary>Vencimiento de la ventana de giro, sellado.</summary>
    public DateOnly? TransferDueDate { get; init; }

    /// <summary>El resultado anterior de la misma línea que éste reemplaza.</summary>
    public int? SupersedesResultId { get; init; }
}
