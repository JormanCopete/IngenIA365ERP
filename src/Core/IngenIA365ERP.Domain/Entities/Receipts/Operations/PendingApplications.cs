using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Receipts;

namespace IngenIA365ERP.Domain.Entities.Receipts.Operations;

/// <summary>
/// <c>RCD_PendingApplications</c> (H; feature 015, R1, T0362; FR-046; D14; data-model §8.1): el <b>recaudo pendiente de aplicar</b>. Una aplicación de un recibo que
/// no pudo ir a su contrato porque el módulo todavía no está activado y la persona está en una carga de migración aplicada sin activar (Cartera lo informa por
/// <c>IConsultaDeCargaSinActivar</c>; Captaciones, en sus exigibles). Va por persona y producto, por valor libre, y se aplica después —en la activación o ese mismo
/// día— por una persona con permiso que elige la modalidad, con la fecha valor del recibo. Sólo <see cref="Status"/> cambia; lo demás se escribe una vez. (nuevo)
/// </summary>
public class PendingApplication : AuditableEntity, IHechoInmutable
{
    public long ReceiptId { get; set; }

    public long ReceiptApplicationId { get; set; }

    /// <summary>La persona titular (deudora, codeudora o titular de la cuenta en la carga).</summary>
    public int PersonId { get; set; }

    /// <summary>A qué módulo va cuando se aplique: <c>Loan</c> (Cartera) o <c>Account</c> (Captaciones y aportes).</summary>
    public ApplicationTarget TargetModule { get; set; }

    /// <summary>La obligación de la carga que señaló el cajero (<c>ObligacionCargadaDto.ObligationPublicId</c>), si la había.</summary>
    public Guid? LoadObligationPublicId { get; set; }

    /// <summary>El número que el cajero vio (de la carga) para la persona que aplicará.</summary>
    public string? ProductReference { get; set; }

    public decimal Amount { get; set; }

    /// <summary>La fecha valor del recibo: con ella se aplica.</summary>
    public DateOnly ValueDate { get; set; }

    public int BranchId { get; set; }

    [ColumnaMutable] public PendingApplicationState Status { get; set; } = PendingApplicationState.Pending;
}

/// <summary>
/// <c>RCD_PendingApplicationResolutions</c> (H; feature 015, R1, T0362; FR-046): la aplicación del pendiente por el contrato del módulo ya activado, con la modalidad que
/// eligió la persona, la clave de la porción del recibo (repetir no aplica dos veces) y el comprobante que devolvió el módulo. Una por pendiente (UK). (nuevo)
/// </summary>
public class PendingApplicationResolution : AuditableEntity, IHechoInmutable
{
    public int PendingApplicationId { get; set; }

    /// <summary>El crédito o la cuenta real, ya activado.</summary>
    public Guid ProductPublicId { get; set; }

    public string? ProductNumber { get; set; }

    /// <summary><c>ModalidadDePago</c> o <c>ModalidadDeConsignacion</c> como número, según el módulo.</summary>
    public int Modality { get; set; }

    public int? PrepaymentChoice { get; set; }

    public DateOnly ValueDate { get; set; }

    public decimal Amount { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public Guid? ModuleOperationPublicId { get; set; }

    public string? ModuleOperationNumber { get; set; }

    public int ResolvedByUserId { get; set; }

    public DateTime ResolvedAt { get; set; }
}
