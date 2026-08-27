namespace IngenIA365ERP.Application.Common.Interfaces.Identity;

/// <summary>
/// Directorio de credenciales de segundo factor (<c>ADM_MfaCredentials</c>). Es
/// la única puerta hacia esa tabla.
///
/// <para>
/// <b>Nunca descifra.</b> Devuelve y recibe el TEXTO PROTEGIDO tal cual: la salida
/// literal de <c>IDataProtector.Protect</c>. Quien cifra y descifra sigue siendo
/// <c>AspNetCoreIdentityProvider</c>, con su purpose y su llavero. Repartir el
/// descifrado por más capas multiplicaría los sitios donde alguien puede
/// equivocarse de purpose — y equivocarse de purpose deja a TODA persona con
/// segundo factor fuera de su cuenta, sin un solo error visible.
/// </para>
///
/// <para>
/// <b>Una persona puede tener VARIAS credenciales TOTP activas</b>: el teléfono,
/// el escritorio, un teléfono viejo de respaldo. No hay «la» credencial, hay una
/// lista, y el ingreso las prueba todas. Por eso ningún método devuelve una sola.
/// El que lo hacía —<c>ObtenerCifradoTotpActivoAsync</c>— usaba
/// <c>FirstOrDefault</c> sin <c>OrderBy</c>: con dos filas habría elegido una
/// arbitraria según el plan del motor, distinta entre PostgreSQL y SQL Server, y
/// las demás no se habrían probado nunca.
/// </para>
///
/// <para>
/// <b>NO escribe <c>TwoFactorEnabled</c>.</b> Una versión anterior de este
/// comentario decía que sí, y era falso: esa columna vive en el bridge de ASP.NET
/// Identity, que este directorio no toca. Quien la escribe es
/// <c>AspNetCoreIdentityProvider</c>, en un solo método, derivándola de
/// <see cref="ContarActivasAsync"/>.
/// </para>
/// </summary>
public interface IMfaDirectory
{
    /// <summary>
    /// Todos los textos protegidos TOTP activos de la persona, en orden
    /// determinista. El ingreso los prueba TODOS. Cada elemento es opaco: se le
    /// pasa entero a <c>Unprotect</c>.
    /// </summary>
    Task<IReadOnlyList<CredencialTotpCifrada>> ListarCifradosTotpActivosAsync(
        Guid centralUserId, CancellationToken ct);

    /// <summary>Lo que la pantalla de gestión muestra. No incluye ningún secreto.</summary>
    Task<IReadOnlyList<CredencialMfaResumen>> ListarActivasAsync(
        Guid centralUserId, CancellationToken ct);

    /// <summary>
    /// Cuántas credenciales activas tiene, <b>de cualquier tipo</b>. Es la fuente
    /// de la que se deriva <c>TwoFactorEnabled</c>, la que responde «¿es la
    /// última?» al revocar y «¿es la primera?» al inscribir, y con la que se
    /// aplica el tope por persona.
    ///
    /// <para>
    /// Contaba sólo las TOTP, y eso era una bomba de relojería esperando al
    /// segundo tipo de credencial: quien tuviera un TOTP y un passkey y retirara
    /// el passkey habría dado cuenta 1, se habría disparado el apagado total, y
    /// <c>RevocarTodasAsync</c> —que nunca filtró por tipo— se habría llevado por
    /// delante el TOTP que esa persona no tocó, dejándola sin segundo factor sin
    /// decírselo. La asimetría era exacta: una operación ciega en un sentido y la
    /// otra en el contrario.
    /// </para>
    /// </summary>
    Task<int> ContarActivasAsync(Guid centralUserId, CancellationToken ct);

    /// <summary>
    /// ¿Tuvo ALGUNA VEZ una credencial TOTP, revocadas incluidas? Ignora el filtro
    /// de soft-delete a propósito.
    ///
    /// <para>
    /// Es lo único que distingue «nunca se trasladó» de «las dio de baja», y de esa
    /// distinción depende que el modo compatibilidad no resucite el autenticador
    /// que la persona acaba de revocar: la columna heredada
    /// <c>ADM_CentralUsers.MfaSecret</c> sólo la limpiaba la baja total.
    /// </para>
    /// </summary>
    Task<bool> HuboAlgunaVezTotpAsync(Guid centralUserId, CancellationToken ct);

    /// <summary>AÑADE una credencial TOTP. No reemplaza ninguna.</summary>
    /// <param name="secretoProtegido">Salida literal de <c>Protect</c>.</param>
    /// <returns><c>PublicId</c> de la credencial creada.</returns>
    Task<Guid> InscribirTotpAsync(
        Guid centralUserId,
        string secretoProtegido,
        string? label,
        DateTime utcNow,
        CancellationToken ct);

