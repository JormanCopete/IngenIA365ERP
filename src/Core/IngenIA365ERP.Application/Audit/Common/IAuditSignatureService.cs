namespace IngenIA365ERP.Application.Audit.Common;

/// <summary>
/// Firma y verifica el HMAC-SHA256 sobre los bytes del PDF exportado
/// (T089 + T090). La clave se mantiene en configuración (administrada por
/// el mismo vault que respalda DataProtection en producción) y se versiona
/// para permitir rotación sin invalidar PDFs antiguos.
///
/// <para>
/// <b>Multi-key</b>: <see cref="VerifyHmacBase64"/> acepta una
/// <c>keyVersion</c> explícita para que el verificador SaaS pueda comprobar
/// PDFs firmados con claves anteriores aún vigentes.
/// </para>
/// </summary>
public interface IAuditSignatureService
{
    /// <summary>Identificador de la clave activa hoy.</summary>
    string CurrentKeyVersion { get; }

    /// <summary>HMAC-SHA256 en Base64 del payload con la clave activa.</summary>
    string ComputeHmacBase64(byte[] payload);

    /// <summary>
    /// Verifica el HMAC con la clave correspondiente a <paramref name="keyVersion"/>.
    /// Devuelve <c>false</c> si la versión es desconocida o el HMAC no coincide.
    /// </summary>
    bool VerifyHmacBase64(byte[] payload, string hmacBase64, string keyVersion);
}
