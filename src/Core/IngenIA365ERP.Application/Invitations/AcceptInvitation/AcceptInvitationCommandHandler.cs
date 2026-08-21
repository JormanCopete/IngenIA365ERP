using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Caching;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Services;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Invitations.AcceptInvitation;

/// <summary>
/// T054 — Acepta una invitación: serializa el trabajo con lock distribuido,
/// resuelve la identidad central (crea/reutiliza/sesión-activa), activa o
/// reactiva la membresía, provisiona <c>SEC_Users</c> del tenant, marca la
/// invitación consumida y emite el JWT operativo con <c>active_tenant_id</c>
/// ya seteado.
///
/// <para>
/// El lock <c>lock:invitation:{tokenHashHex}</c> (TTL 30s) garantiza
/// single-use estricto: si dos clicks del enlace llegan en paralelo, solo el
/// primero entra; el segundo recibe <c>Invitation.LockBusy</c>. El UPDATE
/// condicional sobre <c>RowVersion</c> es defensa adicional para el caso de
/// que el lock haya expirado por TTL antes de terminar.
/// </para>
///
/// <para>
/// <b>Eventos auditables emitidos</b> (FR-024 + AuditEventTypes):
/// <c>Invitation.Accepted</c> + <c>Membership.Activated|ActivatedFromSuspension</c>.
/// </para>
///
/// <para>
/// <b>Asunción multi-tenancy</b>: <c>TenantUserProvisioner</c> escribe en la BD
/// del tenant activo via <see cref="IApplicationDbContext"/>. Como el endpoint
/// <c>/api/invitations/accept</c> es AllowAnonymous y NO pasa por
/// TenantResolutionMiddleware, el contexto Finbuckle queda en el schema
/// default ("dbo") — aceptable en dev/MVP. Para multi-schema real, el
/// endpoint deberá seleccionar el tenant explícitamente antes de invocar
/// (TODO US1.4 integration test cierra esta brecha).
/// </para>
/// </summary>
public sealed class AcceptInvitationCommandHandler(
    IAdminDbContext db,
    ICentralIdentityProvider centralIdentity,
    ICentralJwtIssuer jwtIssuer,
    ICentralRefreshTokenStore refreshStore,
    ITenantUserProvisioner tenantUserProvisioner,
    IDistributedLock distributedLock,
    ISecureTokenGenerator tokens,
    IDateTimeService clock,
    ICurrentCentralUserContext currentUser,
    IAuditAppendOnlyWriter auditWriter,
    ILogger<AcceptInvitationCommandHandler> logger)
    : IRequestHandler<AcceptInvitationCommand, Result<AcceptInvitationResult>>
{
    private static readonly TimeSpan LockTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RefreshTokenTtl = TimeSpan.FromHours(12);

    public async Task<Result<AcceptInvitationResult>> Handle(
        AcceptInvitationCommand request, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var hash = tokens.HashPlainToken(request.Token);
        var hashHex = Convert.ToHexString(hash);

        // 1) Lock distribuido por tokenHash. Si otro click ya está procesando,
        //    rechazamos limpiamente — el caller puede reintentar tras el TTL.
        await using var lockHandle = await distributedLock.TryAcquireAsync(
            $"lock:invitation:{hashHex}", LockTtl, ct);
        if (lockHandle is null)
        {
            return Result.Failure<AcceptInvitationResult>(
                "Invitation.LockBusy",
                "La invitación está siendo procesada por otra sesión. Reintenta en unos segundos.");
        }

        // 2) Cargar Invitation por hash (con tracking — vamos a mutarla).
        var invitation = await db.Invitations.FirstOrDefaultAsync(i => i.TokenHash == hash, ct);
        if (invitation is null)
        {
            return Result.Failure<AcceptInvitationResult>(
                "Invitation.NotFound", "El enlace no corresponde a ninguna invitación.");
        }

        // 3) Validar estado.
        var validityError = ValidateInvitation(invitation, now);
        if (validityError is not null)
        {
            return Result.Failure<AcceptInvitationResult>(validityError.Value.code, validityError.Value.message);
        }

        // 4) Resolver tenant. Si fue soft-deleted entre emisión y aceptación,
        //    rechazamos sin filtrar detalles.
        var tenant = await db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.PublicId == invitation.TenantId && t.IsActive, ct);
        if (tenant is null)
        {
            return Result.Failure<AcceptInvitationResult>(
                "Invitation.NotFound", "El enlace no corresponde a ninguna invitación.");
        }

        // 5) Resolver CentralUser según rama.
        var userResolution = await ResolveCentralUserAsync(request, invitation, ct);
        if (userResolution.IsFailure)
        {
            return Result.Failure<AcceptInvitationResult>(userResolution.Error.Code, userResolution.Error.Message);
        }
        var centralUserId = userResolution.Value;

        // 6) Crear / reactivar TenantMembership.
        var membership = await db.TenantMemberships
            .FirstOrDefaultAsync(m => m.CentralUserId == centralUserId && m.TenantId == tenant.PublicId, ct);

        string membershipAuditAction;
        if (membership is null)
        {
            membership = TenantMembership.CreateActive(
                centralUserId: centralUserId,
                tenantId: tenant.PublicId,
                isTenantAdmin: invitation.InviteAsTenantAdmin,
                invitedByUserId: invitation.InvitedByUserId,
                now: now);
            db.TenantMemberships.Add(membership);
            membershipAuditAction = AuditEventTypes.MembershipActivated;
        }
        else
        {
            switch (membership.Status)
            {
                case MembershipStatus.Active:
                    // Idempotente — ya era miembro activo.
                    membershipAuditAction = AuditEventTypes.MembershipActivated;
                    break;
                case MembershipStatus.Suspended:
                    membership.Reactivate(now);
                    membershipAuditAction = AuditEventTypes.MembershipActivatedFromSuspension;
                    break;
                case MembershipStatus.Revoked:
                    membership.ReactivateFromRevocation(now);
                    membershipAuditAction = AuditEventTypes.MembershipActivated;
                    break;
                case MembershipStatus.Invited:
                    membership.Activate(now);
                    membershipAuditAction = AuditEventTypes.MembershipActivated;
                    break;
                default:
                    return Result.Failure<AcceptInvitationResult>(
                        "Membership.InvalidState",
                        $"Estado de membresía inesperado: {membership.Status}.");
            }
        }

        // 7) Provisionar SEC_Users en el tenant (asunción multi-tenancy, ver
        //    XML doc del handler).
        var provisioning = await tenantUserProvisioner.EnsureExistsAsync(
            centralUserId,
            invitation.Email,
            tenant.PublicId,
            tenant.Id,
            invitation.InviteAsTenantAdmin,
            ct);
        if (provisioning.IsFailure)
        {
            return Result.Failure<AcceptInvitationResult>(provisioning.Error.Code, provisioning.Error.Message);
        }

        // 8) Marcar invitation como Accepted. SaveChanges fuerza UPDATE condicional
        //    sobre RowVersion (interceptor del proyecto). Si otro proceso aceptó
        //    primero, EF lanza DbUpdateConcurrencyException.
        invitation.MarkAccepted(centralUserId, now);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<AcceptInvitationResult>(
                "Invitation.LockBusy",
                "La invitación fue procesada por otra sesión simultáneamente.");
        }

        // 9) Emitir JWT operativo con active_tenant_id.
        var user = await centralIdentity.FindByIdAsync(centralUserId, ct);
        if (user is null)
        {
            // No debería ocurrir — acabamos de crearlo o validarlo.
            logger.LogError("CentralUser {CentralUserId} desapareció entre validación y emisión de JWT.", centralUserId);
            return Result.Failure<AcceptInvitationResult>(
                "Identity.UserNotFound", "Error interno al emitir el token de sesión.");
        }

        // FR-003b/FR-003c: la aceptación no puede saltarse las exigencias de MFA
        // salvo en la rama sesión-activa (ese JWT ya pasó los gates del login).
        //  - Usuario con MFA y sin verificar en este flujo → challenge MfaRequired.
        //  - Tenant con política "MFA obligatorio" y usuario sin MFA → challenge
        //    MfaEnrollmentRequired (enrollment forzado).
        // Antes se emitía sesión operativa full: ventana de hasta 12h sin MFA.
        string? mfaChallenge = null;
        string? challengePurpose = null;
        if (!request.UseActiveSession)
        {
            if (user.TwoFactorEnabled)
            {
                mfaChallenge = "MfaRequired";
                challengePurpose = CentralJwtPurposes.MfaVerify;
            }
            else if (await db.TenantMfaPolicies.AsNoTracking()
                         .AnyAsync(p => p.TenantId == tenant.PublicId && p.IsRequired, ct))
            {
                mfaChallenge = "MfaEnrollmentRequired";
                challengePurpose = CentralJwtPurposes.MfaEnroll;
            }
        }

        CentralAccessTokenResult? access = null;
        CentralRefreshTokenResult? refresh = null;
        CentralAccessTokenResult? challengeToken = null;

        if (mfaChallenge is null)
        {
            access = jwtIssuer.IssueAccessToken(
                centralUserId: centralUserId,
                email: user.Email,
                isGlobalMasterAdmin: user.IsGlobalMasterAdmin,
                activeTenantId: tenant.PublicId,
                tenantAdmin: membership.IsTenantAdmin,
                mfaVerified: false);

            refresh = jwtIssuer.IssueRefreshToken();

            // Sin este Store, el refresh devuelto sería un token muerto: /api/auth/refresh
            // busca la sesión por hash en Redis y respondería Identity.RefreshToken.Invalid.
            await refreshStore.StoreAsync(
                refresh.HashHex,
                new CentralRefreshSession(
                    CentralUserId: centralUserId,
                    ActiveTenantPublicId: tenant.PublicId,
                    FamilyId: Guid.NewGuid(),
                    IssuedAt: now,
                    IpAddress: null,
                    UserAgent: null,
                    ReplacedByTokenHashHex: null,
                    SecurityStamp: user.SecurityStamp),
                RefreshTokenTtl, ct);
        }
        else
        {
            challengeToken = jwtIssuer.IssueChallengeToken(
                centralUserId, user.Email, user.IsGlobalMasterAdmin, challengePurpose!, null);
        }

        // 10) Audit events (FR-024) — además del AuditBehavior automático que
        //     registra el command name, emitimos los eventos específicos del
        //     dominio de identidad.
        await EmitAuditAsync(
            tenantId: tenant.PublicId,
            centralUserId: centralUserId,
            email: user.Email,
            action: AuditEventTypes.InvitationAccepted,
            entityType: nameof(Invitation),
            entityPublicId: invitation.PublicId,
            occurredAt: now,
            ct: ct);

        await EmitAuditAsync(
            tenantId: tenant.PublicId,
            centralUserId: centralUserId,
            email: user.Email,
            action: membershipAuditAction,
            entityType: nameof(TenantMembership),
            entityPublicId: membership.PublicId,
            occurredAt: now,
            ct: ct);

        logger.LogInformation(
            "Invitación {InvitationId} aceptada por CentralUser {CentralUserId} → tenant {Tenant} (membership {Action}).",
            invitation.PublicId, centralUserId, tenant.Name, membershipAuditAction);

        return Result.Success(new AcceptInvitationResult(
            AccessToken: access?.Jwt,
            AccessTokenExpiresAt: access?.ExpiresAt,
            RefreshToken: refresh?.Token,
            RefreshTokenExpiresAt: refresh?.ExpiresAt,
            CentralUserId: centralUserId,
            ActiveTenantPublicId: tenant.PublicId,
            ActiveTenantName: tenant.Name,
            Challenge: mfaChallenge ?? "None",
            ChallengeToken: challengeToken?.Jwt));
    }

    private static (string code, string message)? ValidateInvitation(Invitation invitation, DateTime now)
    {
        if (invitation.Status == InvitationStatus.Accepted)
            return ("Invitation.AlreadyAccepted", "La invitación ya fue aceptada.");
        if (invitation.Status == InvitationStatus.Revoked)
            return ("Invitation.Revoked", "La invitación fue revocada.");
        if (invitation.Status == InvitationStatus.Superseded)
            return ("Invitation.Superseded", "La invitación fue reemplazada por una más reciente.");
        if (invitation.Status == InvitationStatus.Expired || invitation.ExpiresAt <= now)
            return ("Invitation.Expired", "La invitación expiró.");
        if (invitation.Status != InvitationStatus.Pending)
            return ("Invitation.InvalidState", $"Estado inesperado: {invitation.Status}.");
        return null;
    }

    private async Task<Result<Guid>> ResolveCentralUserAsync(
        AcceptInvitationCommand request, Invitation invitation, CancellationToken ct)
    {
        // Rama A — sesión activa: el JWT central del request debe coincidir con
        // el email invitado (FR-029(b)).
        if (request.UseActiveSession)
        {
            if (!currentUser.IsAuthenticated || currentUser.CentralUserId is null)
            {
                return Result.Failure<Guid>(
                    "Invitation.NoActiveSession",
                    "Se indicó 'usar sesión activa' pero no hay un JWT central válido en el request.");
            }

            var jwtEmail = currentUser.Email ?? string.Empty;
            if (!string.Equals(jwtEmail, invitation.Email, StringComparison.OrdinalIgnoreCase))
            {
                return Result.Failure<Guid>(
                    "Invitation.EmailMismatch",
                    "El email del JWT activo no coincide con el email invitado.");
            }
            return Result.Success(currentUser.CentralUserId.Value);
        }

        var existing = await centralIdentity.FindByEmailAsync(invitation.Email, ct);

        // Rama B — registro nuevo: el email no debe estar tomado todavía.
        if (request.Registration is not null)
        {
            if (existing is not null)
            {
                return Result.Failure<Guid>(
                    "Invitation.EmailAlreadyRegistered",
                    "El email ya tiene una identidad central; usa la opción 'credenciales existentes'.");
            }

            var creation = await centralIdentity.CreateUserAsync(
                email: invitation.Email,
                password: request.Registration.Password,
                emailConfirmed: true, // el flujo de invitación implica verificación del email
                ct: ct);
            if (!creation.Succeeded || creation.CentralUserId is null)
            {
                var code = creation.ErrorCodes.FirstOrDefault() ?? "Identity.CreateUserFailed";
                return Result.Failure<Guid>(code, "No se pudo crear la identidad central.");
            }
            return Result.Success(creation.CentralUserId.Value);
        }

        // Rama C — credenciales existentes: usuario YA registrado, sin sesión.
        if (request.ExistingCredentials is not null)
        {
            if (existing is null)
            {
                return Result.Failure<Guid>(
                    "Invitation.EmailNotRegistered",
                    "El email no tiene identidad central; usa la opción 'registro nuevo'.");
            }

            var ok = await centralIdentity.ValidatePasswordAsync(
                existing.Id, request.ExistingCredentials.Password, ct);
            if (!ok)
            {
                return Result.Failure<Guid>(
                    "Identity.InvalidCredentials",
                    "Contraseña incorrecta.");
            }
            return Result.Success(existing.Id);
        }

        // El validator XOR garantiza que llegamos aquí solo en estado inválido.
        return Result.Failure<Guid>(
            "Invitation.AmbiguousBranch",
            "No se indicó cómo confirmar la invitación.");
    }

    private async Task EmitAuditAsync(
        Guid tenantId,
        Guid centralUserId,
        string email,
        string action,
        string entityType,
        Guid entityPublicId,
        DateTime occurredAt,
        CancellationToken ct)
    {
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: tenantId.ToString("N"),
                UserId: centralUserId.ToString("N"),
                UserName: email,
                Action: action,
                EntityType: entityType,
                EntityPublicId: entityPublicId.ToString("N"),
                Module: "Identity",
                OldValuesJson: null,
                NewValuesJson: null,
                ChangedFields: null,
                IpAddress: null,
                UserAgent: null,
                Endpoint: null,
                HttpMethod: null,
                HttpStatusCode: 200,
                DurationMs: null,
                OccurredAt: occurredAt), ct);
        }
        catch (Exception ex)
        {
            // No tumbar el flow por fallar la auditoría; el AuditBehavior
            // del pipeline también registra el command name como respaldo.
            logger.LogWarning(ex, "AuditAppendOnlyWriter falló para acción {Action}", action);
        }
    }
}
