using System.Security.Cryptography;
using IngenIA365ERP.Application.Common.Interfaces.Security;

namespace IngenIA365ERP.Identity.Services;

/// <summary>
/// Genera 10 códigos alfanuméricos de 10 caracteres en formato XXXXX-XXXXX
/// usando un alfabeto sin caracteres ambiguos (O/0, I/1, L/l). Cada código
/// se hashea con BCrypt cost 11 para guardarse en <c>SEC_MfaBackupCodes</c>.
/// </summary>
public class MfaBackupCodeGenerator : IMfaBackupCodeGenerator
{
    // 32 caracteres, sin O, 0, I, 1, L, l.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int CodeChars = 10;
    private const int BcryptCost = 11;

    public IReadOnlyList<MfaBackupCodePlainHashPair> Generate(int count = 10)
    {
        var list = new List<MfaBackupCodePlainHashPair>(count);
        for (int i = 0; i < count; i++)
        {
            var plain = GeneratePlain();
            var hash = BCrypt.Net.BCrypt.HashPassword(plain, workFactor: BcryptCost);
            list.Add(new MfaBackupCodePlainHashPair(plain, hash));
        }
        return list;
    }

    public bool Verify(string code, string hash)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }
        try
        {
            return BCrypt.Net.BCrypt.Verify(Normalize(code), hash);
        }
        catch
        {
            return false;
        }
    }

    private static string GeneratePlain()
    {
        var buf = new char[CodeChars];
        Span<byte> bytes = stackalloc byte[CodeChars];
        RandomNumberGenerator.Fill(bytes);
        for (int i = 0; i < CodeChars; i++)
        {
            buf[i] = Alphabet[bytes[i] % Alphabet.Length];
        }
        // Formato XXXXX-XXXXX para legibilidad.
        return string.Concat(new string(buf, 0, 5), "-", new string(buf, 5, 5));
    }

    private static string Normalize(string code) => code.Trim().ToUpperInvariant();
}
