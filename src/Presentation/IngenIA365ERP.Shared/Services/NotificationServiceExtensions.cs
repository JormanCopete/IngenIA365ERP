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
        var detail = await ExtractDetailAsync(response);
        var status = (int)response.StatusCode;
        var message = string.IsNullOrWhiteSpace(detail)
            ? $"{fallback} (HTTP {status})"
            : $"{fallback} (HTTP {status}): {detail}";
        await notif.ErrorAsync(message);
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