    /// <summary>
    /// Renombra UNA credencial de esa persona. <c>false</c> si no existe o no es
    /// suya. El filtro por persona no es decorativo: sin él, un PublicId ajeno
    /// adivinado renombraría la credencial de otro.
    /// </summary>
    Task<bool> RenombrarAsync(
        Guid centralUserId, Guid credencialPublicId, string? label, DateTime utcNow, CancellationToken ct);

    /// <summary>
    /// Baja lógica de UNA credencial de esa persona. <c>false</c> si no existe o no
    /// es suya. Idempotente.
    /// </summary>
    Task<bool> RevocarUnaAsync(
        Guid centralUserId, Guid credencialPublicId, DateTime utcNow, CancellationToken ct);

    /// <summary>
    /// Baja lógica de TODAS las credenciales activas de la persona. Devuelve
    /// cuántas revocó. Idempotente.
    /// </summary>
    Task<int> RevocarTodasAsync(Guid centralUserId, DateTime utcNow, CancellationToken ct);

    /// <summary>
    /// Sella el último uso correcto. No lanza si la credencial desapareció entre la
    /// verificación y el sello: un ingreso correcto no puede caerse por no haber
    /// podido escribir telemetría.
    /// </summary>
    Task MarcarUsoAsync(
        Guid centralUserId, Guid credencialPublicId, DateTime utcNow, CancellationToken ct);

    // ---------- WebAuthn ----------

    /// <summary>AÑADE una passkey. No reemplaza ninguna.</summary>
    /// <returns><c>PublicId</c> de la credencial creada.</returns>
    Task<Guid> InscribirWebAuthnAsync(
        NuevaCredencialWebAuthn credencial, DateTime utcNow, CancellationToken ct);

    /// <summary>
    /// Las passkeys activas de la persona, para poblar <c>allowCredentials</c> al
    /// entrar y <c>excludeCredentials</c> al inscribir.
    /// </summary>
    Task<IReadOnlyList<CredencialWebAuthnPermitida>> ListarWebAuthnActivasAsync(
        Guid centralUserId, CancellationToken ct);

    /// <summary>
    /// Busca una passkey por su credential id, que es único globalmente.
    ///
    /// <para>
    /// No recibe la persona a propósito: al entrar, el navegador devuelve el
    /// identificador de la llave y de ahí se deduce de quién es. Es lo que permite
    /// que un passkey descubrible abra la sesión sin escribir el correo.
    /// </para>
    /// </summary>
    Task<CredencialWebAuthnGuardada?> BuscarWebAuthnPorCredentialIdAsync(
        byte[] credentialId, CancellationToken ct);

    /// <summary>
    /// Sella el contador de firmas tras una aserción correcta. No hace nada si no
    /// cambió: la mayoría de las passkeys de plataforma reportan siempre cero.
    /// </summary>
    Task ActualizarContadorWebAuthnAsync(
        Guid credencialPublicId, long contador, bool respaldada, CancellationToken ct);

    /// <summary>¿Está libre este credential id? Lo exige la librería al inscribir.</summary>
    Task<bool> ElCredentialIdEstaLibreAsync(byte[] credentialId, CancellationToken ct);
}

/// <summary>Lo que hace falta para persistir una passkey recién verificada.</summary>
public sealed record NuevaCredencialWebAuthn(
    Guid CentralUserId,
    byte[] CredentialId,
    byte[] ClavePublicaCose,
    long ContadorDeFirmas,
    Guid? AaGuid,
    string? TransportsJson,
    bool EsRespaldable,
    bool EstaRespaldada,
    string? FormatoDeAtestacion,
    string? Label);

/// <summary>
/// Una passkey tal como está guardada, con lo justo para verificar una aserción.
/// </summary>
public sealed record CredencialWebAuthnGuardada(
    Guid PublicId,
    Guid CentralUserId,
    byte[] ClavePublicaCose,
    long SignCount);

/// <summary>Un secreto protegido con su identificador público. Nada más.</summary>
public sealed record CredencialTotpCifrada(Guid PublicId, string SecretProtected);

/// <summary>
/// Lo que la persona ve de una de sus credenciales. <c>Label</c> y
/// <c>ConfirmedAt</c> vienen NULL en las trasladadas: ese dato no existía, e
/// inventarlo sería peor que el hueco.
/// </summary>
public sealed record CredencialMfaResumen(
    Guid PublicId,
    string? Label,
    DateTime CreatedAt,
    DateTime? ConfirmedAt,
    DateTime? LastUsedAt);
