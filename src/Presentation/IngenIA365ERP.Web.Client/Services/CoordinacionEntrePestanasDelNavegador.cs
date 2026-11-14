using System.Text.Json;
using IngenIA365ERP.Shared.Services.Security;
using Microsoft.JSInterop;

namespace IngenIA365ERP.Web.Client.Services;

/// <summary>
/// El turno de canje y el buzón de sucesores entre pestañas del navegador, sobre <c>wwwroot/js/pestanas.js</c> de Shared
/// (2026-11-14). Si el script no está o el navegador no tiene Web Locks, degrada a lo de antes: sin turno y sin buzón.
/// </summary>
public sealed class CoordinacionEntrePestanasDelNavegador(IJSRuntime js) : ICoordinacionEntrePestanas
{
    private const string NombreDelTurno = "ingenia365-renovar-sesion";

    /// <summary>
    /// Lo más que se espera el turno. Menos que la espera del canje del renovador, y lo corta el script, no un token de
    /// cancelación: si C# dejara de esperar, el turno concedido después quedaría retenido sin nadie que lo suelte.
    /// </summary>
    private const int EsperaMaximaMs = 25_000;

    public async Task<IAsyncDisposable> TomarTurnoAsync(CancellationToken ct = default)
    {
        string id;
        try
        {
            id = await js.InvokeAsync<string>("erpPestanas.tomar", NombreDelTurno, EsperaMaximaMs);
        }
        catch (JSException ex) when (ex.Message.Contains("AbortError", StringComparison.Ordinal))
        {
            throw new OperationCanceledException("Otra pestaña retuvo el turno de renovación.", ex);
        }
        catch (JSException)
        {
            return new Turno(js, string.Empty); // sin script: sin coordinación
        }
        return new Turno(js, id);
    }

    public async Task<SucesorDeSesion?> SucesorDeAsync(string refreshGastado)
    {
        try
        {
            var crudo = await js.InvokeAsync<string?>("erpPestanas.leer", refreshGastado);
            return string.IsNullOrWhiteSpace(crudo) ? null : JsonSerializer.Deserialize<SucesorDeSesion>(crudo);
        }
        catch (Exception ex) when (ex is JSException or JsonException)
        {
            return null;
        }
    }

    public async Task PublicarSucesorAsync(string refreshGastado, SucesorDeSesion sucesor)
    {
        try
        {
            await js.InvokeVoidAsync("erpPestanas.publicar", refreshGastado, JsonSerializer.Serialize(sucesor));
        }
        catch (JSException)
        {
            // Sin buzón, la gemela no se entera: es lo de antes, no un error de la persona.
        }
    }

    private sealed class Turno(IJSRuntime js, string id) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            if (string.IsNullOrEmpty(id)) return;
            try { await js.InvokeVoidAsync("erpPestanas.soltar", id); }
            catch (JSException) { /* la pestaña se está cerrando: el navegador suelta el turno solo */ }
        }
    }
}
