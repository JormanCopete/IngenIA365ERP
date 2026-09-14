using System.Text.Json;

namespace IngenIA365ERP.Shared.Services;

public static class NotificationServiceExtensions
{
    /// <summary>
    /// Surfaces the HTTP status + parsed error body so the user sees the real reason
    /// (validation message, "Tenant not specified", 401, etc.) instead of a generic toast.
    /// </summary>
    public static async Task ShowErrorAsync(
        this INotificationService notif,
        HttpResponseMessage response,
        string fallback)
    {
        var status = (int)response.StatusCode;
        if (await EsPermisoNegadoAsync(response))
        {
            await notif.ErrorAsync($"{fallback}: {SinPermiso}");
            return;
        }

        var detail = await ExtractDetailAsync(response);
        var message = string.IsNullOrWhiteSpace(detail)
            ? $"{fallback} (HTTP {status})"
            : $"{fallback} (HTTP {status}): {detail}";
        await notif.ErrorAsync(message);
    }

    /// <summary>Lo que se dice cuando una escritura recibe el 404 indistinguible de la puerta de permisos.</summary>
    public const string SinPermiso = "No tenés permiso para esta acción o el registro ya no existe.";

    /// <summary>
    /// Feature 008: la API responde a la falta de permiso con un 404 <c>Generic.NotFound</c>
    /// indistinguible de una ruta inexistente (FR-017 de la feature 003). En una lectura eso es
    /// «no está»; en un POST/PUT/DELETE casi siempre es «no podés», y decirle a la persona
    /// «recurso no encontrado» cuando acaba de pulsar Guardar la deja sin pista.
    /// </summary>
    private static async Task<bool> EsPermisoNegadoAsync(HttpResponseMessage response)
    {
        if (response.StatusCode != System.Net.HttpStatusCode.NotFound) return false;
        var metodo = response.RequestMessage?.Method;
        if (metodo is null || metodo == HttpMethod.Get || metodo == HttpMethod.Head) return false;
        try
        {
            var raw = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(raw)) return false;
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("code", out var code)
                && code.ValueKind == JsonValueKind.String
                && string.Equals(code.GetString(), "Generic.NotFound", StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false; // cuerpo no JSON: no es el envelope de la puerta
        }
    }

    private static async Task<string?> ExtractDetailAsync(HttpResponseMessage response)
    {
        try
        {
            var raw = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(raw)) return null;

            // Common shapes:
            //   { "error": "..." }                                  — middleware/business
            //   { "title": "...", "errors": { "Field": ["..."] } }  — ValidationProblemDetails
            //   "plain string"                                      — Result.Error
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            if (root.ValueKind == JsonValueKind.String)
                return root.GetString();

            if (root.ValueKind == JsonValueKind.Object)
            {
                // El sobre estándar de la API ({ code, message, traceId }), que es
                // lo que devuelve ErrorEnvelopeFilter y también el 503 sintético de
                // RenovacionDeSesionHandler cuando no hay red. Sin esta rama caía
                // al JSON crudo truncado a 200 caracteres.
                if (root.TryGetProperty("message", out var mensaje) && mensaje.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(mensaje.GetString()))
                    return mensaje.GetString();
                if (root.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.String)
                    return err.GetString();

                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    var first = errors.EnumerateObject().FirstOrDefault();
                    if (first.Value.ValueKind == JsonValueKind.Array)
                    {
                        var msg = first.Value.EnumerateArray().FirstOrDefault().GetString();
                        if (!string.IsNullOrWhiteSpace(msg))
                            return $"{first.Name}: {msg}";
                    }
                }

                if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                    return title.GetString();
            }

            return raw.Length > 200 ? raw[..200] + "…" : raw;
        }
        catch
        {
            // Body wasn't JSON — return the trimmed raw text.
            try { return await response.Content.ReadAsStringAsync(); }
            catch { return null; }
        }
    }
}
