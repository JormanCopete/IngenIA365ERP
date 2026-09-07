using System.ComponentModel.DataAnnotations;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// Passkey o llave de seguridad WebAuthn. Segundo valor del discriminador
/// <c>CredentialType</c> de <c>ADM_MfaCredentials</c>, hermana de
/// <see cref="TotpCredential"/>.
///
/// <para>
/// <b>Nada de lo que guarda es secreto.</b> <see cref="PublicKeyCose"/> es la
/// clave PÚBLICA; la privada nunca sale del autenticador. Por eso —a diferencia
/// del TOTP— aquí no interviene el llavero de DataProtection: no hay nada que
/// cifrar, y quien lea esta tabla no puede suplantar a nadie. Es justo lo que
/// hace a un passkey más fuerte que un secreto compartido.
/// </para>
///
/// <para>
/// <b>Por qué <see cref="SignCount"/> es <c>long</c> y no <c>uint</c>.</b> El
/// protocolo lo define como uint32, pero <c>uint</c> no tiene mapeo por defecto en
/// SQL Server y en Npgsql está ocupado por <c>xid</c> — que es justamente lo que
/// las convenciones del proyecto usan para <c>xmin</c>. Con <c>long</c> salen
/// <c>bigint</c> en los dos motores.
/// </para>
///
/// <para>
/// <b>Perder una escritura del contador no deja a nadie fuera</b>, y esa dirección
/// es deliberada: si no se persiste, el próximo ingreso compara contra un valor
/// más viejo, el nuevo es mayor, y pasa. Al revés —guardar uno adelantado— habría
/// bloqueado la llave para siempre.
/// </para>
/// </summary>
public sealed class WebAuthnCredential : MfaCredential
{
    /// <summary>Tope del protocolo para un credential id (WebAuthn L2, §5.8.3).</summary>
    public const int LongitudMaximaCredentialId = 1023;

    /// <summary>
    /// Holgura para una clave COSE. La mayor de uso real es RSA-4096 (~550 bytes);
    /// ES256, que es lo habitual, mide ~77.
    ///
    /// <para>
    /// En PostgreSQL la columna sale <c>bytea</c> SIN límite, porque las
    /// convenciones portables anulan el tipo explícito. O sea que el único sitio
    /// donde este número se respeta en los dos motores es la guarda de la factory.
    /// Sin ella, una clave larga se guardaría bien en el PostgreSQL de desarrollo y
    /// reventaría sólo en una instalación SQL Server.
    /// </para>
    /// </summary>
    public const int LongitudMaximaClavePublica = 1024;

    /// <summary>52 caracteres es el peor caso real (los seis transportes); 128 da aire.</summary>
    public const int LongitudMaximaTransportes = 128;

    public const int LongitudMaximaFormatoAtestacion = 32;

    /// <summary>
    /// Identificador que devuelve el autenticador. Único GLOBALMENTE, incluso
    /// entre personas distintas: el ingreso resuelve la credencial por este valor
    /// antes de saber de quién es.
    /// </summary>
    [MaxLength(LongitudMaximaCredentialId)]
    public byte[] CredentialId { get; private set; } = [];

    /// <summary>Clave pública en formato COSE_Key (RFC 8152). Pública: no se cifra.</summary>
    [MaxLength(LongitudMaximaClavePublica)]
    public byte[] PublicKeyCose { get; private set; } = [];

    /// <summary>Contador de firmas del autenticador. Sirve para detectar clonado.</summary>
    public long SignCount { get; private set; }

    /// <summary>Modelo del autenticador. NULL cuando el fabricante no lo declara.</summary>
    public Guid? AaGuid { get; private set; }

    /// <summary>
    /// Transportes que declara la llave, como texto (<c>["internal","hybrid"]</c>).
    /// Se guarda así y no como tabla hija porque sólo sirve para que el navegador
    /// elija el diálogo adecuado; nadie consulta por él.
    /// </summary>
    [MaxLength(LongitudMaximaTransportes)]
    public string? Transports { get; private set; }

    /// <summary>La llave puede sincronizarse entre dispositivos (passkey de plataforma).</summary>
    public bool IsBackupEligible { get; private set; }

    /// <summary>Ahora mismo está sincronizada.</summary>
    public bool IsBackedUp { get; private set; }

    [MaxLength(LongitudMaximaFormatoAtestacion)]
    public string? AttestationFormat { get; private set; }

    // EF Core
    private WebAuthnCredential() { }

    public static WebAuthnCredential Inscribir(
        Guid centralUserId,
        byte[] credentialId,
        byte[] publicKeyCose,
        long signCount,
        Guid? aaGuid,
        string? transportsJson,
        bool isBackupEligible,
        bool isBackedUp,
        string? attestationFormat,
        string? label,
        DateTime utcNow,
        string? inscritaPor)
    {
        ExigirPersona(centralUserId);
        ExigirBytes(credentialId, LongitudMaximaCredentialId, nameof(credentialId), "El credential id");
        ExigirBytes(publicKeyCose, LongitudMaximaClavePublica, nameof(publicKeyCose), "La clave pública");

        var credencial = new WebAuthnCredential
        {
            CentralUserId = centralUserId,
            CredentialId = credentialId,
            PublicKeyCose = publicKeyCose,
            SignCount = signCount,
            AaGuid = aaGuid == Guid.Empty ? null : aaGuid,
            Transports = Recortar(transportsJson, LongitudMaximaTransportes),
            IsBackupEligible = isBackupEligible,
            IsBackedUp = isBackedUp,
            AttestationFormat = Recortar(attestationFormat, LongitudMaximaFormatoAtestacion),
            ConfirmedAt = utcNow,
            Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
        };
        credencial.SellarAlta(utcNow, inscritaPor);
        return credencial;
    }

    /// <summary>
    /// Sella el contador tras una aserción correcta. Devuelve <c>false</c> si no
    /// cambió nada, para que el llamador se ahorre un UPDATE por cada ingreso: la
    /// mayoría de las passkeys de plataforma reportan siempre 0.
    /// </summary>
    public bool ActualizarContador(long nuevoContador, bool respaldada)
    {
        var cambio = nuevoContador > SignCount || respaldada != IsBackedUp;
        if (!cambio) return false;

        if (nuevoContador > SignCount) SignCount = nuevoContador;
        IsBackedUp = respaldada;
        return true;
    }

    private static string? Recortar(string? valor, int tope) =>
        string.IsNullOrWhiteSpace(valor) ? null
        : valor.Length <= tope ? valor
        : valor[..tope];

    private static void ExigirBytes(byte[] valor, int tope, string parametro, string queEs)
    {
        if (valor is null || valor.Length == 0)
        {
            throw new ArgumentException($"{queEs} es obligatorio.", parametro);
        }

        if (valor.Length > tope)
        {
            throw new ArgumentException(
                $"{queEs} mide {valor.Length} bytes y la columna admite {tope}. Guardarlo lo " +
                "truncaría y la llave dejaría de servir sin que apareciera ningún error.",
                parametro);
        }
    }
}
