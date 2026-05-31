using System.Security.Cryptography;
using System.Text;
using IngenIA365ERP.Application.Common.Interfaces.Security;

namespace IngenIA365ERP.Application.Common.Services;

/// <summary>
/// Implementación de <see cref="ISecureTokenGenerator"/> usando
/// <see cref="RandomNumberGenerator"/> (CSPRNG del sistema operativo) y
/// SHA-256 para el hash. Stateless — se registra como singleton.
///
/// <para>
/// La codificación Base64Url sigue RFC 4648 §5: <c>+</c> → <c>-</c>,
/// <c>/</c> → <c>_</c>, sin padding <c>=</c>. Producir tokens URL-safe
/// permite incrustrar el plano en query strings sin escapado adicional.
/// </para>
/// </summary>
public sealed class SecureTokenGenerator : ISecureTokenGenerator
{
    public SecureTokenResult Generate(int byteCount = 32)
    {
        if (byteCount < 16)
            throw new ArgumentOutOfRangeException(nameof(byteCount),
                "Mínimo 16 bytes (128 bits) para entropía aceptable.");

        var bytes = new byte[byteCount];
        RandomNumberGenerator.Fill(bytes);

        var plain = Base64UrlEncode(bytes);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(plain));

        return new SecureTokenResult(plain, hash);
    }

    public byte[] HashPlainToken(string plainTokenBase64Url)
    {
        if (string.IsNullOrWhiteSpace(plainTokenBase64Url))
            throw new ArgumentException("El token plano no puede estar vacío.", nameof(plainTokenBase64Url));

        return SHA256.HashData(Encoding.UTF8.GetBytes(plainTokenBase64Url));
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        // RFC 4648 §5: Base64 estándar + sustitución de chars + sin padding.
        var b64 = Convert.ToBase64String(bytes);
        return b64.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
