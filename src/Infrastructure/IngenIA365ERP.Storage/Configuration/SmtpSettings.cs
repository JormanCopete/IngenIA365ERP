namespace IngenIA365ERP.Storage.Configuration;

public sealed class SmtpSettings
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 25;
    public bool UseStartTls { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string FromAddress { get; set; } = "no-reply@ingenia365.local";
    public string FromName { get; set; } = "IngenIA365 ERP";

    // Backoff inicial en segundos. Los reintentos crecen ×4 con jitter.
    public int InitialBackoffSeconds { get; set; } = 1;
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Huella SHA-256 del ÚNICO certificado autofirmado que se acepta, en
    /// hexadecimal sin separadores. Vacío = sólo se aceptan certificados que
    /// validen contra las autoridades del sistema, que es lo normal.
    /// </summary>
    /// <remarks>
    /// Existe porque hay servidores de correo que se dejaron con el certificado
    /// de fábrica. La alternativa habitual —desactivar la validación— acepta
    /// CUALQUIER certificado, y con eso quien se interponga en la red puede leer
    /// los enlaces de invitación y de restablecimiento de contraseña que van en
    /// esos correos. Fijar la huella acepta ese certificado y ninguno más: si el
    /// servidor cambia, deja de conectar y hay que enterarse.
    ///
    /// Es un parche, no una solución: lo correcto es que el servidor presente un
    /// certificado válido. Mientras tanto, esto es lo menos malo.
    /// </remarks>
    public string? HuellaCertificadoAceptada { get; set; }

    /// <summary>
    /// Segunda cuenta (o segundo relay) por la que sale el correo cuando la principal
    /// agotó sus reintentos. Sección <c>Smtp:Respaldo</c>, misma forma que ésta; se
    /// considera configurada si declara <see cref="Host"/>. Lleva su PROPIO remitente:
    /// el servidor exige que el «De:» sea la cuenta autenticada, así que un remitente
    /// compartido volvería a fallar por lo mismo que se está cubriendo.
    /// </summary>
    /// <remarks>
    /// Cubre una cuenta bloqueada o una contraseña vencida. Si las dos apuntan al mismo
    /// servidor, NO cubre que ese servidor se caiga: para eso el respaldo tiene que ser
    /// otro relay. Un <c>Respaldo</c> dentro del respaldo se ignora.
    /// </remarks>
    public SmtpSettings? Respaldo { get; set; }

    public bool EstaConfigurado => !string.IsNullOrWhiteSpace(Host) && Port > 0;
}
