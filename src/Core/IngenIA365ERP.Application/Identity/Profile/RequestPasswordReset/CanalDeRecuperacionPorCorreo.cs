using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Identity;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Identity.Profile.Services;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Profile.RequestPasswordReset;

/// <summary>
/// El canal de recuperación por correo: un token de una hora (sólo un enlace vivo a la vez: los anteriores sin
/// usar se consumen) al correo de la cuenta. Antes vivía dentro del olvido de clave; feature 017 lo saca para
/// que el administrador mande el mismo enlace en vez de fijar él la clave.
/// </summary>
public sealed class CanalDeRecuperacionPorCorreo(
    IAdminDbContext adminDb,
    ISecureTokenGenerator tokens,
    IPasswordResetEmailDispatcher emailDispatcher,
    IDateTimeService clock,
    ILogger<CanalDeRecuperacionPorCorreo> logger) : ICanalDeRecuperacion
{
    private static readonly TimeSpan TokenTtl = TimeSpan.FromHours(1);

    public async Task EnviarEnlaceAsync(CentralUser persona, string? ipDeQuienPide, CancellationToken ct)
    {
        var now = clock.UtcNow;

        // Supersede tokens previos NO consumidos del mismo usuario — solo un
        // enlace activo a la vez (UPDATE en lugar de DELETE para preservar la
        // historia auditable; los soft-deletamos).
        var previous = await adminDb.PasswordResetTokens
            .Where(t => t.CentralUserId == persona.Id && t.ConsumedAt == null)
            .ToListAsync(ct);
        foreach (var p in previous)
        {
            p.MarkConsumed(now); // efectivamente los inutiliza
        }

        var (plainToken, hash) = tokens.Generate();
        var resetToken = PasswordResetToken.Create(
            centralUserId: persona.Id,
            tokenHash: hash,
            requesterIp: ipDeQuienPide,
            createdAt: now,
            expiresAt: now.Add(TokenTtl));
        adminDb.PasswordResetTokens.Add(resetToken);
        await adminDb.SaveChangesAsync(ct);

        try
        {
            await emailDispatcher.DispatchAsync(
                toEmail: persona.Email,
                recipientName: persona.Email,
                plainTokenBase64Url: plainToken,
                expiresAt: resetToken.ExpiresAt,
                requesterIp: ipDeQuienPide,
                ct: ct);
        }
        catch (Exception ex)
        {
            // Mantenemos el token en BD aunque el envío falle — el usuario puede
            // reintentar el flow forgot-password sin consecuencias.
            logger.LogWarning(ex, "Falló envío del email de reset para {Email}", persona.Email);
        }
    }
}
