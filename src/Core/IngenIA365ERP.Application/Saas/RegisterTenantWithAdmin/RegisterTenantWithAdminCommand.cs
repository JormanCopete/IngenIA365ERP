using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Saas.RegisterTenantWithAdmin;

/// <summary>
/// T112 — Variante del <c>RegisterTenantCommand</c> de Fase 0 que además emite
/// una invitación con <c>InviteAsTenantAdmin=true</c> para
/// <see cref="FirstAdminEmail"/>.
///
/// <para>
/// <b>No es atómica, y a propósito.</b> El tenant y la invitación se persisten
/// primero; el correo se manda después y es fail-soft. Si el servidor de correo
/// está caído, la cooperativa y la invitación quedan creadas igual — perderlas
/// por eso sería peor — y el resultado lo dice: ver
/// <see cref="RegisterTenantWithAdminResult.CorreoEnviado"/>. La recuperación no
/// es manual: se reenvía desde <c>/admin/tenants/{tenantId}/invitaciones</c>.
/// </para>
/// </summary>
public sealed record RegisterTenantWithAdminCommand(
    string Name,
    string SchemaName,
    string? Subdomain,
    string Nit,
    string LegalName,
    string? LegalAddress,
    string? TaxRegime,
    string ContactEmail,
    string? ContactPhone,
    string PlanType,
    int MaxUsers,
    long StorageLimitMb,
    string FirstAdminEmail,
    // Feature 017 (FR-024): los mismos datos de persona que una invitación, sin rol ni cargo (la
    // cooperativa aún no tiene catálogos; entra como administradora). Opcionales por compatibilidad.
    string? FirstAdminDocumentType = null,
    string? FirstAdminDocumentNumber = null,
    string? FirstAdminDocumentNumberConfirmation = null,
    string? FirstAdminFirstName = null,
    string? FirstAdminOtherNames = null,
    string? FirstAdminLastName = null,
    string? FirstAdminSecondLastName = null,
    string? FirstAdminMobilePhone = null
) : IRequest<Result<RegisterTenantWithAdminResult>>
{
    public Invitations.Services.DatosDelInvitado DatosDelPrimerAdministrador() => new(
        FirstAdminEmail, FirstAdminDocumentType, FirstAdminDocumentNumber, FirstAdminDocumentNumberConfirmation,
        FirstAdminFirstName, FirstAdminOtherNames, FirstAdminLastName, FirstAdminSecondLastName, FirstAdminMobilePhone);
}

/// <summary>
/// Resultado del registro.
///
/// <para>
/// <see cref="CorreoEnviado"/> existe porque el envío del correo es fail-soft:
/// la cooperativa y la invitación se crean igual aunque el SMTP esté caído. Sin
/// este dato, quien registra recibe 201 y se va convencido de que la invitación
/// salió. Con él, la respuesta dice qué pasó de verdad y la pantalla puede
/// ofrecer el reenvío.
/// </para>
/// </summary>
/// <param name="CorreoEnviado">
/// <c>true</c> si el correo de invitación se despachó sin error. <c>false</c> si
/// el envío falló: la invitación existe igual y hay que reenviarla desde
/// <c>/admin/tenants/{tenantId}/invitaciones</c>.
/// </param>
/// <param name="MotivoCorreoNoEnviado">
/// Motivo en una línea, legible, cuando <see cref="CorreoEnviado"/> es
/// <c>false</c>. <c>null</c> cuando el correo salió.
/// </param>
public sealed record RegisterTenantWithAdminResult(
    Guid TenantPublicId,
    Guid InvitationPublicId,
    DateTime InvitationExpiresAt,
    bool CorreoEnviado,
    string? MotivoCorreoNoEnviado);
