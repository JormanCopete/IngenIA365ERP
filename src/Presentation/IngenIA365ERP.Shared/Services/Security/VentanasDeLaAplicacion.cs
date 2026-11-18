using Microsoft.JSInterop;

namespace IngenIA365ERP.Shared.Services.Security;

/// <summary>
/// «Abrir en ventana nueva» desde C# (2026-11-17), sobre <c>wwwroot/js/ventanas.js</c>. Sólo la web carga ese archivo: en
/// MAUI y en el prerender del servidor no hay ventanas, y todo responde «no disponible» sin error.
/// </summary>
public static class VentanasDeLaAplicacion
{
    /// <summary>Si este host abre ventanas (la web en el navegador o instalada).</summary>
    public static async Task<bool> DisponibleAsync(IJSRuntime js)
    {
        try
        {
            // Sin ventanas.js la llamada falla: no disponible.
            return await js.InvokeAsync<bool>("erpVentanas.disponible");
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
            return false;
        }
    }

    /// <summary>Dice a las ventanas que se abran desde aquí si llevan la sesión (parámetro de la cooperativa).</summary>
    public static async Task ConfigurarAsync(IJSRuntime js, bool heredanSesion)
    {
        try
        {
            await js.InvokeVoidAsync("erpVentanas.configurar", heredanSesion);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
            // Sin ventanas.js (MAUI) no hay nada que configurar.
        }
    }

    /// <summary>Abre la ruta en una ventana nueva. <c>false</c> si el navegador la bloqueó o el host no abre ventanas.</summary>
    public static async Task<bool> AbrirAsync(IJSRuntime js, string ruta)
    {
        try
        {
            return await js.InvokeAsync<bool>("erpVentanas.abrir", ruta);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
        {
            return false;
        }
    }
}
