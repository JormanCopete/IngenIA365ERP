namespace IngenIA365ERP.Application.Common.Interfaces.Identity;

/// <summary>
/// Guarda el reto de WebAuthn entre los dos viajes: el que lo emite y el que
/// recibe la respuesta firmada del autenticador.
///
/// <para>
/// <b>Por qué no reusa <c>IMfaPendingStore</c>.</b> Aquél guarda UNA clave por
/// persona. Con dos pestañas abiertas, la segunda pisaría el reto de la primera y
/// la verificación fallaría comparando contra unas opciones que ya no son las que
/// se le presentaron al navegador. Aquí cada reto tiene su propio identificador.
/// </para>
///
/// <para>
/// <b>De un solo uso.</b> El reto es lo único que impide que alguien reproduzca
/// una respuesta capturada: si se pudiera canjear dos veces, dejaría de servir
/// para eso.
/// </para>
/// </summary>
public interface IWebAuthnChallengeStore
{
    /// <summary>
    /// Guarda las opciones que se le entregaron al navegador y devuelve el
    /// identificador con el que se recuperan.
    /// </summary>
    /// <param name="proposito">
    /// Para qué se emitió. Un reto de alta no puede canjearse como reto de
    /// ingreso: son operaciones distintas y confundirlas dejaría inscribir una
    /// llave donde había que demostrar una.
    /// </param>
    Task<string> GuardarAsync(
        Guid centralUserId,
        PropositoDeReto proposito,
        string opcionesJson,
        TimeSpan vigencia,
        CancellationToken ct);

    /// <summary>
    /// Recupera y CONSUME el reto. Devuelve <c>null</c> si no existe, si ya se
    /// usó, si venció, o si no coincide la persona o el propósito.
    /// </summary>
    Task<string?> ConsumirAsync(
        Guid centralUserId,
        PropositoDeReto proposito,
        string idDelReto,
        CancellationToken ct);
}

/// <summary>Para qué se emitió un reto.</summary>
public enum PropositoDeReto
{
    /// <summary>Inscribir una llave nueva.</summary>
    Alta = 0,

    /// <summary>Demostrar una llave ya inscrita, al entrar.</summary>
    Ingreso = 1,
}
