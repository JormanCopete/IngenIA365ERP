namespace IngenIA365ERP.Application.Common.Interfaces.Identity;

/// <summary>
/// Almacén efímero (Redis) del secret + recovery codes generados al iniciar
/// un enrollment de MFA, mientras el usuario confirma con el primer TOTP.
/// TTL típico 10 minutos — si el usuario no confirma a tiempo, el flujo se
/// reinicia desde Begin.
///
/// <para>
/// Se almacena el secret en CLARO en Redis (no cifrado) porque la vida es
/// corta y el alcance de exposición está acotado a la pre-confirmación.
/// Una vez confirmado, se persiste cifrado en <c>ADM_CentralUsers.MfaSecret</c>
/// vía <see cref="ICentralIdentityProvider.ConfirmMfaSetupAsync"/>.
/// </para>
/// </summary>
public interface IMfaPendingStore
{
    Task StoreAsync(
        Guid centralUserId,
        MfaPendingEnrollment pending,
        TimeSpan ttl,
        CancellationToken ct);

    Task<MfaPendingEnrollment?> GetAsync(Guid centralUserId, CancellationToken ct);

    Task ClearAsync(Guid centralUserId, CancellationToken ct);
}

public sealed record MfaPendingEnrollment(
    string SecretBase32,
    IReadOnlyList<string> RecoveryCodes,
    DateTime CreatedAt);
