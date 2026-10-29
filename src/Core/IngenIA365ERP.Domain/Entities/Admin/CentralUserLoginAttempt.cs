using System.ComponentModel.DataAnnotations;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// Telemetría append-only de intentos de login (FR-035, FR-042, FR-046).
/// Maps to [dbo].[ADM_CentralUserLoginAttempts]. Sin <c>AuditableEntity</c> ni soft-delete por diseño
/// — excepción admitida (constitución principio VII: lookup/telemetría inmutable justificada).
/// </summary>
public class CentralUserLoginAttempt
{
    public long Id { get; set; }

    /// <summary>Null si el email no corresponde a ningún <see cref="CentralUser"/> (intento contra cuenta inexistente).</summary>
    public Guid? CentralUserId { get; set; }

    [MaxLength(256)]
    public string NormalizedEmail { get; set; } = string.Empty;

    public LoginAttemptResult Result { get; set; }

    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [MaxLength(512)]
    public string? UserAgent { get; set; }

    public DateTime Timestamp { get; set; }

    /// <summary>Si el intento disparó bloqueo progresivo (research D-11), cuántos segundos.</summary>
    public int? LockoutAppliedSeconds { get; set; }

    /// <summary>Lo que escribió la persona, normalizado (feature 017): correo o usuario de documento.</summary>
    [MaxLength(256)]
    public string? Identifier { get; set; }

    /// <summary>Con qué intentó entrar (feature 017). Nulo en intentos anteriores.</summary>
    public LoginIdentifierKind? IdentifierKind { get; set; }

    public static CentralUserLoginAttempt Record(
        Guid? centralUserId,
        string normalizedEmail,
        LoginAttemptResult result,
        string? ipAddress,
        string? userAgent,
        DateTime now,
        int? lockoutAppliedSeconds = null,
        string? identifier = null,
        LoginIdentifierKind? identifierKind = null) => new()
        {
            Identifier = identifier,
            IdentifierKind = identifierKind,
            CentralUserId = centralUserId,
            NormalizedEmail = normalizedEmail,
            Result = result,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Timestamp = now,
            LockoutAppliedSeconds = lockoutAppliedSeconds,
        };
}

/// <summary>Con qué se identificó la persona al intentar entrar (feature 017).</summary>
public enum LoginIdentifierKind
{
    /// <summary>Un correo (llevaba <c>@</c>).</summary>
    Email = 0,

    /// <summary>Un usuario de documento que corresponde a una cuenta.</summary>
    DocumentUsername = 1,

    /// <summary>Un texto que no corresponde a ninguna cuenta (o un correo sin verificar).</summary>
    Unknown = 2,
}
