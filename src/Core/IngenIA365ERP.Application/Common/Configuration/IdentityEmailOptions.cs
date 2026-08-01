namespace IngenIA365ERP.Application.Common.Configuration;

/// <summary>
/// Configuración del envío de correos del flujo de identidad central
/// (US1 invitaciones + Phase 4b recuperación). Se bindea desde la sección
/// <c>"IdentityEmail"</c> de <c>appsettings*.json</c>.
/// </summary>
public sealed class IdentityEmailOptions
{
    public const string SectionName = "IdentityEmail";

    /// <summary>
    /// URL base del frontend (sin trailing slash). Se concatena con las rutas
    /// específicas para construir el enlace de cada correo:
    /// <list type="bullet">
    ///   <item>Invitaciones: <c>{BaseUrl}/auth/accept-invitation?token={token}</c></item>
    ///   <item>Reset password: <c>{BaseUrl}/auth/reset-password?token={token}</c></item>
    /// </list>
    /// Dev default: <c>https://localhost:7200</c>.
    /// </summary>
    public string BaseUrl { get; set; } = "https://localhost:7200";

    /// <summary>
    /// Días de validez de una invitación desde su emisión (FR-024).
    /// Default: 7. Configurable para escenarios de demo o stress tests.
    /// </summary>
    public int InvitationLifetimeDays { get; set; } = 7;
}
