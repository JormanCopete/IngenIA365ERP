namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// Los dos tipos de autenticador, con los MISMOS literales que la columna
/// discriminadora de <c>ADM_MfaCredentials</c>.
///
/// <para>
/// Vive en Domain, junto a la jerarquía que discrimina, y no en Application:
/// desde aquí lo ven la configuración de EF, la política de métodos y el
/// directorio de credenciales sin que ninguno tenga que copiar la cadena. La
/// configuración de EF los toma de aquí, así que el discriminador que se escribe
/// en la tabla y el que sale por la API son por construcción el mismo.
/// </para>
///
/// <para>
/// La pantalla tiene su propia copia —<c>IngenIA365ERP.Shared</c> no referencia
/// ningún proyecto, como todos sus DTO—, y una prueba de arquitectura comprueba
/// que las dos digan lo mismo.
/// </para>
/// </summary>
public static class TiposDeCredencialMfa
{
    /// <summary>App de códigos: Google Authenticator, Microsoft Authenticator, cualquiera.</summary>
    public const string Totp = "Totp";

    /// <summary>Passkey: la llave del dispositivo, o una física por USB o NFC.</summary>
    public const string WebAuthn = "WebAuthn";
}

/// <summary>
/// Los métodos de segundo factor que una cooperativa puede aceptar. Máscara de
/// bits: una política dice «TOTP y passkey», «sólo passkey», etc.
///
/// <para>
/// <b>No hay miembro para el correo, y es deliberado.</b> El correo es una vía de
/// recuperación, no un factor de ingreso. Si existiera aquí, alguien podría
/// habilitarlo como método aceptado y el segundo factor quedaría degradado al
/// primero: quien tenga acceso al buzón entra. La única forma de que eso no pase
/// es que no se pueda escribir.
/// </para>
///
/// <para>
/// <b>Tampoco hay miembro para el código de recuperación</b>, por otro motivo: un
/// código de recuperación nunca satisface a ninguna política. Sirve para
/// demostrar identidad cuando se perdió el método, y lo que sigue después es
/// inscribir uno nuevo — no entrar como si nada hubiera pasado. Mapearlo a
/// <see cref="Totp"/> sería mentir, y mapearlo a <see cref="Ninguno"/> haría que
/// se rechazara sin explicar por qué.
/// </para>
///
/// <para>
/// Valores numéricos explícitos porque se persisten como entero: renumerarlos
/// reinterpreta las filas ya escritas.
/// </para>
/// </summary>
[Flags]
public enum MetodosMfa
{
    /// <summary>
    /// Ningún método. Como <b>política</b> es un estado ilegal —exigir segundo
    /// factor sin aceptar ninguno encierra a toda la cooperativa— y se rechaza al
    /// escribir. Como <b>método demostrado</b> significa «no consta»: quien no
    /// tiene segundo factor, y también quien trae un token emitido antes de que
    /// esto existiera.
    /// </summary>
    Ninguno = 0,

    /// <summary>App de códigos: Google Authenticator, Microsoft Authenticator, cualquiera.</summary>
    Totp = 1,

    /// <summary>Passkey: la llave del dispositivo, o una física por USB o NFC.</summary>
    WebAuthn = 2,
}

/// <summary>
/// El puente entre los dos vocabularios que ya existen para lo mismo: el
/// discriminador que guarda la tabla (<c>"Totp"</c> / <c>"WebAuthn"</c>, en
/// <c>TiposDeCredencialMfa</c>) y la máscara de la política.
///
/// <para>
/// Está aquí, en un solo sitio y con una prueba de arquitectura detrás, porque
/// emparejarlos «por nombre» es exactamente cómo se desincronizan: los nombres
/// coinciden hoy, nada obliga a que coincidan mañana, y el día que dejen de
/// hacerlo la conversión devolvería <see cref="MetodosMfa.Ninguno"/> en silencio
/// y la política rechazaría a todo el mundo.
/// </para>
/// </summary>
public static class ConversionDeMetodosMfa
{
    /// <summary>
    /// De discriminador a máscara. Un tipo desconocido da
    /// <see cref="MetodosMfa.Ninguno"/> —no lanza— porque esto corre en el camino
    /// del ingreso: una fila con un discriminador que este binario no entiende no
    /// puede tumbar el login de nadie más. Lo que sí hace es no satisfacer ninguna
    /// política, que es el lado seguro.
    /// </summary>
    public static MetodosMfa DesdeTipoDeCredencial(string? tipo) => tipo switch
    {
        TiposDeCredencialMfa.Totp => MetodosMfa.Totp,
        TiposDeCredencialMfa.WebAuthn => MetodosMfa.WebAuthn,
        _ => MetodosMfa.Ninguno,
    };

    /// <summary>De máscara a discriminador. Sólo para valores de un solo bit.</summary>
    public static string? ATipoDeCredencial(MetodosMfa metodo) => metodo switch
    {
        MetodosMfa.Totp => TiposDeCredencialMfa.Totp,
        MetodosMfa.WebAuthn => TiposDeCredencialMfa.WebAuthn,
        _ => null,
    };

    /// <summary>
    /// Todos los métodos que hoy existen. Es el valor por defecto de toda política
    /// nueva: una cooperativa que enciende el segundo factor sin decir nada más
    /// quiere «que tengan algo», no «que tengan justo esto».
    /// </summary>
    public const MetodosMfa Todos = MetodosMfa.Totp | MetodosMfa.WebAuthn;

    /// <summary>
    /// Los métodos sueltos, para recorrerlos. Se escribe una vez aquí para que
    /// añadir un tercero no obligue a buscar bucles por todo el repositorio.
    /// </summary>
    public static readonly IReadOnlyList<MetodosMfa> Sueltos =
        [MetodosMfa.Totp, MetodosMfa.WebAuthn];

    /// <summary>
    /// Desglosa la máscara en los literales que entiende el cliente. Es lo que
    /// viaja por la API: el mismo vocabulario que ya usa la lista de credenciales,
    /// en vez de un entero que obligaría a la pantalla a conocer los bits.
    /// </summary>
    public static IReadOnlyList<string> ALiterales(MetodosMfa mascara) =>
        [.. Sueltos
            .Where(m => (mascara & m) != MetodosMfa.Ninguno)
            .Select(m => ATipoDeCredencial(m)!)];

    /// <summary>Reconstruye la máscara desde los literales. Ignora lo que no reconoce.</summary>
    public static MetodosMfa DesdeLiterales(IEnumerable<string>? literales)
    {
        var mascara = MetodosMfa.Ninguno;
        if (literales is null) return mascara;

        foreach (var literal in literales)
        {
            mascara |= DesdeTipoDeCredencial(literal);
        }

        return mascara;
    }
}
