using System.Security.Cryptography;
using IngenIA365ERP.Identity.Configuration;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Identity.KeyManagement;

/// <summary>
/// Gestor de la clave RSA usada para firmar y validar JWT RS256. Carga la
/// clave PEM desde <see cref="JwtSettings.PrivateKeyPath"/>.
///
/// <para>
/// Si el archivo no existe: en <b>Production</b> el arranque FALLA (una clave
/// efímera emitiría tokens que mueren en cada reinicio y rompería la sesión
/// de todos los usuarios en silencio); en dev/tests se genera una clave en
/// memoria para no exigir secretos montados.
/// </para>
/// </summary>
public interface IRsaKeyProvider
{
    RSA GetKey();
}

public class RsaKeyProvider : IRsaKeyProvider, IDisposable
{
    private readonly RSA _rsa;

    public RsaKeyProvider(IOptions<JwtSettings> settings)
    {
        _rsa = RSA.Create();
        var path = settings.Value.PrivateKeyPath;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            _rsa.ImportFromPem(File.ReadAllText(path));
        }
        else
        {
            RsaKeyGuard.ThrowIfProduction(path);
            _rsa.KeySize = 2048;
        }
    }

    public RSA GetKey() => _rsa;

    public void Dispose() => _rsa.Dispose();
}

/// <summary>
/// Guardia compartida: prohíbe el fallback a clave RSA efímera cuando el
/// entorno es Production (o no está definido — el default de ASP.NET Core
/// ES Production). Hardening surgido del integration test T118, donde el
/// fallback silencioso produjo emisor y validador con claves distintas.
/// </summary>
internal static class RsaKeyGuard
{
    public static void ThrowIfProduction(string? configuredPath)
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                  ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        var isProduction = string.IsNullOrWhiteSpace(env)
                           || env.Equals("Production", StringComparison.OrdinalIgnoreCase);
        if (isProduction)
        {
            throw new InvalidOperationException(
                $"Clave RSA de firma JWT no encontrada en '{configuredPath}'. " +
                "En Production la clave PEM debe estar montada (JwtSettings:PrivateKeyPath); " +
                "sin ella se generaría una clave efímera y todos los tokens emitidos " +
                "morirían en cada reinicio del servicio.");
        }
    }
}
