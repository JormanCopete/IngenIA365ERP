using BCrypt.Net;
using IngenIA365ERP.Persistence.Identity;
using Microsoft.AspNetCore.Identity;

namespace IngenIA365ERP.Identity.CentralIdentity;

/// <summary>
/// Sustituye el <c>PasswordHasher&lt;TUser&gt;</c> default de ASP.NET Identity
/// (PBKDF2-HMAC-SHA256, 100k iters) por BCrypt cost 11 (T035, research D-02).
///
/// <para>
/// La constitución del proyecto (estándares técnicos) exige BCrypt cost ≥ 11, alineado
/// con Fase 0. Este hasher se registra en <c>AddCentralIdentity()</c> mediante
/// <c>services.AddScoped&lt;IPasswordHasher&lt;CentralUserIdentity&gt;, BcryptPasswordHasher&gt;()</c>.
/// </para>
///
/// <para>
/// <b>Compatibilidad hacia adelante</b>: si en el futuro se sube el cost (p.ej. 12)
/// el método <see cref="VerifyHashedPassword"/> detecta hashes con cost inferior y
/// retorna <see cref="PasswordVerificationResult.SuccessRehashNeeded"/> para que
/// <c>UserManager</c> re-hashee silenciosamente en el siguiente login exitoso.
/// </para>
/// </summary>
internal sealed class BcryptPasswordHasher : IPasswordHasher<CentralUserIdentity>
{
    private const int WorkFactor = 11;

    public string HashPassword(CentralUserIdentity user, string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("La contraseña no puede estar vacía.", nameof(password));

        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public PasswordVerificationResult VerifyHashedPassword(
        CentralUserIdentity user,
        string hashedPassword,
        string providedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword) || string.IsNullOrEmpty(providedPassword))
            return PasswordVerificationResult.Failed;

        try
        {
            var verified = BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword);
            if (!verified) return PasswordVerificationResult.Failed;

            // Si el hash usa un cost menor al actual, marcar para rehash silencioso.
            var currentWorkFactor = ExtractWorkFactor(hashedPassword);
            return currentWorkFactor < WorkFactor
                ? PasswordVerificationResult.SuccessRehashNeeded
                : PasswordVerificationResult.Success;
        }
        catch (SaltParseException)
        {
            // Hash corrupto o de algoritmo desconocido — tratar como contraseña inválida.
            return PasswordVerificationResult.Failed;
        }
    }

    private static int ExtractWorkFactor(string bcryptHash)
    {
        // Formato BCrypt: $2{a|b|y}$NN$... — NN son dos dígitos del cost.
        if (bcryptHash.Length < 7 || bcryptHash[0] != '$') return WorkFactor;

        var costSpan = bcryptHash.AsSpan(4, 2);
        return int.TryParse(costSpan, out var cost) ? cost : WorkFactor;
    }
}
