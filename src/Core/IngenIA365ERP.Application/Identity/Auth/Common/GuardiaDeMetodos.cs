using IngenIA365ERP.Domain.Entities.Admin;

namespace IngenIA365ERP.Application.Identity.Auth.Common;

/// <summary>
/// La única regla que decide si el método con el que alguien demostró su
/// identidad le sirve para entrar a UNA cooperativa concreta.
///
/// <para>
/// Es una función pura y está en un solo sitio a propósito. Hay siete puertas que
/// acuñan un token con cooperativa resuelta —los dos auto-selects, elegir,
/// cambiar, el ascenso tras inscribir, aceptar una invitación y el refresh— y
/// siete copias de un <c>if</c> sobre bits es una garantía de que alguna diverja.
/// La que divergiera dejaría un agujero o encerraría a alguien, y en ninguno de
/// los dos casos fallaría ninguna prueba de las otras seis.
/// </para>
///
/// <para>
/// <b>Esta regla NO sustituye a la comprobación de que la persona tenga segundo
/// factor</b> (<c>TwoFactorEnabled</c>), que sigue donde estaba. Son dos preguntas
/// distintas —«¿tiene algo?» y «¿le sirve aquí?»— y fundirlas fue lo primero que
/// se intentó: el resultado admitía a quien no tenía nada, porque una máscara que
/// lo acepta todo no distingue «cualquier método» de «ningún método».
/// </para>
/// </summary>
public static class GuardiaDeMetodos
{
    /// <summary>
    /// Sólo hay DOS salidas, y no tres. No existe «rechazado y punto»: si el método
    /// no sirve, siempre hay algo que la persona puede hacer, y decírselo es la
    /// diferencia entre una política y un encierro.
    ///
    /// <para>
    /// Que la máscara no pueda quedar vacía es lo que sostiene esa afirmación: si
    /// una cooperativa pudiera exigir segundo factor sin aceptar ningún método,
    /// <see cref="FaltaInscribir"/> sería mentira —no habría nada que inscribir— y
    /// haría falta una tercera salida. Por eso se rechaza al escribir la política y
    /// no aquí.
    /// </para>
    /// </summary>
    public enum Veredicto
    {
        /// <summary>Adelante.</summary>
        Admite,

        /// <summary>
        /// Tiene que inscribir uno de los métodos aceptados. Cubre a quien lo tiene
        /// del tipo equivocado y a quien entró con un código de recuperación.
        /// </summary>
        FaltaInscribir,
    }

    /// <summary>
    /// ¿Esta máscara excluye algo? Una que acepta todo lo que existe no restringe
    /// nada, y de ahí sale el corto-circuito de <see cref="Evaluar"/>.
    /// </summary>
    public static bool Restringe(MetodosMfa metodosAceptados) =>
        metodosAceptados != ConversionDeMetodosMfa.Todos;

    /// <param name="cooperativaExigeMfa">
    /// La máscara <b>sólo muerde cuando la cooperativa exige segundo factor</b>.
    /// Una cooperativa que no exige nada no puede rechazar a nadie por cómo entró.
    /// </param>
    /// <param name="metodosAceptados">La máscara de la cooperativa de destino.</param>
    /// <param name="metodoDemostrado">
    /// Con qué entró, sellado en el token. <see cref="MetodosMfa.Ninguno"/>
    /// significa <b>«no consta»</b>, y eso cubre tres situaciones que conviene tener
    /// presentes: quien entró con un código de recuperación (que no es ninguno de
    /// los dos métodos), quien no tiene segundo factor, y quien trae un token
    /// emitido antes de que este dato existiera.
    /// </param>
    public static Veredicto Evaluar(
        bool cooperativaExigeMfa,
        MetodosMfa metodosAceptados,
        MetodosMfa metodoDemostrado)
    {
        if (!cooperativaExigeMfa) return Veredicto.Admite;

        // Si la cooperativa acepta todo lo que hay, preguntar CÓMO entró no cambia
        // ninguna respuesta. El corto-circuito no es una optimización: es lo que
        // impide que el día del despliegue todas las sesiones vivas —que no llevan
        // el dato porque se emitieron antes— caigan a la vez en «te falta
        // inscribir» sin que ninguna política haya cambiado.
        if (!Restringe(metodosAceptados)) return Veredicto.Admite;

        // Intersección, no HasFlag. `HasFlag(Ninguno)` es SIEMPRE true —cualquier
        // número contiene el cero— así que con HasFlag quien no tiene método
        // pasaría todas las puertas. Es el error clásico de las máscaras de bits, y
        // aquí abriría el sistema entero sin que nada fallara.
        return (metodosAceptados & metodoDemostrado) != MetodosMfa.Ninguno
            ? Veredicto.Admite
            : Veredicto.FaltaInscribir;
    }

    /// <summary>
    /// Qué métodos tendría que inscribir para que alguna cooperativa exigente lo
    /// admita. Es lo que se le manda a la pantalla de inscripción: sin esto ofrece
    /// los dos botones, y el que no sirve vuelve a encerrarlo — con la diferencia
    /// de que ahora cree que ya lo arregló.
    /// </summary>
    public static MetodosMfa LoQueLeServiria(
        IEnumerable<(bool ExigeMfa, MetodosMfa Aceptados)> cooperativas)
    {
        var union = MetodosMfa.Ninguno;
        var hayExigentes = false;

        foreach (var c in cooperativas)
        {
            if (!c.ExigeMfa) continue;
            hayExigentes = true;
            union |= c.Aceptados;
        }

        // Sin ninguna exigente, cualquier método vale. Devolver Ninguno aquí dejaría
        // a la pantalla sin nada que ofrecer.
        return hayExigentes && union != MetodosMfa.Ninguno
            ? union
            : ConversionDeMetodosMfa.Todos;
    }
}
