using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Enums.Approvals;

namespace IngenIA365ERP.Application.Common.Approvals;

/// <summary>
/// El motor de aprobaciones de plataforma (feature 012, T33, T34, T083; FR-009, FR-010; research R13). Un solo motor
/// para todo lo que pide aprobación: tipos de documento, ajustes de conteo, saldo inicial, diferencias de traslado,
/// descuentos sobre el tope, arqueo, movimientos de caja, crédito provisional y el cruce de compras. Decide con
/// <c>EvaluadorDePolitica</c> (puro); guarda solicitudes y decisiones en <c>COR_Approval*</c>. Nunca llama a
/// <c>SaveChanges</c> en <see cref="EvaluarAsync"/> ni en <see cref="SolicitarAsync"/>: la solicitud se guarda con el
/// documento que la pide.
/// </summary>
public interface IMotorDeAprobaciones
{
    /// <summary>
    /// Qué exige un monto: la política del sujeto vigente en la <paramref name="fechaDeOperacion"/> (la del tipo gana
    /// sobre la de todos) o su regla fija, con el monto máximo del <paramref name="permisoLimitado"/> por
    /// <c>ILimitesPorPermiso</c>. Falla con <c>Inventory.Approval.AmountExceedsLimit</c> si el monto supera el máximo y
    /// no hay nivel que forzar.
    /// </summary>
    Task<Result<EvaluacionConPolitica>> EvaluarAsync(
        string subject,
        Guid? documentTypePublicId,
        DateOnly fechaDeOperacion,
        decimal monto,
        string? permisoLimitado,
        CancellationToken ct);

    /// <summary>
    /// Evalúa y, si hace falta aprobación, crea la solicitud <b>sellada</b> (política, niveles, excluidos, alcance y
    /// huella) sin guardarla, y avisa a los titulares del primer nivel. <c>Success(null)</c> si no hace falta.
    /// </summary>
    Task<Result<ApprovalRequest?>> SolicitarAsync(SolicitudDeAprobacion solicitud, CancellationToken ct);

    /// <summary>
    /// Decide el nivel actual: permiso del nivel y alcance (si no, 404), estado pendiente, segregación, huella. Con
    /// niveles restantes pasa al siguiente y avisa; en el último, la fuente confirma en la misma transacción (si falla,
    /// la decisión no queda). Rechazar exige motivo y devuelve lo aprobado a borrador. Guarda.
    /// </summary>
    Task<Result<DecisionResultDto>> DecidirAsync(DecisionDeAprobacion decision, CancellationToken ct);

    /// <summary>Las solicitudes que quien hace la petición puede decidir <b>ahora</b>.</summary>
    Task<IReadOnlyList<ApprovalRequest>> PendientesParaMiAsync(CancellationToken ct);

    /// <summary>
    /// Invalida (<c>Cancelled</c>) la solicitud pendiente de una fuente cuando lo que se aprueba cambió (un descuento
    /// recalculado), sin guardar: lo llama el módulo en la misma unidad de trabajo del cambio. Así la invalidación no
    /// depende de que un aprobador intente decidir, porque el rechazo <c>Approvals.Request.ContentChanged</c> revierte
    /// su transacción. Devuelve la solicitud invalidada, o nula si no había. (nuevo)
    /// </summary>
    Task<ApprovalRequest?> InvalidarAsync(string sourceType, Guid sourcePublicId, string subject, CancellationToken ct);

    /// <summary>
    /// El solicitante o el creador retiran una solicitud pendiente (<c>Cancelled</c>) y lo aprobado vuelve a borrador
    /// para corregirlo. Otro usuario: 404. Guarda. (nuevo)
    /// </summary>
    Task<Result> RetirarAsync(Guid requestPublicId, string motivo, CancellationToken ct);
}

/// <summary>La evaluación y la versión de política que la produjo (nula en la regla fija o sin política). (nuevo)</summary>
public sealed record EvaluacionConPolitica(EvaluacionDeAprobacion Evaluacion, int? PolicyId);

/// <summary>
/// Lo que un módulo pide aprobar (nuevo). <paramref name="CreatedByUserId"/> y <paramref name="Participantes"/> son
/// <c>SEC_Users.Id</c>; quien pide es el actor de la petición. <paramref name="ContentSha256"/> es la huella de lo que
/// se aprueba (si cambia, la aprobación ya no vale). <paramref name="PermisoLimitado"/> es el permiso cuyo monto
/// máximo se compara (<c>Inventory.Adjustments.Confirm</c>…), nulo si no aplica.
/// </summary>
public sealed record SolicitudDeAprobacion(
    string Subject,
    string SourceType,
    Guid SourcePublicId,
    string SourceLabel,
    Guid? DocumentTypePublicId,
    Guid? ScopeWarehousePublicId,
    Guid? ScopePointOfSalePublicId,
    decimal Amount,
    DateOnly OperationDate,
    int CreatedByUserId,
    IReadOnlyCollection<int> Participantes,
    string ContentSha256,
    string? PermisoLimitado,
    string Currency = "COP");

/// <summary>
/// El aprobador presente en el equipo de quien pidió, ya identificado por su passkey o su TOTP (nuevo). Sin él decide
/// quien tiene la sesión (<see cref="ApprovalMethod.OwnSession"/>).
/// </summary>
public sealed record AprobadorPresente(int UserId, string Name, ApprovalMethod Method, Guid? CredentialPublicId);

/// <summary>Una decisión sobre el nivel actual de una solicitud. (nuevo)</summary>
public sealed record DecisionDeAprobacion(
    Guid RequestPublicId,
    ApprovalDecisionKind Decision,
    string? Reason,
    string ExpectedContentSha256,
    AprobadorPresente? Presente = null);
