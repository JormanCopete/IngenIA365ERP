using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Profile.SetDefaultTenant;

public sealed class SetDefaultTenantCommandHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    ITenantMembershipReader memberships,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<SetDefaultTenantCommandHandler> logger)
    : IRequestHandler<SetDefaultTenantCommand, Result>
{
    public async Task<Result> Handle(SetDefaultTenantCommand request, CancellationToken ct)
    {
        if (currentUser.CentralUserId is null || !currentUser.IsAuthenticated)
            return Result.Failure("Identity.Unauthenticated", "Se requiere autenticación válida.");
        if (currentUser.Purpose != CentralJwtPurposes.Full)
            return Result.Failure("Identity.WrongTokenPurpose",
                "Este endpoint requiere purpose=full.");

        var centralUserId = currentUser.CentralUserId.Value;

        if (request.TenantPublicId.HasValue)
        {
            var active = await memberships.GetActiveMembershipsAsync(centralUserId, ct);
            if (!active.Any(m => m.TenantId == request.TenantPublicId.Value))
            {
                return Result.Failure(
                    "Profile.DefaultTenant.NotActiveMembership",
                    "No tienes membresía activa con la empresa indicada.");
            }
        }

        await centralIdentity.SetDefaultTenantAsync(
            centralUserId, request.TenantPublicId, ct);

        await EmitAuditAsync(centralUserId, currentUser.Email ?? string.Empty,
            request.TenantPublicId, clock.UtcNow, ct);

        return Result.Success();
    }

    private async Task EmitAuditAsync(
        Guid centralUserId, string email, Guid? newDefault, DateTime now, CancellationToken ct)
    {
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: newDefault?.ToString("N") ?? string.Empty,
                UserId: centralUserId.ToString("N"),
                UserName: email,
                Action: AuditEventTypes.ProfileDefaultTenantChanged,
                EntityType: nameof(Domain.Entities.Admin.CentralUser),
                EntityPublicId: centralUserId.ToString("N"),
                Module: "Identity",
                OldValuesJson: null,
                NewValuesJson: newDefault?.ToString("N"),
                ChangedFields: new[] { "DefaultTenantId" },
                IpAddress: null, UserAgent: null,
                Endpoint: "/api/profile/default-tenant",
                HttpMethod: "PUT", HttpStatusCode: 204, DurationMs: null,
                OccurredAt: now), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditWriter falló para Profile.DefaultTenantChanged");
        }
    }
}
