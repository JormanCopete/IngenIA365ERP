using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Identity.Services;

public interface IEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}

public class EncryptionService : IEncryptionService
{
    private readonly IDataProtector _protector;
    private readonly ILogger<EncryptionService> _logger;

    private const string Purpose = "IngenIA365ERP.SensitiveData.v1";

    public EncryptionService(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<EncryptionService> logger)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
        _logger = logger;
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        return _protector.Protect(plainText);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return cipherText;

        try
        {
            return _protector.Unprotect(cipherText);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Failed to decrypt data. The key may have been rotated.");
            throw new InvalidOperationException("No se pudo descifrar los datos. La clave pudo haber sido rotada.", ex);
        }
    }
}
