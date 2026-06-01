using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Identity.Profile.Services;
using IngenIA365ERP.Domain.Entities.Admin;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Profile.RequestPasswordReset;

public sealed class RequestPasswordResetCommandHandler(
    ICentralIdentityProvider centralIdentity,
    IAdminDbContext adminDb,
    ISecureTokenGenerator tokens,
    IPasswordResetEmailDispatcher emailDispatcher,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<RequestPasswordResetCommandHandler> logger)
    : IRequestHandler<RequestPasswordResetCommand, Result>
{
    private static readonly TimeSpan TokenTtl = TimeSpan.FromHours(1);

    public async Task<Result> Handle(RequestPasswordResetCommand request, CancellationToken ct)
    {
        var displayEmail = request.Email.Trim();
        var now = clock.UtcNow;

        var user = await centralIdentity.FindByEmailAsync(displayEmail, ct);
        if (user is null)
        {
            // Email inexistente: log + audit, pero responder éxito (FR-041).
            logger.LogInformation(
                "Password reset solicitado para email inexistente: {Email}.", displayEmail);
            await EmitAuditAsync(
                centralUserId: null,
                email: displayEmail,
                action: AuditEventTypes.ProfilePasswordResetRequestedNoSuchEmail,
                request, now, ct);
            return Result.Success();
        }

        // Supersede tokens previos NO consumidos del mismo usuario — solo un
        // enlace activo a la vez (UPDATE en lugar de DELETE para preservar la
        // historia auditable; los soft-deletamos).
        var previous = await adminDb.PasswordResetTokens
            .Where(t => t.CentralUserId == user.Id && t.ConsumedAt == null)
            .ToListAsync(ct);
        foreach (var p in previous)
        {
            p.MarkConsumed(now); // efectivamente los inutiliza
        }

        var (plainToken, hash) = tokens.Generate();
        var resetToken = PasswordResetToken.Create(
            centralUserId: user.Id,
            tokenHash: hash,
            requesterIp: request.IpAddress,
            createdAt: now,
            expiresAt: now.Add(TokenTtl));
        adminDb.PasswordResetTokens.Add(resetToken);
        await adminDb.SaveChangesAsync(ct);

        try
        {
            await emailDispatcher.DispatchAsync(
                toEmail: user.Email,
                recipientName: user.Email,
                plainTokenBase64Url: plainToken,
                expiresAt: resetToken.ExpiresAt,
                requesterIp: request.IpAddress,
                ct: ct);
        }
        catch (Exception ex)
        {
            // Mantenemos el token en BD aunque el envío falle — el usuario puede
            // reintentar el flow forgot-password sin consecuencias.
            logger.LogWarning(ex, "Falló envío del email de reset para {Email}", user.Email);
        }

        await EmitAuditAsync(
            centralUserId: user.Id,
            email: user.Email,
            action: AuditEventTypes.ProfilePasswordResetRequested,
            request, now, ct);

        return Result.Success();
    }

    private async Task EmitAuditAsync(
        Guid? centralUserId,
        string email,
        string action,
        RequestPasswordResetCommand request,
        DateTime now,
        CancellationToken ct)
    {
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: string.Empty,
                UserId: centralUserId?.ToString("N") ?? string.Empty,
                UserName: email,
                Action: action,
                EntityType: nameof(Domain.Entities.Admin.CentralUser),
                EntityPublicId: centralUserId?.ToString("N") ?? string.Empty,
                Module: "Identity",
                OldValuesJson: null,
                NewValuesJson: null,
                ChangedFields: null,
                IpAddress: request.IpAddress,
                UserAgent: null,
                Endpoint: "/api/auth/password/forgot",
                HttpMethod: "POST",
                HttpStatusCode: 202,
                DurationMs: null,
                OccurredAt: now), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditWriter falló para {Action}", action);
        }
    }
}
