using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace IngenIA365ERP.Shared.Services.Security;

/// <summary>
/// Lado C# de <c>webauthn.js</c>. Es el único sitio de la aplicación que sabe que
/// ese módulo existe.
///
/// <para>
/// El módulo se importa la primera vez que hace falta y no al construir el
/// servicio: durante el prerenderizado no hay navegador, y un import ahí falla.
/// Además, así las 173 pantallas que no usan passkeys no descargan el archivo.
/// </para>
///
/// <para>
/// <b>Esta clase no se puede probar en la suite.</b> Todo lo que hace es cruzar al
/// navegador, y este repositorio no tiene pruebas de navegador. Lo que sí está
/// probado es el contrato del JSON que viaja —ver
/// <c>ElFormatoDeLaRespuestaWebAuthn</c>—, que es donde de verdad se rompen estas
/// integraciones.
/// </para>
/// </summary>
public sealed class WebAuthnInterop(IJSRuntime js, ILogger<WebAuthnInterop> logger) : IAsyncDisposable
{
    private const string RutaModulo = "./_content/IngenIA365ERP.Shared/js/webauthn.js";

    private IJSObjectReference? _modulo;

    /// <summary>
    /// Si devuelve false, la pantalla no ofrece el botón. Un botón que sólo puede
    /// fallar es peor que ningún botón.
    /// </summary>
    public async Task<bool> EstaDisponibleAsync()
    {
        try
        {
            var modulo = await ObtenerModuloAsync();
            return await modulo.InvokeAsync<bool>("estaDisponible");
        }
        catch (Exception ex) when (EsFaltaDeNavegador(ex))
        {
            // Prerenderizado: todavía no hay navegador. No es un fallo, es el
            // ciclo de vida; se resuelve solo en el primer render interactivo.
            logger.LogDebug(ex, "Sin navegador al comprobar si hay passkeys disponibles.");
            return false;
        }
    }

    /// <summary>Alta: le pide una llave nueva a la persona.</summary>
    public Task<ResultadoWebAuthn> InscribirAsync(string opcionesJson) =>
        LlamarAsync("inscribir", opcionesJson);

    /// <summary>Ingreso: le pide que firme el reto con una llave que ya tiene.</summary>
    public Task<ResultadoWebAuthn> FirmarAsync(string opcionesJson) =>
        LlamarAsync("firmar", opcionesJson);

    private async Task<ResultadoWebAuthn> LlamarAsync(string funcion, string opcionesJson)
    {
        try
        {
            var modulo = await ObtenerModuloAsync();
            var resultado = await modulo.InvokeAsync<ResultadoWebAuthn>(funcion, opcionesJson);

            // Un dominio mal configurado no es un tropiezo de quien usa la
            // aplicación: es una avería que nadie va a reportar, porque el
            // mensaje que ve la persona dice «avisá a soporte» y ahí termina.
            // Tiene que quedar en el registro del servidor.
            if (resultado is { Ok: false, Motivo: MotivosWebAuthn.Configuracion })
            {
                logger.LogError(
                    "El navegador rechazó la passkey por configuración de dominio: {Mensaje}. " +
                    "Revisá WebAuthn:RelyingPartyId contra el origen que sirve la aplicación.",
                    resultado.Mensaje);
            }

            return resultado;
        }
        catch (Exception ex) when (EsFaltaDeNavegador(ex))
        {
            logger.LogWarning(ex, "Falló el interop de passkeys en {Funcion}.", funcion);
            return new ResultadoWebAuthn(
                Ok: false,
                Motivo: MotivosWebAuthn.Error,
                Mensaje: "No se pudo hablar con el navegador. Recargá la página e intentá de nuevo.",
                RespuestaJson: null);
        }
    }

    private async Task<IJSObjectReference> ObtenerModuloAsync() =>
        _modulo ??= await js.InvokeAsync<IJSObjectReference>("import", RutaModulo);

    /// <summary>
    /// Durante el prerenderizado toda llamada a JS lanza, y es esperado. Se
    /// distingue aquí para no tragarse ninguna otra excepción.
    /// </summary>
    private static bool EsFaltaDeNavegador(Exception ex) =>
        ex is JSException or InvalidOperationException or TaskCanceledException;

    public async ValueTask DisposeAsync()
    {
        if (_modulo is null) return;

        try
        {
            await _modulo.DisposeAsync();
        }
        catch (Exception ex) when (EsFaltaDeNavegador(ex))
        {
            // El circuito ya se cerró; no queda nada que liberar del otro lado.
            logger.LogDebug(ex, "El módulo de passkeys ya no estaba al liberarlo.");
        }
    }
}

/// <param name="Motivo">Uno de <see cref="MotivosWebAuthn"/>. Null si <paramref name="Ok"/>.</param>
/// <param name="RespuestaJson">
/// Lo que devolvió el autenticador, listo para mandarlo al servidor tal cual. Null
/// si algo falló.
/// </param>
public sealed record ResultadoWebAuthn(
    bool Ok,
    string? Motivo,
    string? Mensaje,
    string? RespuestaJson);

/// <summary>
/// Los motivos que emite <c>webauthn.js</c>. Están aquí como constantes y no como
/// enum porque cruzan JSON: un enum obligaría a mantener el orden de los valores
/// sincronizado con el otro lado, que es exactamente el tipo de acoplamiento que
/// se rompe en silencio.
/// </summary>
public static class MotivosWebAuthn
{
    /// <summary>
    /// Cerró el diálogo, se le acabó el tiempo, o no acercó la llave. La
    /// especificación devuelve el mismo error para los tres a propósito, para no
    /// revelar si la llave existía. <b>No merece un aviso de error.</b>
    /// </summary>
    public const string Cancelado = "cancelado";

    /// <summary>Intentó inscribir una llave que ya tenía. Se lo dice el navegador, no el servidor.</summary>
    public const string YaInscrita = "yaInscrita";

    /// <summary>El dominio configurado no coincide con el que sirve la página. Avería, no error de uso.</summary>
    public const string Configuracion = "configuracion";

    /// <summary>Navegador sin WebAuthn, o página servida sin HTTPS.</summary>
    public const string NoSoportado = "noSoportado";

    /// <summary>Cualquier otra cosa.</summary>
    public const string Error = "error";
}
