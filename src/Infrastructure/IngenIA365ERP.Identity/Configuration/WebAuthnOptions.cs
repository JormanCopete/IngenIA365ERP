using Microsoft.Extensions.Options;

namespace IngenIA365ERP.Identity.Configuration;

/// <summary>
/// Configuración de WebAuthn por ambiente.
///
/// <para>
/// <b>Estos dos valores no se pueden equivocar, y uno de ellos no se puede
/// cambiar después.</b> El navegador exige que <see cref="RelyingPartyId"/> sea el
/// host del origen o un sufijo registrable suyo; si no lo es, rechaza con
/// <c>SecurityError</c> <b>sin hacer una sola petición al servidor</b>: no hay
/// 4xx, no hay registro, no hay traza. Y una vez que hay passkeys inscritas,
/// cambiarlo <b>las invalida todas a la vez</b>, porque el identificador del
/// dominio va dentro del dispositivo junto a una clave privada que nunca sale de
/// ahí. No hay migración posible.
/// </para>
///
/// <para>
/// Por eso ninguno tiene valor por defecto: es preferible que el proceso no
/// arranque a que arranque con un dominio que invalidará las llaves de todo el
/// mundo el día que alguien lo corrija.
/// </para>
/// </summary>
public sealed class WebAuthnOptions
{
    public const string SectionName = "WebAuthn";

    /// <summary>
    /// El dominio al que quedan atadas las llaves. En producción es el dominio
    /// raíz —<c>ingenia365.com</c>— y no el host concreto, para que una passkey
    /// siga valiendo si el frontend se mueve de subdominio. En desarrollo es
    /// <c>localhost</c>, porque es el host desde el que se sirve.
    /// </summary>
    public string RelyingPartyId { get; set; } = string.Empty;

    /// <summary>Nombre que el navegador muestra en el diálogo del sistema.</summary>
    public string RelyingPartyName { get; set; } = "IngenIA365ERP";

    /// <summary>
    /// Orígenes desde los que se acepta una respuesta. A diferencia del RP id,
    /// esto SÍ es una lista y se le pueden añadir hosts después sin invalidar
    /// nada.
    ///
    /// <para>
    /// Detrás de un proxy que reescriba el host hay que declarar el host
    /// <b>público</b>, no el interno: lo que se compara es el origen que reportó
    /// el navegador.
    /// </para>
    /// </summary>
    public List<string> OrigenesPermitidos { get; set; } = [];

    /// <summary>Cuánto espera el diálogo del navegador antes de rendirse.</summary>
    public int TimeoutMs { get; set; } = 120_000;
}

/// <summary>
/// Falla el arranque si la configuración de WebAuthn no puede funcionar, en vez
/// de dejar que el fallo aparezca en el navegador de una persona sin dejar rastro
/// en el servidor.
/// </summary>
public sealed class WebAuthnOptionsValidator : IValidateOptions<WebAuthnOptions>
{
    public ValidateOptionsResult Validate(string? name, WebAuthnOptions opciones)
    {
        var problemas = new List<string>();

        if (string.IsNullOrWhiteSpace(opciones.RelyingPartyId))
        {
            problemas.Add(
                $"{WebAuthnOptions.SectionName}:RelyingPartyId es obligatorio y no tiene valor por " +
                "defecto a propósito: equivocarlo invalida todas las passkeys ya inscritas, y no " +
                "hay vuelta atrás.");
        }

        if (opciones.OrigenesPermitidos.Count == 0)
        {
            problemas.Add(
                $"{WebAuthnOptions.SectionName}:OrigenesPermitidos no puede estar vacío: sin " +
                "orígenes, toda respuesta del navegador se rechaza.");
        }

        foreach (var origen in opciones.OrigenesPermitidos)
        {
            if (!Uri.TryCreate(origen, UriKind.Absolute, out var uri))
            {
                problemas.Add($"Origen inválido: '{origen}'. Se espera algo como https://app.ingenia365.com.");
                continue;
            }

            // WebAuthn exige contexto seguro: HTTPS, o localhost exactamente.
            // Cualquier equipo de la red que entre por IP (http://10.0.0.5:5200)
            // no podrá usar passkeys, y el navegador no dirá por qué.
            var esSeguro = uri.Scheme == Uri.UriSchemeHttps || uri.Host is "localhost" or "127.0.0.1";
            if (!esSeguro)
            {
                problemas.Add(
                    $"El origen '{origen}' no es un contexto seguro. WebAuthn sólo funciona sobre " +
                    "HTTPS o sobre localhost exactamente; por IP de red el navegador lo rechaza.");
            }

            // El RP id tiene que ser el host o un sufijo registrable suyo.
            if (!string.IsNullOrWhiteSpace(opciones.RelyingPartyId)
                && uri.Host != opciones.RelyingPartyId
                && !uri.Host.EndsWith("." + opciones.RelyingPartyId, StringComparison.OrdinalIgnoreCase))
            {
                problemas.Add(
                    $"El origen '{origen}' no encaja con RelyingPartyId '{opciones.RelyingPartyId}': " +
                    "el navegador exige que el RP id sea el host o un sufijo suyo. Así configurado, " +
                    "el diálogo fallaría con SecurityError sin llegar al servidor.");
            }
        }

        return problemas.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(problemas);
    }
}
