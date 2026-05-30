namespace IngenIA365ERP.Application.Common.Interfaces.Security;

/// <summary>
/// Servicio TOTP RFC 6238 (FR-005/006). Genera secretos base32 de 160 bits,
/// emite código actual, valida TOTP con ventana ±1 (30 s), y produce el QR
/// inline (SVG) para la inscripción.
/// </summary>
public interface ITotpService
{
    /// <summary>Genera un secreto aleatorio en base32 (160 bits / 32 chars).</summary>
    string GenerateSecret();

    /// <summary>Valida un código TOTP de 6 dígitos contra un secreto en claro.</summary>
    bool Verify(string base32Secret, string totpCode);

    /// <summary>Construye la URL otpauth:// para inscripción en authenticator apps.</summary>
    string BuildOtpAuthUri(string base32Secret, string accountName, string issuer);

    /// <summary>Renderiza el SVG inline del QR para la URL otpauth provista.</summary>
    string BuildQrCodeSvg(string otpAuthUri);

    /// <summary>Cifra el secreto antes de persistirlo (DataProtection).</summary>
    string ProtectSecret(string base32Secret);

    /// <summary>Descifra el secreto persistido.</summary>
    string UnprotectSecret(string protectedSecret);
}
