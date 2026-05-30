namespace IngenIA365ERP.Audit.Configuration;

/// <summary>
/// Configuración del firmador HMAC para exports de audit log (T089).
///
/// <para>
/// Mapea desde la sección <c>AuditSignature</c> en <c>appsettings.json</c>.
/// En producción, los secretos deben venir de Azure KeyVault / AWS Secrets
/// Manager respaldando DataProtection — nunca en JSON commited.
/// </para>
///
/// <para>
/// <b>Rotación</b>: para rotar la clave, agrega la nueva en
/// <see cref="Keys"/> con un nuevo <see cref="AuditSignatureKey.Version"/>,
/// promueve <see cref="CurrentKeyVersion"/> y mantén la anterior en la
/// lista hasta que ningún PDF emitido bajo ella esté en uso (5 años — TTL
/// del audit log).
/// </para>
/// </summary>
public sealed class AuditSignatureSettings
{
    public const string SectionName = "AuditSignature";

    /// <summary>Versión activa para firmar nuevos PDFs.</summary>
    public string CurrentKeyVersion { get; set; } = "dev-v1";

    /// <summary>Claves disponibles (current + previas vigentes).</summary>
    public List<AuditSignatureKey> Keys { get; set; } =
    [
        new()
        {
            Version = "dev-v1",
            // Default DEV ONLY — sobreescribir en prod (32 bytes base64).
            SecretBase64 = "ZGV2ZWxvcG1lbnQtb25seS1zZWNyZXQtZG8tbm90LXVzZS1pbi1wcm9k"
        }
    ];
}

public sealed class AuditSignatureKey
{
    public string Version { get; set; } = string.Empty;
    public string SecretBase64 { get; set; } = string.Empty;
}
