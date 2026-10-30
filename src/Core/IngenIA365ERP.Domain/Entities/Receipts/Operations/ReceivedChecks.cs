using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Domain.Entities.Receipts.Operations;

/// <summary>
/// <c>RCD_ReceivedChecks</c> (feature 015, US15, T0216; data-model §4.5; FR-083 a FR-087): el cheque recibido en caja como medio de un recibo o en custodia (posfechado,
/// sin recibo), con el banco librado, la cuenta, el número, el girador, la fecha y el valor. UK <c>(DraweeBankId, AccountNumber, CheckNumber)</c> sin filtro: el mismo
/// cheque no entra dos veces. El estado avanza por las transiciones manuales (consignado, en canje, pagado) o por la devolución, cada una con su evento. (nuevo)
/// </summary>
public class ReceivedCheck : AuditableEntity
{
    public int DraweeBankId { get; set; }

    public string CheckNumber { get; set; } = string.Empty;

    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>El girador (persona); nulo si no se registró como persona.</summary>
    public int? DrawerPersonId { get; set; }

    public string? DrawerName { get; set; }

    /// <summary>Quien lo entregó en caja (el pagador del recibo).</summary>
    public int PayerPersonId { get; set; }

    public DateOnly IssueDate { get; set; }

    public decimal Amount { get; set; }

    public bool IsThirdParty { get; set; }

    public bool SamePlace { get; set; }

    /// <summary>El último día del plazo de presentación (C. Co. art. 718); informativo salvo <c>Cheques.BloquearFueraDePlazo</c>.</summary>
    public DateOnly PresentationDeadline { get; set; }

    public int BranchId { get; set; }

    /// <summary>El recibo que pagó (nulo en custodia).</summary>
    public long? ReceiptId { get; set; }

    public ReceivedCheckState Status { get; set; } = ReceivedCheckState.Received;

    public string? ReturnCauseCode { get; set; }

    public Guid? RemittancePublicId { get; set; }

    /// <summary>La cuenta por cobrar que dejó la devolución si ya se había entregado dinero (FR-060).</summary>
    public int? PayerReceivableId { get; set; }

    /// <summary>Cheque en canje cargado por la puesta en marcha (<c>recaudo.cheques-en-canje</c>, US13): sin recibo ni hecho contable. (nuevo)</summary>
    public bool IsOpeningLoad { get; set; }

    /// <summary>R2 (fase 37; FR-084): el lote de canje en que va y la línea del extracto que lo pagó (por su <c>PublicId</c>). (nuevo)</summary>
    public int? ClearingBatchId { get; set; }

    public Guid? PaidByStatementLinePublicId { get; set; }
}

/// <summary><c>RCD_ReceivedCheckEvents</c> (H): cada transición del cheque con quién, cuándo, la causa y el soporte. (nuevo)</summary>
public class ReceivedCheckEvent : AuditableEntity, IHechoInmutable
{
    public int ReceivedCheckId { get; set; }

    public ReceivedCheckState FromStatus { get; set; }

    public ReceivedCheckState ToStatus { get; set; }

    public int ByUserId { get; set; }

    public DateTime At { get; set; }

    public string? CauseCode { get; set; }

    public Guid? SupportAttachmentPublicId { get; set; }

    public string? Note { get; set; }
}

/// <summary><c>RCD_CheckReturnCauses</c>: el catálogo de causales de devolución (código del banco y descripción), sembrado «pendiente de validar». (nuevo)</summary>
public class CheckReturnCause : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>La causal es de fondos (insuficientes, cuenta cancelada): cuenta para la lista de giradores con devoluciones.</summary>
    public bool CountsForDrawerList { get; set; } = true;

    public bool IsActive { get; set; } = true;
}
