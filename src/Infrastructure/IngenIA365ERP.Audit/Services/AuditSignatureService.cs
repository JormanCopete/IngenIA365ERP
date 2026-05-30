using System.Security.Cryptography;
using IngenIA365ERP.Application.Audit.Common;
using IngenIA365ERP.Audit.Configuration;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Audit.Services;

/// <summary>
/// HMAC-SHA256 sobre bytes del PDF con claves versionadas (T089/T090).
/// Multi-key para que el verificador acepte PDFs firmados con claves
/// previas vigentes (ver <see cref="AuditSignatureSettings"/>).
/// </summary>
public sealed class AuditSignatureService : IAuditSignatureService
{
    private readonly AuditSignatureSettings _settings;
    private readonly Dictionary<string, byte[]> _keyMaterialByVersion;

    public AuditSignatureService(IOptions<AuditSignatureSettings> settings)
    {
        _settings = settings.Value;
        _keyMaterialByVersion = _settings.Keys.ToDictionary(
            k => k.Version,
            k => Convert.FromBase64String(k.SecretBase64),
            StringComparer.Ordinal);

        if (!_keyMaterialByVersion.ContainsKey(_settings.CurrentKeyVersion))
        {
            throw new InvalidOperationException(
                $"AuditSignature: CurrentKeyVersion '{_settings.CurrentKeyVersion}' " +
                "no está presente en Keys[]. Revisa la configuración.");
        }
    }

    public string CurrentKeyVersion => _settings.CurrentKeyVersion;

    public string ComputeHmacBase64(byte[] payload)
    {
        var key = _keyMaterialByVersion[_settings.CurrentKeyVersion];
        var hmac = HMACSHA256.HashData(key, payload);
        return Convert.ToBase64String(hmac);
    }

    public bool VerifyHmacBase64(byte[] payload, string hmacBase64, string keyVersion)
    {
        if (!_keyMaterialByVersion.TryGetValue(keyVersion, out var key))
        {
            return false;
        }
        byte[] provided;
        try { provided = Convert.FromBase64String(hmacBase64); }
        catch (FormatException) { return false; }

        var computed = HMACSHA256.HashData(key, payload);
        // Comparación tiempo-constante para evitar timing attacks.
        return CryptographicOperations.FixedTimeEquals(provided, computed);
    }
}
