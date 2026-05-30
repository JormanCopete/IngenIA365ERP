using System.Security.Cryptography;
using IngenIA365ERP.Identity.Configuration;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Identity.KeyManagement;

/// <summary>
/// Gestor de la clave RSA usada para firmar y validar JWT RS256. Carga la
/// clave PEM desde <see cref="JwtSettings.PrivateKeyPath"/>; si el archivo no
/// existe, genera una clave en memoria (apto solo para entornos efímeros
/// como tests / dev). En producción la clave debe estar persistida (volume
/// montado) y rotarse vía DataProtection KeyManager.
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
            _rsa.KeySize = 2048;
        }
    }

    public RSA GetKey() => _rsa;

    public void Dispose() => _rsa.Dispose();
}
