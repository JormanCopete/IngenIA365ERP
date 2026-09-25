using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Enums.Approvals;

namespace IngenIA365ERP.Domain.Entities.Approvals.Transactions;

/// <summary>
/// Una decisión sobre un nivel de una solicitud (<c>COR_ApprovalDecisions</c>; feature 012, T33, T081; data-model
/// §21). Es un <b>hecho</b>: sólo se inserta. Quien decide es <c>SEC_Users.Id</c> de <c>IActorActual</c> (o el
/// aprobador presente identificado por su passkey o su TOTP), nunca el entero del token ni el correo. Una sola
/// aprobación por nivel (único <c>(RequestId, Level)</c> filtrado <c>[Decision] = 1</c>).
/// </summary>
public class ApprovalDecision : AuditableEntity, IHechoInmutable
{
    public int RequestId { get; set; }

    public ApprovalRequest? Request { get; set; }

    /// <summary>El orden del nivel decidido (tinyint).</summary>
    public byte Level { get; set; }

    public ApprovalDecisionKind Decision { get; set; }

    /// <summary><c>SEC_Users.Id</c> de quien decidió.</summary>
    public int DecidedByUserId { get; set; }

    /// <summary>Su nombre, sellado (máx. 150).</summary>
    public string DecidedByName { get; set; } = string.Empty;

    public DateTime DecidedAt { get; set; }

    /// <summary>Desde su sesión o presente en el equipo de quien pidió; nunca con contraseña.</summary>
    public ApprovalMethod Method { get; set; }

    /// <summary>La passkey usada en <see cref="ApprovalMethod.InPersonPasskey"/>.</summary>
    public Guid? CredentialPublicId { get; set; }

    /// <summary>El permiso del nivel con el que decidió (máx. 100).</summary>
    public string PermissionCodeUsed { get; set; } = string.Empty;

    /// <summary>La huella de lo aprobado al decidir (char(64)).</summary>
    public string ContentSha256 { get; set; } = string.Empty;

    /// <summary>Obligatorio al rechazar (máx. 500).</summary>
    public string? Reason { get; set; }
}
