using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.Extensions.Logging;

using IngenIA365ERP.Application.Identity.Auth.Common;

namespace IngenIA365ERP.Application.Identity.Sessions.SelectTenant;

/// <summary>
/// Selecciona el tenant activo al inicio de la sesión (cuando el JWT central
/// llegó con purpose=tenant-select y el usuario eligió en
/// <c>SelectTenant.razor</c>). Acepta también purpose=full por si el cliente
/// re-llama tras una operación.
/// </summary>
public sealed class SelectTenantCommandHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    ITenantMembershipReader memberships,
    ICentralJwtIssuer jwtIssuer,
    ICentralRefreshTokenStore refreshStore,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<SelectTenantCommandHandler> logger)
    : IRequestHandler<SelectTenantCommand, Result<SelectTenantResult>>
{
    private static readonly TimeSpan RefreshTokenTtl = TimeSpan.FromHours(12);

    public async Task<Result<SelectTenantResult>> Handle(SelectTenantCommand request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.CentralUserId is null)
        {
            return Result.Failure<SelectTenantResult>(
                "Identity.Unauthenticated", "Falta el token de challenge.");
        }

        // Acepta tenant-select (post-login) o full (re-selección post-auth).
        if (currentUser.Purpose != CentralJwtPurposes.TenantSelect
            && currentUser.Purpose != CentralJwtPurposes.Full)
        {
            return Result.Failure<SelectTenantResult>(
                "Identity.WrongTokenPurpose",
                $"Este endpoint requiere purpose=tenant-select o full, recibido '{currentUser.Purpose}'.");
        }

        var centralUserId = currentUser.CentralUserId.Value;
        var user = await centralIdentity.FindByIdAsync(centralUserId, ct);
        if (user is null)
        {
            return Result.Failure<SelectTenantResult>(
                "Identity.Unauthenticated", "Usuario no encontrado.");
        }

        // Verificar que el usuario tiene membresía Active con el tenant elegido.
        var active = await memberships.GetActiveMembershipsAsync(centralUserId, ct);
        var chosen = active.FirstOrDefault(m => m.TenantId == request.TenantPublicId);
        if (chosen is null)
        {
            return Result.Failure<SelectTenantResult>(
                "Membership.NotActive",
                "No tienes membresía activa con la empresa indicada.");
        }

        // Esta puerta NO comprobaba ninguna politica: solo membresia activa. Sin
        // el filtro aqui, la comprobacion del auto-select se rodea simplemente
        // eligiendo la cooperativa a mano desde el selector.
        var metodoDemostrado = currentUser.MetodoMfa;
        if (GuardiaDeMetodos.Evaluar(
                chosen.IsMfaRequiredByTenant, chosen.MetodosAceptados, metodoDemostrado)
            != GuardiaDeMetodos.Veredicto.Admite)
        {
            return Result.Failure<SelectTenantResult>(
                "Tenant.MfaMethodNotAccepted",
                $"'{chosen.TenantName}' no acepta el metodo con el que entraste. " +
                $"Metodos que acepta: {string.Join(", ", ConversionDeMetodosMfa.ALiterales(chosen.MetodosAceptados))}.");
        }

        var now = clock.UtcNow;

        var access = jwtIssuer.IssueAccessToken(
            centralUserId: centralUserId,
            email: user.Email,
            isGlobalMasterAdmin: user.IsGlobalMasterAdmin,
            activeTenantId: chosen.TenantId,
            tenantAdmin: chosen.IsTenantAdmin,
            mfaVerified: user.TwoFactorEnabled,
            metodoMfa: metodoDemostrado);

        var refresh = jwtIssuer.IssueRefreshToken();
        var familyId = Guid.NewGuid();

        await refreshStore.StoreAsync(
            tokenHashHex: refresh.HashHex,
            session: new CentralRefreshSession(
                CentralUserId: centralUserId,
                ActiveTenantPublicId: chosen.TenantId,
                FamilyId: familyId,
                IssuedAt: now,
                IpAddress: null,
                UserAgent: null,
                ReplacedByTokenHashHex: null,
                SecurityStamp: user.SecurityStamp,
                MetodoMfa: metodoDemostrado),
            ttl: RefreshTokenTtl,
            ct: ct);

        await EmitAuditAsync(centralUserId, user.Email, chosen.TenantId, now, ct);

        return Result.Success(new SelectTenantResult(
            AccessToken: access.Jwt,
            AccessTokenExpiresAt: access.ExpiresAt,
            RefreshToken: refresh.Token,
            RefreshTokenExpiresAt: refresh.ExpiresAt,
            ExpiresInSeconds: (int)(access.ExpiresAt - now).TotalSeconds,
            Tenant: new SelectedTenantInfo(chosen.TenantId, chosen.TenantName)));
    }

    private async Task EmitAuditAsync(
        Guid centralUserId, string email, Guid tenantId, DateTime now, CancellationToken ct)
    {
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: tenantId.ToString("N"),
                UserId: centralUserId.ToString("N"),
                UserName: email,
                Action: AuditEventTypes.SessionTenantSelected,
                EntityType: nameof(Domain.Entities.Admin.TenantMembership),
                EntityPublicId: tenantId.ToString("N"),
                Module: "Identity",
                OldValuesJson: null,
                NewValuesJson: null,
                ChangedFields: null,
                IpAddress: null,
                UserAgent: null,
                Endpoint: "/api/sessions/select-tenant",
                HttpMethod: "POST",
                HttpStatusCode: 200,
                DurationMs: null,
                OccurredAt: now), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditAppendOnlyWriter falló para Session.TenantSelected");
        }
    }
}
