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

    /// <summary>
    /// Feature 012 (T38): la versión con que se firman las anclas de la cadena de auditoría
    /// (<c>AuditSignature:AnchorKeyVersion</c>); nula si no está configurada, no está en las claves o es
    /// <c>dev-v1</c> (nunca se ancla con la clave de los PDF).
    /// </summary>
    string? AnchorKeyVersion { get; }

    /// <summary>HMAC-SHA256 en Base64 con la clave de <paramref name="keyVersion"/>; lanza si no existe.</summary>
    string ComputeHmacBase64(byte[] payload, string keyVersion);
}
