using System.Net.Http.Json;
using System.Text.Json;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Contabilidad;

/// <summary>
/// Respuesta de la API contable que conserva el <c>data</c> de la envolvente de error (feature
/// 009, FR-041): errores por fila de una importación o por línea de un comprobante. El
/// <see cref="InvitationApiResult{T}"/> común sólo trae código y mensaje, que para un archivo de
/// 2 000 filas no le sirve a nadie.
/// </summary>
public sealed record ResultadoContable<T>(bool IsSuccess, T? Value, string? ErrorCode, string? ErrorMessage, int? StatusCode, JsonElement? Data)
{
    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web);

    public List<ErrorDeFilaDto> ErroresDeFila() => Lista<ErrorDeFilaDto>("errors");

    public List<ErrorDeLineaDto> ErroresDeLinea() => Lista<ErrorDeLineaDto>("errors");

    private List<TItem> Lista<TItem>(string propiedad)
    {
        if (Data is not { ValueKind: JsonValueKind.Object } data || !data.TryGetProperty(propiedad, out var arreglo) || arreglo.ValueKind != JsonValueKind.Array)
            return [];
        try { return arreglo.Deserialize<List<TItem>>(Opciones) ?? []; }
        catch (JsonException) { return []; }
    }

    public static async Task<ResultadoContable<T>> DesdeAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode)
        {
            if (resp.StatusCode == System.Net.HttpStatusCode.NoContent || resp.Content.Headers.ContentLength == 0)
                return new(true, default, null, null, (int)resp.StatusCode, null);
            var valor = await resp.Content.ReadFromJsonAsync<T>(Opciones, ct);
            return new(true, valor, null, null, (int)resp.StatusCode, null);
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
            // Sin envolvente (proxy, 502…): el estado HTTP es todo lo que hay.
            data = null;
        }
        return new(false, default, codigo ?? $"Http.{(int)resp.StatusCode}", mensaje ?? $"La API respondió {(int)resp.StatusCode}.", (int)resp.StatusCode, data);
    }

    public static ResultadoContable<T> SinToken() =>
        new(false, default, "Identity.NoAccessToken", "Falta el token. Vuelve a iniciar sesión.", 401, null);

    public static ResultadoContable<T> ErrorDeRed(string mensaje) =>
        new(false, default, "Network.Error", $"No se pudo contactar al servidor: {mensaje}", null, null);
}
