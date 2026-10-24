using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Domain.Entities.Receipts.Banks;

// Feature 015, R1, US6 (T0378; FR-067, FR-069; D11, D15; data-model §8.1): las reglas que proponen el destino de un abono y la consignación reportada en ventanilla.
// La línea del extracto es de Tesorería: se guarda por su PublicId, sin FK hacia TES_ (R-05). (nuevo)

/// <summary>
/// <c>RCD_IdentificationRules</c>: una regla de identificación de abonos, por criterio y prioridad (la menor primero). <see cref="ApplyOnLoad"/> sólo surte efecto con la
/// referencia exacta y el parámetro <c>Identificacion.AplicarAlCargar</c> encendido; lo que propone se aplica con la modalidad del canal banco (D38, <c>RCD_ChannelModalityRules</c>). (nuevo)
/// </summary>
public class IdentificationRule : AuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public IdentificationCriterion Criterion { get; set; }

    public int Priority { get; set; }

    public bool ApplyOnLoad { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// <c>RCD_ReportedDeposits</c>: la consignación que el asociado reporta en ventanilla con su soporte (FR-069; D11). Por defecto queda por verificar hasta encontrarla en el
/// extracto; con <c>ConsignacionReportada.Modo = AplicarDeInmediato</c> se aplica al registrarla con medio «consignación» y queda pendiente de cruzar. Lo no encontrado en
/// <c>ConsignacionReportada.DiasParaVerificar</c> alerta y se muestra <c>Expired</c> (informativo: se puede verificar igual). Sólo cambian el estado, la línea que la confirma y el recibo. (nuevo)
/// </summary>
public class ReportedDeposit : AuditableEntity
{
    public string Number { get; set; } = string.Empty;

    public int PersonId { get; set; }

    public int BranchId { get; set; }

    public int RegisteredByUserId { get; set; }

    public Guid OwnBankAccountPublicId { get; set; }

    public DateOnly DepositDate { get; set; }

    public decimal Amount { get; set; }

    public string? Reference { get; set; }

    public Guid SupportAttachmentPublicId { get; set; }

    /// <summary><c>Loan</c> o <c>Account</c>: a qué va lo consignado.</summary>
    public ApplicationTarget Target { get; set; }

    public Guid ProductPublicId { get; set; }

    public bool AppliedImmediately { get; set; }

    public DateTime RegisteredAt { get; set; }

    public ReportedDepositStatus Status { get; set; } = ReportedDepositStatus.ToVerify;

    /// <summary>El recibo que la aplicó (al registrarla, si se aplicó de inmediato, o al cruzarla con el extracto).</summary>
    public long? ReceiptId { get; set; }

    /// <summary>La línea del extracto que la confirma.</summary>
    public Guid? StatementLinePublicId { get; set; }

    public DateTime? VerifiedAt { get; set; }

    public int? VerifiedByUserId { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;
}
