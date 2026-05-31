using System.Security.Cryptography;
using System.Text;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Auth.Logout;

public sealed class LogoutCommandHandler(
    ICentralRefreshTokenStore refreshStore,
    ICurrentCentralUserContext currentUser,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<LogoutCommandHandler> logger)
    : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            var hash = HashHex(request.RefreshToken);
            await refreshStore.DeleteAsync(hash, ct);
        }

        if (currentUser.IsAuthenticated && currentUser.CentralUserId.HasValue)
        {
            await EmitAuditAsync(
                currentUser.CentralUserId.Value,
                currentUser.Email ?? string.Empty,
                clock.UtcNow, ct);
        }

        return Result.Success();
    }

    private async Task EmitAuditAsync(
        Guid centralUserId, string email, DateTime now, CancellationToken ct)
    {
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: string.Empty,
                UserId: centralUserId.ToString("N"),
                UserName: email,
                Action: AuditEventTypes.CentralUserLogout,
                EntityType: nameof(Domain.Entities.Admin.CentralUser),
                EntityPublicId: centralUserId.ToString("N"),
                Module: "Identity",
                OldValuesJson: null,
                NewValuesJson: null,
                ChangedFields: null,
                IpAddress: null,
                UserAgent: null,
                Endpoint: "/api/auth/logout",
                HttpMethod: "POST",
                HttpStatusCode: 204,
                DurationMs: null,
                OccurredAt: now), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditAppendOnlyWriter falló para Logout");
        }
    }

    private static string HashHex(string plainToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(plainToken));
        return Convert.ToHexString(hash);
    }
}
