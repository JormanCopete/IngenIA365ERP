using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Saas.ForceMfaReset;

public sealed class ForceMfaResetCommandHandler(
    ICurrentCentralUserContext currentUser,
    ICentralIdentityProvider centralIdentity,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<ForceMfaResetCommandHandler> logger)
    : IRequestHandler<ForceMfaResetCommand, Result>
{
    public async Task<Result> Handle(ForceMfaResetCommand request, CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || currentUser.CentralUserId is null)
            return Result.Failure("Identity.Unauthenticated", "Se requiere autenticación válida.");
        if (!currentUser.IsGlobalMasterAdmin)
            return Result.Failure(
                "Saas.MasterOnly", "Solo el master admin puede ejecutar este reset.");

        var target = await centralIdentity.FindByIdAsync(request.CentralUserPublicId, ct);
        if (target is null)
            return Result.Failure("Identity.UserNotFound", "El usuario no existe.");

        await centralIdentity.ResetMfaAsync(request.CentralUserPublicId, ct);

        var now = clock.UtcNow;
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: string.Empty,
                UserId: currentUser.CentralUserId.Value.ToString("N"),
                UserName: currentUser.Email ?? string.Empty,
                Action: AuditEventTypes.CentralUserMfaResetByMaster,
                EntityType: nameof(Domain.Entities.Admin.CentralUser),
                EntityPublicId: request.CentralUserPublicId.ToString("N"),
                Module: "Identity",
                OldValuesJson: null,
                NewValuesJson: System.Text.Json.JsonSerializer.Serialize(new { reason = request.Reason }),
                ChangedFields: new[] { "MfaSecret", "TwoFactorEnabled", "SecurityStamp" },
                IpAddress: null, UserAgent: null,
                Endpoint: "/api/saas/users/{centralUserPublicId}/force-mfa-reset",
                HttpMethod: "POST", HttpStatusCode: 204, DurationMs: null,
                OccurredAt: now), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditWriter falló para CentralUser.MfaResetByMaster");
        }

        return Result.Success();
    }
}
