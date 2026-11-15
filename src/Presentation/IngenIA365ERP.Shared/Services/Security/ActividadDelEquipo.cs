using Microsoft.JSInterop;

namespace IngenIA365ERP.Shared.Services.Security;

/// <summary>
/// Si la persona está usando el computador, en cualquier programa. No dice cuál: sólo «hay teclado o ratón en uso en el
/// último minuto» y «la pantalla está bloqueada». Lo consulta el aviso de sesión una vez por segundo cuando la cooperativa
/// tiene encendido <c>Security.Session.CountDeviceActivity</c>.
/// </summary>
public interface IActividadDelEquipo
{
    Task<EstadoDelEquipo> LeerAsync();

    /// <summary>Pide el permiso al navegador. Tiene que salir de un clic de la persona. <c>true</c> si quedó concedido.</summary>
    Task<bool> PedirPermisoAsync();

    Task DetenerAsync();
}

/// <param name="Soportado">El navegador o la aplicación saben responder.</param>
/// <param name="Permitido">La persona dio el permiso (o no hace falta).</param>
/// <param name="PuedePedirPermiso">Todavía no se ha preguntado: tiene sentido ofrecer el botón.</param>
public sealed record EstadoDelEquipo(bool Soportado, bool Permitido, bool PuedePedirPermiso, bool EnUso, bool PantallaBloqueada)
{
    public static readonly EstadoDelEquipo NoSoportado = new(false, false, false, false, false);

    /// <summary>Cuenta como actividad: en uso y con la pantalla desbloqueada.</summary>
    public bool CuentaComoActividad => Soportado && Permitido && EnUso && !PantallaBloqueada;
}

/// <summary>En el navegador (web y WebAssembly): <c>wwwroot/js/actividad-del-equipo.js</c>, sobre la Idle Detection API.</summary>
public sealed class ActividadDelEquipoDelNavegador(IJSRuntime js) : IActividadDelEquipo
{
    public async Task<EstadoDelEquipo> LeerAsync()
    {
        try
        {
            var e = await js.InvokeAsync<EstadoJs>("actividadDelEquipo.leer");
            if (e is null || !e.Soportado) return EstadoDelEquipo.NoSoportado;
            return new EstadoDelEquipo(true, e.Permiso == "granted", e.Permiso is "prompt" or "desconocido",
                e.EnUso, e.PantallaBloqueada);
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException)
        {
            // Sin el script (prerenderizado, circuito caído) es como un navegador que no lo soporta.
            return EstadoDelEquipo.NoSoportado;
        }
    }

    public async Task<bool> PedirPermisoAsync()
    {
        try
        {
            return await js.InvokeAsync<string>("actividadDelEquipo.pedirPermiso") == "granted";
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException)
        {
            return false;
        }
    }

    public async Task DetenerAsync()
    {
        try
        {
            await js.InvokeVoidAsync("actividadDelEquipo.detener");
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException)
        {
            // Nada que detener.
        }
    }

    private sealed record EstadoJs(bool Soportado, string? Permiso, bool EnUso, bool PantallaBloqueada);
}
