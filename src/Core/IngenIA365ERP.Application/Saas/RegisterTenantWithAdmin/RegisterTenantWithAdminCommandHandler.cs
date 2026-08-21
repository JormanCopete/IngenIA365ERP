using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Configuration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Invitations.Services;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Application.Saas.RegisterTenantWithAdmin;

/// <summary>
/// T112 — Crea el tenant + emite invitación admin en una sola unidad lógica.
/// Defensa en profundidad: además del filter RequireMasterAdmin del endpoint,
/// el handler reverifica is_global_master_admin.
/// </summary>
public sealed class RegisterTenantWithAdminCommandHandler(
    ICurrentCentralUserContext currentUser,
    IAdminDbContext adminDb,
    ISecureTokenGenerator tokens,
    IInvitationEmailDispatcher emailDispatcher,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    IOptions<IdentityEmailOptions> identityEmailOptions,
    ILogger<RegisterTenantWithAdminCommandHandler> logger)
    : IRequestHandler<RegisterTenantWithAdminCommand, Result<RegisterTenantWithAdminResult>>
{
    private readonly IdentityEmailOptions _identityEmailOptions = identityEmailOptions.Value;

    public async Task<Result<RegisterTenantWithAdminResult>> Handle(
        RegisterTenantWithAdminCommand request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.CentralUserId is null)
            return Result.Failure<RegisterTenantWithAdminResult>(
                "Identity.Unauthenticated", "Se requiere autenticación válida.");
        if (!currentUser.IsGlobalMasterAdmin)
            return Result.Failure<RegisterTenantWithAdminResult>(
                "Saas.MasterOnly", "Solo el master admin puede registrar empresas.");

        var existsNit = await adminDb.Tenants.AnyAsync(t => t.Nit == request.Nit, ct);
        if (existsNit)
            return Result.Failure<RegisterTenantWithAdminResult>(
                "Tenant.NitConflict", "Ya existe una empresa con ese NIT.");

        var now = clock.UtcNow;
        var tenant = new Tenant
        {
            Name = request.Name,
            SchemaName = request.SchemaName,
            Subdomain = request.Subdomain,
            Nit = request.Nit,
            LegalName = request.LegalName,
            LegalAddress = request.LegalAddress,
            TaxRegime = request.TaxRegime,
            ContactEmail = request.ContactEmail,
            ContactPhone = request.ContactPhone,
            PlanType = request.PlanType,
            MaxUsers = request.MaxUsers,
            StorageLimitMb = request.StorageLimitMb,
            IsActive = true,
            ActivatedAt = now,
            CreatedBy = "master-admin",
        };
        adminDb.Tenants.Add(tenant);
        await adminDb.SaveChangesAsync(ct);

        // Emite la invitación admin para el FirstAdminEmail.
        var normalizedEmail = request.FirstAdminEmail.Trim().ToUpperInvariant();
        var displayEmail = request.FirstAdminEmail.Trim();
        var (plainToken, hash) = tokens.Generate();
        var expiresAt = now.AddDays(_identityEmailOptions.InvitationLifetimeDays);

        var invitation = Invitation.Create(
            email: displayEmail,
            normalizedEmail: normalizedEmail,
            tenantId: tenant.PublicId,
            invitedByUserId: currentUser.CentralUserId.Value,
            inviteAsTenantAdmin: true,
            tokenHash: hash,
            createdAt: now,
            expiresAt: expiresAt);
        adminDb.Invitations.Add(invitation);
        await adminDb.SaveChangesAsync(ct);

        var correoEnviado = true;
        string? motivoCorreoNoEnviado = null;

        try
        {
            await emailDispatcher.DispatchAsync(new InvitationEmailRequest(
                Invitation: invitation,
                Tenant: tenant,
                InviterDisplayName: currentUser.Email ?? "Master admin",
                PlainTokenBase64Url: plainToken), ct);
        }
        catch (Exception ex)
        {
            // El envío puede fallar (SMTP caído, credenciales, certificado). No
            // se deshace nada a propósito: la cooperativa y la invitación quedan
            // persistidas, porque perderlas por un servidor de correo caído
            // sería peor que quedarse sin el correo.
            //
            // Lo que NO se hace es callarlo: el resultado viaja con
            // CorreoEnviado=false y el motivo, para que la pantalla lo diga en
            // lugar de dar por enviada una invitación que nunca salió.
            //
            // El reenvío existe: ReenviarInvitacionCommand
            // (Application/Invitations/GestionInvitaciones/GestionInvitaciones.cs),
            // expuesto en POST /api/tenants/{tenantId}/invitations/{id}/reenviar
            // y en la pantalla /admin/tenants/{tenantId}/invitaciones.
            correoEnviado = false;
            motivoCorreoNoEnviado = DescribirFalloDeEnvio(ex);
            logger.LogWarning(ex,
                "Falló envío del email de invitación admin para tenant {Tenant}. " +
                "La invitación {Invitation} quedó creada y debe reenviarse desde " +
                "/admin/tenants/{TenantId}/invitaciones",
                tenant.Name, invitation.PublicId, tenant.PublicId);
        }

        await EmitAuditAsync(currentUser.CentralUserId.Value,
            currentUser.Email ?? string.Empty,
            tenant.PublicId, now, ct);

        return Result.Success(new RegisterTenantWithAdminResult(
            TenantPublicId: tenant.PublicId,
            InvitationPublicId: invitation.PublicId,
            InvitationExpiresAt: expiresAt,
            CorreoEnviado: correoEnviado,
            MotivoCorreoNoEnviado: motivoCorreoNoEnviado));
    }

    /// <summary>
    /// Motivo en una línea para mostrar a quien registró la cooperativa. Usa el
    /// mensaje de la excepción más interna, que es donde el cliente SMTP dice
    /// lo concreto ("no se pudo conectar", "certificado no válido", "535
    /// autenticación fallida"). El stack completo queda en el log.
    /// </summary>
    private static string DescribirFalloDeEnvio(Exception ex)
    {
        var raiz = ex;
        while (raiz.InnerException is not null)
            raiz = raiz.InnerException;

        var mensaje = raiz.Message?.Trim();
        return string.IsNullOrEmpty(mensaje)
            ? "El servidor de correo no aceptó el envío."
            : mensaje;
    }

    private async Task EmitAuditAsync(
        Guid actorId, string actorEmail, Guid tenantId, DateTime now, CancellationToken ct)
    {
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: tenantId.ToString("N"),
                UserId: actorId.ToString("N"),
                UserName: actorEmail,
                Action: AuditEventTypes.TenantCreated,
                EntityType: nameof(Tenant),
                EntityPublicId: tenantId.ToString("N"),
                Module: "Identity",
                OldValuesJson: null, NewValuesJson: null,
                ChangedFields: null,
                IpAddress: null, UserAgent: null,
                Endpoint: "/api/saas/tenants",
                HttpMethod: "POST", HttpStatusCode: 201, DurationMs: null,
                OccurredAt: now), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditWriter falló para Tenant.Created");
        }
    }
}
