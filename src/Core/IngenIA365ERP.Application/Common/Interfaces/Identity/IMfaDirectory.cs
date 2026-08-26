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
/// <b>Es el único sitio que puede escribir <c>TwoFactorEnabled</c>.</b> Esa
/// columna pasa a ser derivada («¿tiene alguna credencial activa?»), y una columna
/// derivada con dos escritores se desincroniza. Ya pasó con
/// <c>SEC_Users.IsMfaEnabled</c>: alguien dejó de escribirla y nadie lo notó,
/// porque «No» parece un dato real.
/// </para>
/// </summary>
public interface IMfaDirectory
{
    /// <summary>
    /// Texto protegido del TOTP activo de la persona, o <c>null</c> si no tiene.
    /// El resultado es opaco: se le pasa entero a <c>Unprotect</c>.
    /// </summary>
    Task<string?> ObtenerCifradoTotpActivoAsync(Guid centralUserId, CancellationToken ct);

    /// <summary>
    /// true si la persona tiene una credencial TOTP activa. Es la fuente de la que
    /// se deriva <c>TwoFactorEnabled</c>.
    /// </summary>
    Task<bool> TieneTotpActivoAsync(Guid centralUserId, CancellationToken ct);

    /// <summary>
    /// Deja a la persona con exactamente UNA credencial TOTP activa y este
    /// secreto. Si ya tenía una, la reemplaza en sitio; si no, la crea.
    ///
    /// <para>
    /// En sitio y no «revocar más insertar» por dos razones: cabe en un solo
    /// <c>SaveChanges</c> —los dos proveedores llevan <c>EnableRetryOnFailure</c>,
    /// así que una transacción explícita exigiría la estrategia de ejecución— y
    /// nunca choca contra el índice único filtrado por orden de sentencias.
    /// </para>
    /// </summary>
    /// <param name="secretoProtegido">Salida literal de <c>Protect</c>.</param>
    /// <returns><c>PublicId</c> de la credencial resultante.</returns>
    Task<Guid> ReemplazarTotpAsync(
        Guid centralUserId,
        string secretoProtegido,
        string? label,
        DateTime utcNow,
        CancellationToken ct);

    /// <summary>
    /// Baja lógica de TODAS las credenciales activas de la persona. Devuelve
    /// cuántas revocó. Idempotente.
    /// </summary>
    Task<int> RevocarTodasAsync(Guid centralUserId, DateTime utcNow, CancellationToken ct);
}
