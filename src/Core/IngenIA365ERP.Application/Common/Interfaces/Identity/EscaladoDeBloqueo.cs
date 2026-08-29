namespace IngenIA365ERP.Application.Common.Interfaces.Identity;

/// <summary>
/// Cuánto se bloquea tras N fallos (research D-11).
///
/// <para>
/// <b>Por qué es una función aparte y no una condición dentro del contador.</b>
/// El fallo que motivó extraerla vivía justo aquí, enterrado entre dos llamadas
/// a Redis: el candado sólo se escribía cuando el contador era <i>exactamente</i>
/// igual a un umbral, y como el último umbral es 20, a partir del intento 21
/// ninguna coincidencia volvía a darse. El contador dejaba de bloquear <b>para
/// siempre</b>, y el TTL de 24 h se refrescaba en cada fallo, así que tampoco
/// bajaba nunca. El coste real de un ataque de fuerza bruta era: aguantar veinte
/// intentos, esperar la hora una vez, y a partir de ahí intentos ilimitados.
/// </para>
///
/// <para>
/// Con la decisión fuera del I/O, la regla se puede comprobar con una tabla en
/// lugar de con un Redis levantado — y el caso «intento 21» deja de ser
/// invisible.
/// </para>
/// </summary>
public static class EscaladoDeBloqueo
{
    /// <summary>Umbrales, SIEMPRE en orden ascendente.</summary>
    public static readonly (int TrasFallos, int SegundosDeBloqueo)[] Umbrales =
    [
        (5,  60),     // 1 min
        (10, 300),    // 5 min
        (15, 900),    // 15 min
        (20, 3600),   // 60 min — y desde aquí, 60 min por CADA intento
    ];

    /// <summary>Fallos a partir de los cuales cada intento vuelve a bloquear.</summary>
    public static int Tope => Umbrales[^1].TrasFallos;

    /// <summary>
    /// Decide si este fallo dispara bloqueo y por cuánto.
    ///
    /// <para>
    /// Se bloquea al CRUZAR un umbral —para no re-bloquear en cada fallo dentro
    /// de una ventana ya activa— y además en CADA fallo por encima del tope, que
    /// es la mitad que faltaba. Por encima del tope no hay «entre umbrales»: cada
    /// intento cuesta la ventana máxima, que es lo que el comentario del umbral
    /// 20 llevaba prometiendo sin que nadie lo escribiera.
    /// </para>
    /// </summary>
    public static LoginLockoutVerdict Decidir(int fallosAcumulados)
    {
        (int tras, int segundos)? aplicable = null;
        foreach (var (tras, segundos) in Umbrales)
        {
            if (fallosAcumulados >= tras) aplicable = (tras, segundos);
        }

        if (aplicable is null)
        {
            return new LoginLockoutVerdict(
                ShouldLock: false, LockSeconds: 0, FailureCount: fallosAcumulados);
        }

        var cruzaUmbral = fallosAcumulados == aplicable.Value.tras;
        var porEncimaDelTope = fallosAcumulados > Tope;

        return cruzaUmbral || porEncimaDelTope
            ? new LoginLockoutVerdict(true, aplicable.Value.segundos, fallosAcumulados)
            : new LoginLockoutVerdict(false, 0, fallosAcumulados);
    }
}
