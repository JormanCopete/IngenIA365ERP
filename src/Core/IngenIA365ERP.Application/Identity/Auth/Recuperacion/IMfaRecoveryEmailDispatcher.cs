namespace IngenIA365ERP.Application.Identity.Auth.Recuperacion;

/// <param name="TokenDeConfirmacion">Plano. Nunca se guarda; sólo viaja al buzón.</param>
/// <param name="TokenDeCancelacion">
/// Plano, y DISTINTO del anterior. Van los dos en el mismo correo porque el aviso
/// y la capacidad de pararlo tienen que llegar juntos: un aviso que dice «alguien
/// pidió retirar tu segundo factor» sin un botón para impedirlo es una notificación
/// de daño, no una defensa.
/// </param>
public sealed record AvisoDeRecuperacionMfa(
    string Correo,
    string TokenDeConfirmacion,
    string TokenDeCancelacion,
    DateTime EjecutableDesde,
    DateTime ExpiraEn,
    string? IpSolicitante,
    string? UserAgent);

/// <summary>
/// Manda el aviso de recuperación del segundo factor.
///
/// <para>
/// Va por <c>IEmailSender</c> directo y NO por <c>SendNotificationCommand</c>. No
/// es una preferencia de estilo: aquel escribe la notificación en la base de una
/// cooperativa, y esto ocurre <b>antes de elegir cooperativa</b> — en ese estado el
/// contexto de datos lanza por el Principio IV, y con razón. Es el mismo camino
/// que ya usa el correo de «olvidé mi contraseña».
/// </para>
/// </summary>
public interface IMfaRecoveryEmailDispatcher
{
    Task DespacharAsync(AvisoDeRecuperacionMfa aviso, CancellationToken ct);
}

/// <summary>
/// Cuántas recuperaciones puede pedir una persona al día.
///
/// <para>
/// Clave propia en Redis y no <c>ILoginAttemptCounter</c>. Aquel escala castigando
/// FALLOS —5 fallos un minuto, 10 cinco minutos— y aquí no hay ningún fallo: cada
/// solicitud es legítima por sí sola. Lo que hay que acotar es «cuántas al día»,
/// que el escalado responde mal, y sin acotarlo quien tenga la contraseña inunda el
/// buzón de la víctima con avisos hasta que deje de leerlos — que es precisamente
/// como se derrota una defensa basada en avisar.
/// </para>
/// </summary>
public interface ITopeDeSolicitudesDeRecuperacion
{
    /// <summary>
    /// Cuenta una solicitud y dice si queda dentro del tope. Cuenta ANTES de
    /// responder, a propósito: si contara sólo las que llegan a mandar correo, un
    /// fallo del SMTP dejaría el tope sin efecto justo cuando alguien está
    /// reintentando en bucle.
    /// </summary>
    Task<bool> RegistrarYComprobarAsync(Guid centralUserId, CancellationToken ct);
}
