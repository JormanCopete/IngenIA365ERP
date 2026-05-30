using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Storage.Services;

/// <summary>
/// T117 — Renderer de plantillas de correo. Implementación pragmática con
/// reemplazo de placeholders <c>{{Key}}</c> en lugar de Razor compilado:
/// las 6 plantillas de Fase 0 son simples (asunto + 2-3 párrafos) y no
/// justifican el costo de RazorEngineCore (assembly compilation in-process).
///
/// <para>
/// Si las plantillas crecen en complejidad (loops, partials, layouts), se
/// puede swappear por un renderer Razor real sin tocar el contrato
/// <see cref="INotificationTemplateRenderer"/>.
/// </para>
///
/// <para>
/// Templates en <c>EmailTemplates/</c> embebidos como assets físicos
/// (no recursos embebidos) para que un operador pueda editar el HTML
/// sin recompilar la solución.
/// </para>
/// </summary>
public sealed class NotificationTemplateRenderer : INotificationTemplateRenderer
{
    private static readonly Regex Placeholder = new(
        @"\{\{\s*(?<key>[A-Za-z_][A-Za-z0-9_]*)\s*\}\}",
        RegexOptions.Compiled);

    private readonly string _templateRoot;
    private readonly ConcurrentDictionary<string, string?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<NotificationTemplateRenderer> _logger;

    public NotificationTemplateRenderer(ILogger<NotificationTemplateRenderer> logger)
    {
        _templateRoot = Path.Combine(AppContext.BaseDirectory, "EmailTemplates");
        _logger = logger;
    }

    public Task<string?> RenderHtmlAsync(
        string notificationType,
        IReadOnlyDictionary<string, string?> model,
        CancellationToken ct)
    {
        var template = _cache.GetOrAdd(notificationType, LoadTemplate);
        if (template is null)
        {
            return Task.FromResult<string?>(null);
        }

        var html = Placeholder.Replace(template, match =>
        {
            var key = match.Groups["key"].Value;
            return model.TryGetValue(key, out var value)
                ? WebUtility.HtmlEncode(value ?? string.Empty)
                : match.Value; // deja el placeholder intacto si falta el dato
        });
        return Task.FromResult<string?>(html);
    }

    private string? LoadTemplate(string notificationType)
    {
        var path = Path.Combine(_templateRoot, $"{notificationType}.cshtml");
        if (!File.Exists(path))
        {
            _logger.LogDebug(
                "Template {Type}.cshtml no encontrada en {Root}; se usará Body literal.",
                notificationType, _templateRoot);
            return null;
        }
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "No se pudo leer template {Type}.cshtml; se usará Body literal.",
                notificationType);
            return null;
        }
    }
}
