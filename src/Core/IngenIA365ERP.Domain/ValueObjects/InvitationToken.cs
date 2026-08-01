using System.Security.Cryptography;
using System.Text;
using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.ValueObjects;

/// <summary>
/// Token de un solo uso para aceptar invitación o resetear contraseña.
/// Solo el <see cref="Hash"/> se persiste; el <see cref="PlainText"/> se entrega al
/// destinatario por correo y NUNCA queda en BD (FR-027, FR-028).
/// </summary>
public sealed class InvitationToken : ValueObject
{
    public const int TokenBytes = 32;          // 256 bits de entropía
    public const int HashBytes = 32;            // SHA-256

    /// <summary>Texto plano Base64Url (43 chars). Solo disponible al crear; al cargar desde BD es null.</summary>
    public string? PlainText { get; }

    /// <summary>Hash SHA-256 del texto plano (bytes crudos). Único en BD.</summary>
    public byte[] Hash { get; }

    private InvitationToken(string? plain, byte[] hash)
    {
        PlainText = plain;
        Hash = hash;
    }

    /// <summary>Genera un token nuevo (plano + hash).</summary>
    public static InvitationToken Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenBytes);
        var plain = Base64UrlEncode(bytes);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(plain));
        return new InvitationToken(plain, hash);
    }

    /// <summary>Reconstruye un value object a partir de un hash ya almacenado en BD.</summary>
    public static InvitationToken FromHash(byte[] hash)
    {
        if (hash is null || hash.Length != HashBytes)
            throw new ArgumentException($"Hash debe ser de {HashBytes} bytes.", nameof(hash));

        return new InvitationToken(null, hash);
    }

    /// <summary>Calcula el hash de un token plano (para lookup contra BD).</summary>
    public static byte[] HashOf(string plainTextToken)
    {
        if (string.IsNullOrWhiteSpace(plainTextToken))
            throw new ArgumentException("Token plano no puede estar vacío.", nameof(plainTextToken));

        return SHA256.HashData(Encoding.UTF8.GetBytes(plainTextToken));
    }

    private static string Base64UrlEncode(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        foreach (var b in Hash)
            yield return b;
    }
}
