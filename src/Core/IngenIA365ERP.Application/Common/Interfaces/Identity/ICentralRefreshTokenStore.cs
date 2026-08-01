namespace IngenIA365ERP.Application.Common.Interfaces.Identity;

/// <summary>
/// Almacén distribuido (Redis) de refresh tokens del flujo de identidad central.
/// Coexiste con el <c>IRefreshTokenStore</c> legacy de Fase 0 (que usa
/// <c>int UserId</c>) — éste maneja <c>Guid CentralUserId</c> + family rotation
/// con detección de reuso (FR refresh): si llega un refresh ya rotado, la
/// familia entera se invalida.
///
/// <para>
/// Persistencia por <b>hash hex SHA-256 del token plano</b> (no por token
/// plano) — mismo patrón que invitaciones / password reset, para que un volcado
/// del Redis no exponga tokens utilizables.
/// </para>
/// </summary>
public interface ICentralRefreshTokenStore
{
    /// <summary>Almacena la sesión asociada a un refresh token recién emitido.</summary>
    Task StoreAsync(
        string tokenHashHex,
        CentralRefreshSession session,
        TimeSpan ttl,
        CancellationToken ct);

    /// <summary>Recupera la sesión. <c>null</c> si la key no existe (expirada,
    /// revocada o nunca emitida).</summary>
    Task<CentralRefreshSession?> GetAsync(string tokenHashHex, CancellationToken ct);

    /// <summary>
    /// Marca el token como rotado, apuntándolo al hash del reemplazo.
    /// El registro queda en Redis para detección de reuso: un segundo intento
    /// con el mismo token llega aquí, ve <c>ReplacedByTokenHashHex != null</c>
    /// y dispara <see cref="InvalidateFamilyAsync"/>.
    /// </summary>
    Task MarkRotatedAsync(
        string tokenHashHex,
        string replacedByTokenHashHex,
        CancellationToken ct);

    /// <summary>Elimina el refresh actual (logout explícito).</summary>
    Task DeleteAsync(string tokenHashHex, CancellationToken ct);

    /// <summary>Invalida toda una familia (cascada por <see cref="CentralRefreshSession.FamilyId"/>).
    /// Cualquier futuro <see cref="GetAsync"/> sobre tokens de la familia retornará null
    /// aunque la key todavía exista.</summary>
    Task InvalidateFamilyAsync(Guid familyId, CancellationToken ct);

    /// <summary>true si la familia fue invalidada (por reuso o logout-all).</summary>
    Task<bool> IsFamilyInvalidatedAsync(Guid familyId, CancellationToken ct);
}

/// <summary>
/// Contexto serializado en Redis asociado a un refresh token central.
///
/// <para><c>SecurityStamp</c> es el snapshot del stamp del usuario al momento
/// de emitir el refresh. Cambiar/resetear contraseña y el force-mfa-reset del
/// master regeneran el stamp en <c>ADM_CentralUsers</c>; el refresh compara
/// snapshot vs actual y un mismatch invalida la familia completa. Sesiones
/// serializadas antes de este campo deserializan <c>null</c> → se rechazan
/// (fail-secure: exige un re-login único).</para>
/// </summary>
public sealed record CentralRefreshSession(
    Guid CentralUserId,
    Guid? ActiveTenantPublicId,
    Guid FamilyId,
    DateTime IssuedAt,
    string? IpAddress,
    string? UserAgent,
    string? ReplacedByTokenHashHex,
    string? SecurityStamp);
