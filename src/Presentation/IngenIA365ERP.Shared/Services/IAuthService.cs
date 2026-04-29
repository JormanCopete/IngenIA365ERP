using IngenIA365ERP.Shared.Models;

namespace IngenIA365ERP.Shared.Services
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(LoginRequest request);
        Task<bool> LogoutAsync();
        Task<bool> IsAuthenticatedAsync();
        Task<string?> GetTokenAsync();
    }
}
