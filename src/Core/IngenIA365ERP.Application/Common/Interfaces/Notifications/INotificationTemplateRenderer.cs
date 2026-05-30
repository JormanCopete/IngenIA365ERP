namespace IngenIA365ERP.Application.Common.Interfaces.Notifications;

/// <summary>
/// T117 — Renderiza una plantilla de correo por <c>NotificationType</c> +
/// modelo. La impl canónica (Razor) vive en Infrastructure.Storage. Si la
/// plantilla no existe, devuelve <see langword="null"/> y el dispatcher
/// usa el Body literal del payload como fallback.
/// </summary>
public interface INotificationTemplateRenderer
{
    /// <summary>
    /// Renderiza el HTML del cuerpo de correo para el tipo dado.
    /// <paramref name="model"/> contiene pares clave→valor que el template
    /// puede sustituir vía <c>{{Key}}</c>. <c>Subject</c> y <c>Body</c> ya
    /// vienen pre-poblados por el handler emisor; el template los puede usar.
    /// </summary>
    Task<string?> RenderHtmlAsync(
        string notificationType,
        IReadOnlyDictionary<string, string?> model,
        CancellationToken ct);
}
