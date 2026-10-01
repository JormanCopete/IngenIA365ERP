namespace IngenIA365ERP.Application.Common.Approvals;

/// <summary>
/// Dónde viven los desafíos de la aprobación presencial y los pasos TOTP ya usados (feature 012, T33, T085;
/// contracts/api.md §15.2; nuevo). La implementación está en la ranura de caché de la <b>cooperativa</b>
/// (<c>ITenantCacheSlots</c>): un desafío o un código de una cooperativa no sirve en otra.
/// </summary>
public interface IDesafiosDePresencia
{
    /// <summary>Guarda el desafío hasta que venza.</summary>
    Task GuardarAsync(DesafioDePresencia desafio, TimeSpan vigencia, CancellationToken ct);

    /// <summary>Lee y borra el desafío (un solo intento por desafío); nulo si venció o no existe.</summary>
    Task<DesafioDePresencia?> ConsumirAsync(Guid publicId, CancellationToken ct);

    /// <summary>
    /// Marca un código TOTP de un aprobador como usado. Devuelve <c>false</c> si ya lo estaba: el mismo código no
    /// aprueba dos veces (<c>Approvals.Presence.TotpReused</c>).
    /// </summary>
    Task<bool> MarcarTotpUsadoAsync(Guid centralUserId, string codigo, TimeSpan vigencia, CancellationToken ct);
}

/// <summary>
/// Un desafío de aprobación presencial (nuevo): para qué solicitud, desde qué sesión (<c>SEC_Users.Id</c> del
/// solicitante), a qué aprobador, con qué métodos y, si tiene passkeys, las opciones WebAuthn que se le presentaron
/// (hay que verificar contra ellas y no contra otras).
/// </summary>
public sealed record DesafioDePresencia(
    Guid PublicId,
    Guid RequestPublicId,
    int RequesterUserId,
    int ApproverUserId,
    Guid ApproverCentralUserId,
    string ApproverName,
    IReadOnlyList<string> Methods,
    string? OpcionesWebAuthnJson,
    DateTime ExpiresAt);
