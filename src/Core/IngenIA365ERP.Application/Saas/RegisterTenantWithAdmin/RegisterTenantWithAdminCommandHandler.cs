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
            // El envío puede fallar (SMTP caído). El tenant + invitación quedan
            // persistidos; el master admin podrá reenviar luego.
            logger.LogWarning(ex,
                "Falló envío del email de invitación admin para tenant {Tenant}",
                tenant.Name);
        }

        await EmitAuditAsync(currentUser.CentralUserId.Value,
            currentUser.Email ?? string.Empty,
            tenant.PublicId, now, ct);

        return Result.Success(new RegisterTenantWithAdminResult(
            TenantPublicId: tenant.PublicId,
            InvitationPublicId: invitation.PublicId,
            InvitationExpiresAt: expiresAt));
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
