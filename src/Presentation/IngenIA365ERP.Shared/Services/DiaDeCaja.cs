namespace IngenIA365ERP.Shared.Services;

/// <summary>
/// El día de hoy para la caja, medido en Colombia (UTC−5, sin horario de verano), sin depender de la zona del equipo ni del servidor que
/// pinta la pantalla. Lo usa <c>AvisoSesionDeOtroDia</c> para advertir que la sesión de caja abierta es de un día anterior (pedido del
/// dueño del 2026-11-24). (nuevo)
/// </summary>
public static class DiaDeCaja
{
    private static readonly TimeSpan DesfaseDeColombia = TimeSpan.FromHours(-5);

    /// <summary>La fecha de Colombia en el instante dado.</summary>
    public static DateOnly En(DateTimeOffset instante) => DateOnly.FromDateTime(instante.ToOffset(DesfaseDeColombia).DateTime);

    /// <summary>La fecha de hoy en Colombia.</summary>
    public static DateOnly Hoy() => En(DateTimeOffset.UtcNow);

    /// <summary>Si la sesión del día dado quedó atrás: su día es anterior a hoy.</summary>
    public static bool EsDeUnDiaAnterior(DateOnly diaDeLaSesion, DateOnly hoy) => diaDeLaSesion < hoy;
}
