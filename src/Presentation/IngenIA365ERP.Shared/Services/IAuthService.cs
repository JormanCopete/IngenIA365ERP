using IngenIA365ERP.Shared.Models;

namespace IngenIA365ERP.Shared.Services
{
    /// <summary>
    /// Sesión local: token guardado, cierre de sesión y estado.
    ///
    /// <para>
    /// <b>Ya no inicia sesión.</b> El <c>LoginAsync</c> que había aquí llamaba a
    /// <c>/api/auth/dev/login</c>, un atajo que existía para saltarse el segundo
    /// factor, y dejó de ser aceptable en cuanto el MFA pasó a ser obligatorio
    /// —incluido el del administrador maestro—. Iniciar sesión es cosa de
    /// <c>CentralAuthClient</c>: correo, contraseña y segundo factor.
    /// </para>
    /// </summary>
    public interface IAuthService
    {
        Task<bool> LogoutAsync();
        Task<bool> IsAuthenticatedAsync();
        Task<string?> GetTokenAsync();

        /// <summary>Feature 003 (FR-115): descarta el token local sin llamar al
        /// backend — para tokens corruptos detectados al restaurar sesión.</summary>
        void ClearTokenSilently();
    }
}
