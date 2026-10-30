using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Treasury;

namespace IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;

/// <summary>
/// Lo común de un firmante y un token de una cuenta propia (feature 015, T1, fase 34, T0416; data-model §13.1; FR-208): la cuenta, la persona, la vigencia, el estado y
/// el acta y la aprobación (sujeto <c>BankAccountAuthorityChange</c>) del alta y del retiro. Un cambio nunca se edita: el alta queda pendiente hasta que otra persona la
/// aprueba, y el retiro también; la fila vieja queda con su vigencia cerrada. (nuevo)
/// </summary>
public abstract class BankAccountAuthority : AuditableEntity
{
    public int BankAccountId { get; set; }

    /// <summary>La persona que firma o tiene el token (<c>COR_People.Id</c>).</summary>
    public int PersonId { get; set; }

    /// <summary>Su usuario del ERP, si lo tiene (para la segregación y la alerta de retiro).</summary>
    public int? UserId { get; set; }

    public DateOnly ValidFrom { get; set; }

    public DateOnly? ValidTo { get; set; }

    public BankAccountAuthorityState Status { get; set; } = BankAccountAuthorityState.PendingApproval;

    /// <summary>El acta que respalda el alta (de la junta o del consejo).</summary>
    public Guid MinutesAttachmentPublicId { get; set; }

    public Guid? ApprovalRequestPublicId { get; set; }

    public int RequestedByUserId { get; set; }

    public DateTime RequestedAt { get; set; }

    public DateTime? DecidedAt { get; set; }

    public string? DecisionReason { get; set; }

    /// <summary>El acta y la aprobación del retiro.</summary>
    public Guid? RetirementMinutesAttachmentPublicId { get; set; }

    public Guid? RetirementApprovalRequestPublicId { get; set; }

    public string? RetirementReason { get; set; }

    /// <summary>
    /// Segunda ronda del 2026-10-27 (D-l, FR-234): la fecha desde la que la persona no firma, registrada al pedir el retiro. Desde ese día no cuenta para la
    /// combinación aunque el retiro siga por aprobar; al aprobarlo, la vigencia se cierra la víspera.
    /// </summary>
    public DateOnly? RetiredOn { get; set; }

    public bool Vigente(DateOnly fecha) => Status is BankAccountAuthorityState.Active or BankAccountAuthorityState.PendingRetirement
                                           && ValidFrom <= fecha && (ValidTo is null || ValidTo >= fecha) && (RetiredOn is null || fecha < RetiredOn);
}

/// <summary>
/// <c>TES_BankAccountSigners</c>: un firmante de la cuenta con su clase de firma (<see cref="SignatureClass"/>, la letra de la combinación: «A», «B»), lo que puede firmar
/// solo (<see cref="MaxAmountAlone"/>; nulo, nada) y si firma en conjunto por encima de eso. (nuevo)
/// </summary>
public class BankAccountSigner : BankAccountAuthority
{
    public string SignatureClass { get; set; } = string.Empty;

    public decimal? MaxAmountAlone { get; set; }

    public bool CanSignJointly { get; set; } = true;

    /// <summary>El cargo con que firma (gerente, tesorero, presidente del consejo).</summary>
    public string? Role { get; set; }
}

/// <summary><c>TES_BankAccountTokens</c>: un token del portal del banco de la cuenta, con su serial y quién lo tiene. (nuevo)</summary>
public class BankAccountToken : BankAccountAuthority
{
    public string Serial { get; set; } = string.Empty;

    /// <summary>Para qué sirve en el portal (preparar, aprobar, ambos).</summary>
    public string? Purpose { get; set; }
}
