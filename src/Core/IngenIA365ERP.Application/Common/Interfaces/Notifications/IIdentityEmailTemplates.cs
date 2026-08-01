namespace IngenIA365ERP.Application.Common.Interfaces.Notifications;

/// <summary>
/// Carga y renderiza las plantillas HTML del flujo de identidad central
/// (T042 — Feature 002), ubicadas en
/// <c>src/Infrastructure/IngenIA365ERP.Storage/Templates/</c>. Usan
/// interpolación simple <c>{{Placeholder}}</c> (no Razor), por lo que el
/// renderer es trivial y se mantiene desacoplado del renderer de
/// notificaciones US6 (<see cref="INotificationTemplateRenderer"/>).
///
/// <para>Plantillas disponibles:</para>
/// <list type="bullet">
///   <item><c>InvitationEmail</c> — onboarding por invitación (US1).</item>
///   <item><c>PasswordResetEmail</c> — flujo "olvidé mi contraseña" (Phase 4b).</item>
///   <item><c>PasswordChangedNotification</c> — notificación post-cambio (Phase 4b).</item>
/// </list>
/// </summary>
public interface IIdentityEmailTemplates
{
    /// <summary>
    /// Carga la plantilla y sustituye cada <c>{{Key}}</c> por su valor del
    /// modelo (case-insensitive). Las keys no presentes en el modelo se
    /// dejan en su forma <c>{{Faltante}}</c> para que el operador note
    /// el bug en QA. Cache estática del archivo cargado.
    /// </summary>
    Task<string> RenderAsync(
        string templateName,
        IReadOnlyDictionary<string, string?> model,
        CancellationToken ct);
}
