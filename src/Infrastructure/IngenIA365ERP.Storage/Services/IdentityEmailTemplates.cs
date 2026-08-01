using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Storage.Services;

/// <summary>
/// Carga las plantillas HTML del flujo de identidad central desde
/// <c>Templates/</c> (no <c>EmailTemplates/</c>) — sustituye placeholders
/// <c>{{Key}}</c> con regex simple.
///
/// <para>Singleton: cachea cada plantilla en memoria tras la primera lectura
/// (los archivos son estáticos en el output del build, copiados por
/// <c>IngenIA365ERP.Storage.csproj</c>).</para>
/// </summary>
public sealed class IdentityEmailTemplates : IIdentityEmailTemplates
{
    private static readonly Regex Placeholder = new(
        @"\{\{\s*(?<key>[A-Za-z_][A-Za-z0-9_]*)\s*\}\}",
        RegexOptions.Compiled);

    private readonly string _templateRoot;
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<IdentityEmailTemplates> _logger;

    public IdentityEmailTemplates(ILogger<IdentityEmailTemplates> logger)
    {
        _templateRoot = Path.Combine(AppContext.BaseDirectory, "Templates");
        _logger = logger;
    }

    public Task<string> RenderAsync(
        string templateName,
        IReadOnlyDictionary<string, string?> model,
        CancellationToken ct)
    {
        var template = _cache.GetOrAdd(templateName, LoadOrThrow);
        var rendered = Placeholder.Replace(template, match =>
        {
            var key = match.Groups["key"].Value;
            if (model.TryGetValue(key, out var value) && value is not null)
                return value;

            // Se deja {{Key}} literal para que en QA salte el placeholder
            // faltante; no romper el envío por una key olvidada.
            _logger.LogWarning(
                "IdentityEmailTemplates: placeholder '{{{Key}}}' no resuelto en plantilla '{Template}'.",
                key, templateName);
            return match.Value;
        });
        return Task.FromResult(rendered);
    }

    private string LoadOrThrow(string templateName)
    {
        var path = Path.Combine(_templateRoot, templateName + ".html");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Plantilla de identidad '{templateName}' no encontrada en '{_templateRoot}'. " +
                $"Verifica que el archivo {templateName}.html existe en " +
                $"src/Infrastructure/IngenIA365ERP.Storage/Templates/ y que el csproj " +
                $"lo copia al output (PreserveNewest).",
                path);
        }

        return File.ReadAllText(path);
    }
}
