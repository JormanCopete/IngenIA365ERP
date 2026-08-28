using IngenIA365ERP.Application.Identity.Auth.Recuperacion;
using StackExchange.Redis;

namespace IngenIA365ERP.Caching.Services.Identity;

/// <summary>
/// Tope diario de solicitudes de recuperación del segundo factor, por persona.
///
/// <para>
/// Clave propia y no <c>ILoginAttemptCounter</c>: aquel escala castigando fallos, y
/// aquí ninguna solicitud es un fallo. Lo que se acota es «cuántas al día», porque
/// sin tope quien tenga la contraseña inunda el buzón de la víctima con avisos
/// hasta que deje de leerlos — y el aviso es la defensa entera de este diseño.
/// </para>
///
/// <para>
/// La ventana es deslizante desde la primera solicitud, no de medianoche a
/// medianoche: con la ventana de calendario, pedir cuatro a las 23:55 y cuatro a
/// las 00:05 daría ocho avisos en diez minutos sin pasarse de ningún tope.
/// </para>
/// </summary>
internal sealed class RedisTopeDeSolicitudesDeRecuperacion(IConnectionMultiplexer redis)
    : ITopeDeSolicitudesDeRecuperacion
{
    private const string Prefijo = "mfa-recovery-request:";

    /// <summary>
    /// Tres al día. Suficiente para quien se equivoca de buzón o borra el correo sin
    /// querer, y lejos del volumen que hace falta para que la víctima deje de mirar.
    /// </summary>
    private const int TopeDiario = 3;

    private static readonly TimeSpan Ventana = TimeSpan.FromDays(1);

    public async Task<bool> RegistrarYComprobarAsync(Guid centralUserId, CancellationToken ct)
    {
        var db = redis.GetDatabase();
        var clave = $"{Prefijo}{centralUserId:N}";

        var cuenta = await db.StringIncrementAsync(clave);

        // El TTL se pone sólo al crear la clave. Renovarlo en cada incremento
        // convertiría la ventana en una que nunca vence mientras alguien siga
        // pidiendo, que es exactamente el caso que hay que acotar.
        if (cuenta == 1)
        {
            await db.KeyExpireAsync(clave, Ventana);
        }

        return cuenta <= TopeDiario;
    }
}
