using System.ComponentModel.DataAnnotations;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// Token de un solo uso del flujo "olvidé mi contraseña" (FR-044, Phase 4b).
/// Se persiste el HASH SHA-256 del plano — el plano nunca toca disco.
/// Maps to <c>[dbo].[ADM_PasswordResetTokens]</c>.
///
/// <para>
/// Concurrencia: <c>RowVersion</c> + el lock distribuido del handler garantizan
/// que dos clicks simultáneos del enlace no consuman el token dos veces. El
/// UPDATE condicional sobre <c>ConsumedAt</c> dentro del handler también
/// detecta race conditions extremas.
/// </para>
/// </summary>
public class PasswordResetToken : AuditableEntity
{
    /// <summary>FK lógica a <c>ADM_CentralUsers.Id</c>. Sin navigation property
    /// porque EF mapea el bridge <c>CentralUserIdentity</c>, no el POCO.</summary>
    public Guid CentralUserId { get; private set; }

    /// <summary>SHA-256 (32 bytes) del token plano. UNIQUE — un mismo token plano
    /// no puede existir en dos filas.</summary>
    public byte[] TokenHash { get; private set; } = [];

    [MaxLength(45)]
    public string? RequesterIp { get; private set; }

    public DateTime ExpiresAt { get; private set; }

    public DateTime? ConsumedAt { get; private set; }

    // EF Core
    private PasswordResetToken() { }

    public static PasswordResetToken Create(
        Guid centralUserId,
        byte[] tokenHash,
        string? requesterIp,
        DateTime createdAt,
        DateTime expiresAt)
    {
        if (centralUserId == Guid.Empty)
            throw new ArgumentException("CentralUserId requerido.", nameof(centralUserId));
        if (tokenHash is null || tokenHash.Length == 0)
            throw new ArgumentException("TokenHash requerido.", nameof(tokenHash));
        if (expiresAt <= createdAt)
            throw new ArgumentException("ExpiresAt debe ser posterior a CreatedAt.", nameof(expiresAt));

        return new PasswordResetToken
        {
            CentralUserId = centralUserId,
            TokenHash = tokenHash,
            RequesterIp = requesterIp,
            ExpiresAt = expiresAt,
        };
    }

    /// <summary>true si el token está sin consumir y dentro del periodo de validez.</summary>
    public bool IsValid(DateTime now) =>
        ConsumedAt is null && ExpiresAt > now;

    /// <summary>
    /// Marca como consumido. Lanza si ya estaba consumido o expirado — el
    /// handler debe haber filtrado antes pero esta es la última línea de defensa.
    /// </summary>
    public void MarkConsumed(DateTime now)
    {
        if (ConsumedAt is not null)
            throw new InvalidOperationException("El token ya fue consumido.");
        if (ExpiresAt <= now)
            throw new InvalidOperationException("El token expiró.");
        ConsumedAt = now;
    }
}
