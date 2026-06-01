using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Caching;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Profile.ResetPassword;

public sealed class ResetPasswordCommandHandler(
    ICentralIdentityProvider centralIdentity,
    IAdminDbContext adminDb,
    ISecureTokenGenerator tokens,
    IDistributedLock distributedLock,
    IAuditAppendOnlyWriter auditWriter,
    IDateTimeService clock,
    ILogger<ResetPasswordCommandHandler> logger)
    : IRequestHandler<ResetPasswordCommand, Result>
{
    private static readonly TimeSpan LockTtl = TimeSpan.FromSeconds(30);

    public async Task<Result> Handle(ResetPasswordCommand request, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var hash = tokens.HashPlainToken(request.Token);
        var hashHex = Convert.ToHexString(hash);

        // 1) Lock distribuido por tokenHash.
        await using var handle = await distributedLock.TryAcquireAsync(
            $"lock:pwdreset:{hashHex}", LockTtl, ct);
        if (handle is null)
        {
            return Result.Failure(
                "Profile.PasswordReset.LockBusy",
                "El enlace está siendo procesado por otra sesión.");
        }

        var token = await adminDb.PasswordResetTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (token is null)
        {
            return Result.Failure(
                "Profile.PasswordReset.Invalid",
                "El enlace no corresponde a una solicitud activa.");
        }

        if (token.ConsumedAt is not null)
        {
            return Result.Failure(
                "Profile.PasswordReset.AlreadyConsumed",
                "Este enlace ya fue usado. Solicita uno nuevo si lo necesitas.");
        }
        if (token.ExpiresAt <= now)
        {
            return Result.Failure(
                "Profile.PasswordReset.Expired",
                "El enlace expiró. Solicita uno nuevo.");
        }

        // 2) Aplicar el nuevo password — ICentralIdentityProvider corre Pwned check
        //    interno + regenera SecurityStamp → invalida refresh tokens activos.
        var result = await centralIdentity.AdminResetPasswordAsync(
            token.CentralUserId, request.NewPassword, ct);
        if (!result.Succeeded)
        {
            var code = result.ErrorCodes.FirstOrDefault() ?? "Profile.PasswordReset.Failed";
            return Result.Failure(code, MapErrorMessage(code));
        }

        // 3) Marcar token consumido. UPDATE condicional sobre RowVersion lo
        //    garantiza el interceptor del proyecto.
        token.MarkConsumed(now);

        try
        {
            await adminDb.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(
                "Profile.PasswordReset.AlreadyConsumed",
                "Este enlace fue procesado simultáneamente. Verifica si tu contraseña ya cambió.");
        }

        var user = await centralIdentity.FindByIdAsync(token.CentralUserId, ct);
        await EmitAuditAsync(
            token.CentralUserId,
            user?.Email ?? string.Empty,
            AuditEventTypes.ProfilePasswordResetConsumed,
            now, ct);

        logger.LogInformation(
            "Password reset completado para CentralUser {UserId}.", token.CentralUserId);

        return Result.Success();
    }

    private async Task EmitAuditAsync(
        Guid centralUserId, string email, string action, DateTime now, CancellationToken ct)
    {
        try
        {
            await auditWriter.AppendAsync(new AuditEventDocument(
                TenantId: string.Empty,
                UserId: centralUserId.ToString("N"),
                UserName: email,
                Action: action,
                EntityType: nameof(Domain.Entities.Admin.CentralUser),
                EntityPublicId: centralUserId.ToString("N"),
                Module: "Identity",
                OldValuesJson: null,
                NewValuesJson: null,
                ChangedFields: null,
                IpAddress: null,
                UserAgent: null,
                Endpoint: "/api/auth/password/reset",
                HttpMethod: "POST",
                HttpStatusCode: 204,
                DurationMs: null,
                OccurredAt: now), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AuditWriter falló para {Action}", action);
        }
    }

    private static string MapErrorMessage(string code) => code switch
    {
        "Identity.Password.Pwned" =>
            "La contraseña aparece en filtraciones públicas; elige otra.",
        _ => "No se pudo restablecer la contraseña.",
    };
}
