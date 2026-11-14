using System.Text.Json;
using IngenIA365ERP.Shared.Services.Security;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace IngenIA365ERP.Web.Client.Services;

/// <summary>
/// El turno de canje y el buzón de sucesores entre pestañas del navegador, sobre <c>wwwroot/js/pestanas.js</c> de Shared
/// (2026-11-14). Si el script no está o el navegador no tiene Web Locks, degrada a lo de antes: sin turno y sin buzón.
/// </summary>
public sealed class CoordinacionEntrePestanasDelNavegador(IJSRuntime js, ILogger<CoordinacionEntrePestanasDelNavegador> logger)
    : ICoordinacionEntrePestanas
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
            return new Turno(js, string.Empty, logger); // sin script: sin coordinación
        }
        return new Turno(js, id, logger);
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
        catch (JSException ex)
        {
            // Sin buzón, la gemela no se entera: es lo de antes, no un error de la persona. Queda en la consola
            // (Principio IX: un catch vacío no se admite).
            logger.LogDebug(ex, "No se pudo publicar el sucesor de la sesión para las otras pestañas.");
        }
    }

    private sealed class Turno(IJSRuntime js, string id, ILogger logger) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            if (string.IsNullOrEmpty(id)) return;
            try { await js.InvokeVoidAsync("erpPestanas.soltar", id); }
            catch (JSException ex)
            {
                // La pestaña se está cerrando: el navegador suelta el turno solo.
                logger.LogDebug(ex, "No se pudo soltar el turno de renovación {Turno}.", id);
            }
        }
    }
}
