using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Invitations.IssueTenantInvitation;

/// <summary>
/// T051 — Emite una invitación a la empresa indicada. El emisor debe ser
/// administrador del tenant (verificado por el handler además del filter
/// <c>RequireTenantAdmin</c> del endpoint). NO se permite marcar el destinatario
/// como tenant admin desde este command — esa potestad es exclusiva del master
/// admin (ver <c>IssueMasterInvitationCommand</c>, T052).
/// </summary>
/// <remarks>
/// Feature 017: además del correo, el documento (digitado dos veces), nombres, celular, rol y cargo.
/// Sin documento la invitación sale como antes (compatibilidad de la API); la pantalla lo exige.
/// </remarks>
public sealed record IssueTenantInvitationCommand(
    Guid TenantPublicId,
    string Email,
    string? DocumentType = null,
    string? DocumentNumber = null,
    string? DocumentNumberConfirmation = null,
    string? FirstName = null,
    string? OtherNames = null,
    string? LastName = null,
    string? SecondLastName = null,
    string? MobilePhone = null,
    Guid? RolePublicId = null,
    Guid? PositionPublicId = null
) : IRequest<Result<IssueTenantInvitationResult>>
{
    public Services.DatosDelInvitado Datos() => new(
        Email, DocumentType, DocumentNumber, DocumentNumberConfirmation,
        FirstName, OtherNames, LastName, SecondLastName, MobilePhone, RolePublicId, PositionPublicId);
}

/// <summary>
/// Resultado de la emisión: identificador público y vencimiento. El token plano
/// NO se devuelve al API (sale por correo al destinatario, FR-024).
/// </summary>
public sealed record IssueTenantInvitationResult(
    Guid InvitationPublicId,
    DateTime ExpiresAt,
    string? Message = null
);

/// <summary>
/// El mismo mensaje haya o no identidad (feature 017, D-05): el administrador nunca sabe si la
/// persona pertenece a otras cooperativas.
/// </summary>
public static class MensajesDeInvitacion
{
    public const string Enviada =
        "Invitación enviada. Si la persona ya tiene cuenta, le llegará a su correo registrado.";
}
