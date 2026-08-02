using IngenIA365ERP.Shared.Models;

namespace IngenIA365ERP.Shared.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request);
        Task<bool> LogoutAsync();
        Task<bool> IsAuthenticatedAsync();
        Task<string?> GetTokenAsync();

        /// <summary>Feature 003 (FR-115): descarta el token local sin llamar al
        /// backend — para tokens corruptos detectados al restaurar sesión.</summary>
        void ClearTokenSilently();
    }
}
