using System.Net.Http.Json;
using System.Text.Json;
using IngenIA365ERP.Shared.Services.Http;

namespace IngenIA365ERP.Shared.Services.Inventario;

/// <summary>
/// La respuesta de la API de Inventario (feature 012, T183), molde de <c>ResultadoContable</c>: además del código y el
/// mensaje conserva el <c>data</c> del sobre de error, porque en este módulo casi todo error útil lo trae —la existencia
/// que falta (<c>Inventory.Stock.Insufficient</c>), el período cerrado, los dependientes de una anulación, la revisión
/// entera de una importación (<c>Import.Invalid</c>)—. <see cref="Repetida"/> dice si la respuesta fue la guardada de un
/// envío anterior con la misma clave (<c>Idempotent-Replayed: true</c>). (nuevo)
/// </summary>
public sealed record ResultadoDeInventario<T>(bool IsSuccess, T? Value, string? ErrorCode, string? ErrorMessage, int? StatusCode, JsonElement? Data, bool Repetida = false)
{
    public const string ImportacionInvalida = "Import.Invalid";

    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);

    /// <summary>Un número de <c>data</c> (<c>available</c>, <c>lineNumber</c>, <c>max</c>…), o nulo si no viene.</summary>
    public int? Entero(string propiedad) =>
        Data is { ValueKind: JsonValueKind.Object } data && data.TryGetProperty(propiedad, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : null;

    /// <summary>Una propiedad de <c>data</c> leída como <typeparamref name="TDato"/>, o su valor por defecto.</summary>
    public TDato? Dato<TDato>(string propiedad)
    {
        if (Data is not { ValueKind: JsonValueKind.Object } data || !data.TryGetProperty(propiedad, out var valor)) return default;
        try { return valor.Deserialize<TDato>(Opciones); }
        catch (JsonException) { return default; }
    }

    /// <summary>
    /// La revisión de una importación que no se aplicó: en <c>apply</c> con errores la API responde 422
    /// <see cref="ImportacionInvalida"/> con el mismo cuerpo de la revisión en <c>data</c> (contracts/plantillas.md §0.5).
    /// </summary>
    public ResultadoDeImportacionDto? ComoImportacion()
    {
        if (Value is ResultadoDeImportacionDto propio) return propio;
        if (ErrorCode != ImportacionInvalida || Data is not { ValueKind: JsonValueKind.Object } data) return null;
        try { return data.Deserialize<ResultadoDeImportacionDto>(Opciones); }
        catch (JsonException) { return null; }
    }

    public static async Task<ResultadoDeInventario<T>> DesdeAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        var repetida = ClaveDeOperacion.FueRepeticion(resp);
        if (resp.IsSuccessStatusCode)
        {
            if (resp.StatusCode == System.Net.HttpStatusCode.NoContent || resp.Content.Headers.ContentLength == 0)
                return new(true, default, null, null, (int)resp.StatusCode, null, repetida);
            var valor = await resp.Content.ReadFromJsonAsync<T>(Opciones, ct);
            return new(true, valor, null, null, (int)resp.StatusCode, null, repetida);
        }

        var texto = await resp.Content.ReadAsStringAsync(ct);
        string? codigo = null, mensaje = null;
        JsonElement? data = null;
        try
        {
            using var doc = JsonDocument.Parse(texto);
            var raiz = doc.RootElement;
            if (raiz.ValueKind == JsonValueKind.Object)
            {
                if (raiz.TryGetProperty("code", out var c)) codigo = c.GetString();
                if (raiz.TryGetProperty("message", out var m)) mensaje = m.GetString();
                if (raiz.TryGetProperty("data", out var d)) data = d.Clone();
            }
        }
        catch (JsonException)
        {
            // Sin sobre (proxy, 502…): el estado HTTP es todo lo que hay.
            data = null;
        }
        return new(false, default, codigo ?? $"Http.{(int)resp.StatusCode}", mensaje ?? $"La API respondió {(int)resp.StatusCode}.", (int)resp.StatusCode, data, repetida);
    }

    public static ResultadoDeInventario<T> SinToken() =>
        new(false, default, "Identity.NoAccessToken", "Falta el token. Vuelve a iniciar sesión.", 401, null);

    public static ResultadoDeInventario<T> ErrorDeRed(string mensaje) =>
        new(false, default, "Network.Error", $"No se pudo contactar al servidor: {mensaje}", null, null);

    public static ResultadoDeInventario<T> FormatoInesperado(string mensaje) =>
        new(false, default, "Generic.RespuestaInesperada", $"El servidor respondió con un formato inesperado: {mensaje}", 0, null);
}
