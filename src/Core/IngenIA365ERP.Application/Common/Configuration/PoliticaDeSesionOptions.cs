namespace IngenIA365ERP.Application.Common.Configuration;

/// <summary>
/// Cuánto vive una sesión. Sección <c>"Sesion"</c> de <c>appsettings*.json</c>
/// (en Kubernetes, <c>Sesion__InactividadMinutos</c> y <c>Sesion__DuracionMaximaHoras</c>).
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

    public TimeSpan Inactividad => TimeSpan.FromMinutes(InactividadMinutos);
    public TimeSpan DuracionMaxima => TimeSpan.FromHours(DuracionMaximaHoras);

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
        motivo = "";
        return true;
    }
}
