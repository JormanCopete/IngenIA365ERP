namespace IngenIA365ERP.Application.Common.Interfaces.Security;

/// <summary>
/// Genera y verifica códigos de respaldo MFA (FR-013). Cada generación
/// produce 10 códigos pertenecientes a un único <c>BatchId</c>; el hash
/// se guarda con BCrypt cost 11.
/// </summary>
public interface IMfaBackupCodeGenerator
{
    /// <summary>
    /// Genera <paramref name="count"/> códigos en claro junto a sus hashes.
    /// Los códigos en claro se muestran al usuario una sola vez (post-enroll
    /// o regenerar) y luego se descartan.
    /// </summary>
    IReadOnlyList<MfaBackupCodePlainHashPair> Generate(int count = 10);

    /// <summary>Compara un código en claro con su hash BCrypt.</summary>
    bool Verify(string code, string hash);
}

public sealed record MfaBackupCodePlainHashPair(string PlainCode, string Hash);
