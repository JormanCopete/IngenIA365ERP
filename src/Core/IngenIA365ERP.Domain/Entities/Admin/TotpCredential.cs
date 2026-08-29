using System.ComponentModel.DataAnnotations;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// Autenticador TOTP (RFC 6238) — Google Authenticator, Microsoft Authenticator y
/// cualquier otro que consuma un <c>otpauth://</c> estándar. Discriminador
/// <c>'Totp'</c>.
///
/// <para>
/// <b>Lo que guarda NO es el secreto.</b> <see cref="SecretProtected"/> es lo que
/// devuelve <c>IDataProtector.Protect(base32)</c>: texto, no binario, atado al
/// purpose <c>central-identity:mfa-secret</c> y al llavero de DataProtection.
/// Domain no descifra nada — eso lo hace <c>AspNetCoreIdentityProvider</c> con el
/// mismo protector que lo cifró.
/// </para>
///
/// <para>
/// De ahí que el traslado desde <c>ADM_CentralUsers.MfaSecret</c> sea una copia
/// literal de la cadena: cualquier recodificación por el camino —a binario, a
/// Base64 estándar, a otro ancho de columna— la rompe, y el fallo es MUDO, porque
/// la verificación captura la excepción de criptografía y responde «código
/// inválido» sin decir por qué.
/// </para>
/// </summary>
public sealed class TotpCredential : MfaCredential
{
    /// <summary>
    /// Ancho de la columna. El origen mide 512; 1024 da holgura y garantiza que la
    /// copia nunca trunque. Truncar el ciphertext lo vuelve indescifrable para
    /// siempre, y en silencio.
    /// </summary>
    public const int LongitudMaxima = 1024;

    [MaxLength(LongitudMaxima)]
    public string SecretProtected { get; private set; } = string.Empty;

    // EF Core
    private TotpCredential() { }

    public static TotpCredential Inscribir(
        Guid centralUserId,
        string secretoProtegido,
        string? label,
        DateTime utcNow,
        string? inscritaPor)
    {
        ExigirPersona(centralUserId);
        ExigirSecreto(secretoProtegido);

        var credencial = new TotpCredential
        {
            CentralUserId = centralUserId,
            SecretProtected = secretoProtegido,
            ConfirmedAt = utcNow,
        };
        credencial.Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        credencial.SellarAlta(utcNow, inscritaPor);
        return credencial;
    }

    private static void ExigirSecreto(string secretoProtegido)
    {
        if (string.IsNullOrWhiteSpace(secretoProtegido))
        {
            throw new ArgumentException(
                "El secreto protegido es obligatorio.", nameof(secretoProtegido));
        }

        if (secretoProtegido.Length > LongitudMaxima)
        {
            throw new ArgumentException(
                $"El secreto protegido mide {secretoProtegido.Length} caracteres y la columna admite " +
                $"{LongitudMaxima}. Guardarlo lo truncaría y la persona perdería su segundo factor sin " +
                "que apareciera ningún error.",
                nameof(secretoProtegido));
        }
    }
}
