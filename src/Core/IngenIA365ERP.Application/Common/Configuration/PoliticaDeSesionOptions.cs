namespace IngenIA365ERP.Application.Common.Configuration;

/// <summary>
/// Cuánto vive una sesión. Sección <c>"Sesion"</c> de <c>appsettings*.json</c>
/// (en Kubernetes, <c>Sesion__InactividadMinutos</c>, <c>Sesion__DuracionMaximaHoras</c> y <c>Sesion__GraciaDeReusoSegundos</c>).
///
/// <para>
/// Dos límites y los dos se hacen cumplir en el refresh, que es la única puerta por
/// la que una sesión se prolonga:
/// </para>
/// <list type="bullet">
///   <item><b>Duración máxima</b> desde el ingreso, tope absoluto: la rotación no lo
///         corre. Pasado, <c>Identity.RefreshToken.SessionExpired</c>.</item>
///   <item><b>Inactividad</b>: tiempo máximo entre dos rotaciones. El cliente rota
///         sola la sesión de una pestaña que sigue activa aunque al access le quede
///         vida, y corta la pestaña que no lo está exactamente al cumplirse; el
///         servidor rechaza con <c>Identity.RefreshToken.InactivityExpired</c> el
///         refresh que llegue después. Es el respaldo, no el reloj: el servidor no ve
///         cada petición, ve rotaciones.</item>
/// </list>
///
/// <para>
/// El cliente los lee de <c>GET /api/auth/session-policy</c> para que el número
/// viva en un solo sitio; si no puede leerlos usa estos mismos valores por defecto.
/// </para>
/// </summary>
public sealed class PoliticaDeSesionOptions
{
    public const string SectionName = "Sesion";

    /// <summary>Minutos sin actividad tras los cuales la sesión se cierra. Mínimo 2.</summary>
    public int InactividadMinutos { get; set; } = 30;

    /// <summary>Horas desde el ingreso tras las cuales la sesión termina aunque siga activa. Mínimo 1.</summary>
    public int DuracionMaximaHoras { get; set; } = 12;

    /// <summary>
    /// Segundos durante los que un refresh recién rotado todavía se acepta una vez más sin tomarlo por reuso, siempre que su
    /// sucesor no se haya usado (<c>Sesion__GraciaDeReusoSegundos</c>; 0 la apaga). Es la defensa del servidor para el canje cuya
    /// respuesta no llegó al cliente —la red se cortó, la pestaña lo abortó—: el cliente vuelve con el refresh que tenía y, sin
    /// gracia, eso invalidaba la familia y sacaba a la persona al login. Fuera de la ventana, o con el sucesor ya usado, sigue
    /// siendo reuso.
    /// </summary>
    public int GraciaDeReusoSegundos { get; set; } = 30;

    public TimeSpan Inactividad => TimeSpan.FromMinutes(InactividadMinutos);
    public TimeSpan DuracionMaxima => TimeSpan.FromHours(DuracionMaximaHoras);
    public TimeSpan GraciaDeReuso => TimeSpan.FromSeconds(GraciaDeReusoSegundos);

    /// <summary>
    /// Lo que se comprueba al arrancar. Un valor absurdo aquí no debe descubrirse
    /// con la primera persona expulsada.
    /// </summary>
    public bool EsValida(out string motivo)
    {
        if (InactividadMinutos < 2)
        {
            motivo = "Sesion:InactividadMinutos debe ser 2 o más: con menos, el aviso de cinco minutos no cabe y nadie alcanza a reaccionar.";
            return false;
        }
        if (DuracionMaximaHoras < 1)
        {
            motivo = "Sesion:DuracionMaximaHoras debe ser 1 o más.";
            return false;
        }
        if (Inactividad > DuracionMaxima)
        {
            motivo = "Sesion:InactividadMinutos no puede superar la duración máxima de la sesión.";
            return false;
        }
        if (GraciaDeReusoSegundos < 0)
        {
            motivo = "Sesion:GraciaDeReusoSegundos debe ser 0 (sin gracia) o más.";
            return false;
        }
        motivo = "";
        return true;
    }
}
