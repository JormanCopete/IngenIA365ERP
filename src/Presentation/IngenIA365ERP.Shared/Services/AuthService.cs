using IngenIA365ERP.Shared.Models;
using IngenIA365ERP.Shared.Configuration;
using System.Net.Http.Json;

namespace IngenIA365ERP.Shared.Services
{
    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly ISecureStorage _secureStorage;
        private readonly ITenantService _tenantService;
        private const string TokenKey = "auth_token";
        private const string RefreshTokenKey = "refresh_token";

        public AuthService(HttpClient httpClient, ISecureStorage secureStorage, ITenantService tenantService)
        {
            _httpClient = httpClient;
            _secureStorage = secureStorage;
            _tenantService = tenantService;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                // Register tenant before sending so the DelegatingHandler attaches X-Tenant-Id
                // on this and all subsequent requests.
                await _tenantService.SetTenantAsync(request.TenantId);

                var apiUrl = AppSettings.Endpoints.Login;
                var response = await _httpClient.PostAsJsonAsync(apiUrl, request);

                if (response.IsSuccessStatusCode)
                {
                    var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();

                    if (authResponse is not null && !string.IsNullOrEmpty(authResponse.Tokens.AccessToken))
                    {
                        await _secureStorage.SetAsync(TokenKey, authResponse.Tokens.AccessToken);
                        await _secureStorage.SetAsync(RefreshTokenKey, authResponse.Tokens.RefreshToken);

                        return new LoginResponse
                        {
                            Success = true,
                            Token = authResponse.Tokens.AccessToken,
                            Message = "Login exitoso",
                            User = new UserInfo
                            {
                                Email = authResponse.Email,
                                Name = authResponse.FullName
                            }
                        };
                    }
                }

                return new LoginResponse
                {
                    Success = false,
                    Message = "Usuario o contrasena incorrectos"
                };
            }
            catch (Exception ex)
            {
                return new LoginResponse
                {
                    Success = false,
                    Message = $"Error al conectar con el servidor: {ex.Message}"
                };
            }
        }

        public async Task<bool> LogoutAsync()
        {
            try
            {
                var token = await GetTokenAsync();
                bool serverOk = true;

                if (!string.IsNullOrEmpty(token))
                {
                    // AuthBearerHandler attaches the Authorization header automatically.
                    var response = await _httpClient.PostAsJsonAsync(
                        AppSettings.Endpoints.Logout,
                        new LogoutRequest { Token = token });
                    serverOk = response.IsSuccessStatusCode;
                }

                _secureStorage.Remove(TokenKey);
                _secureStorage.Remove(RefreshTokenKey);
                return serverOk;
            }
            catch (Exception)
            {
                _secureStorage.Remove(TokenKey);
                _secureStorage.Remove(RefreshTokenKey);
                return false;
            }
        }

        public async Task<bool> IsAuthenticatedAsync()
        {
            var token = await GetTokenAsync();
            return !string.IsNullOrEmpty(token);
        }

        public async Task<string?> GetTokenAsync()
        {
            try
            {
                return await _secureStorage.GetAsync(TokenKey);
            }
            catch
            {
                return null;
            }
        }
    }
}
