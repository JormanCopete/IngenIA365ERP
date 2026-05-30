using System.Security.Cryptography;
using System.Text;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using Microsoft.AspNetCore.DataProtection;
using OtpNet;
using QRCoder;

namespace IngenIA365ERP.Identity.Services;

/// <summary>
/// Implementación TOTP RFC 6238 con Otp.NET. Ventana ±1 (30 s antes/después).
/// El secreto se cifra con DataProtection antes de persistirse en <c>SEC_Users.MfaSecret</c>.
/// </summary>
public class TotpService : ITotpService
{
    private const string ProtectorPurpose = "IngenIA365ERP.Mfa.Totp.v1";
    private const int SecretByteLength = 20; // 160 bits
    private const int TotpStep = 30;

    private readonly IDataProtector _protector;

    public TotpService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(ProtectorPurpose);
    }

    public string GenerateSecret()
    {
        var bytes = new byte[SecretByteLength];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Base32Encoding.ToString(bytes).TrimEnd('=');
    }

    public bool Verify(string base32Secret, string totpCode)
    {
        if (string.IsNullOrWhiteSpace(base32Secret) || string.IsNullOrWhiteSpace(totpCode))
        {
            return false;
        }

        var secretBytes = Base32Encoding.ToBytes(base32Secret);
        var totp = new Totp(secretBytes, step: TotpStep, mode: OtpHashMode.Sha1, totpSize: 6);
        return totp.VerifyTotp(totpCode, out _, new VerificationWindow(previous: 1, future: 1));
    }

    public string BuildOtpAuthUri(string base32Secret, string accountName, string issuer)
    {
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedAccount = Uri.EscapeDataString(accountName);
        return $"otpauth://totp/{encodedIssuer}:{encodedAccount}"
             + $"?secret={base32Secret}&issuer={encodedIssuer}&algorithm=SHA1&digits=6&period={TotpStep}";
    }

    public string BuildQrCodeSvg(string otpAuthUri)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(otpAuthUri, QRCodeGenerator.ECCLevel.Q);
        var svg = new SvgQRCode(data);
        return svg.GetGraphic(4);
    }

    public string ProtectSecret(string base32Secret) =>
        Convert.ToBase64String(_protector.Protect(Encoding.UTF8.GetBytes(base32Secret)));

    public string UnprotectSecret(string protectedSecret) =>
        Encoding.UTF8.GetString(_protector.Unprotect(Convert.FromBase64String(protectedSecret)));
}
