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

                // Estrategia: intenta primero /api/auth/dev/login (dev one-shot).
                // Si no está disponible (404 = entorno no-Development), cae al
                // flujo canónico /login → /mfa/verify.
                var tokens = await TryDevLoginAsync(request)
                          ?? await TwoStepLoginAsync(request);

                if (tokens is null)
                {
                    return new LoginResponse
                    {
                        Success = false,
                        Message = "Usuario o contrasena incorrectos"
                    };
                }

                await _secureStorage.SetAsync(TokenKey, tokens.AccessToken);
                await _secureStorage.SetAsync(RefreshTokenKey, tokens.RefreshToken);

                return new LoginResponse
                {
                    Success = true,
                    Token = tokens.AccessToken,
                    Message = "Login exitoso",
                    User = new UserInfo
                    {
                        Email = tokens.User?.Username ?? request.Email,
                        Name = tokens.User?.Username ?? request.Email
                    }
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

        /// <summary>
        /// Endpoint dev-only que combina login + mfa/verify en una llamada.
        /// Devuelve null si responde 404 (no registrado en el entorno) o si
        /// el body de error indica que MFA está inscrito y debe usarse el
        /// flujo canónico (Auth.DevLoginRequiresMfa).
        /// </summary>
        private async Task<AuthTokensDto?> TryDevLoginAsync(LoginRequest request)
        {
            var body = new
            {
                tenantSubdomainOrNit = request.TenantId,
                username = request.Email,
                email = request.Email,
                tenantId = request.TenantId,
                password = request.Password
            };
            var response = await _httpClient.PostAsJsonAsync(
                $"{AppSettings.GetApiBaseUrl()}/api/auth/dev/login", body);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<AuthTokensDto>();
        }

        /// <summary>
        /// Flujo canónico /login → /mfa/verify. Usa código TOTP dummy "000000"
        /// y depende de que el usuario tenga MFA deshabilitado (en cuyo caso
        /// el handler omite la verificación). Si MFA está activo, este método
        /// devuelve null y la UI debería enrutar al challenge MFA real
        /// (pendiente — Phase 0 US1 cubrió backend; la UI MFA quedó scaffold).
        /// </summary>
        private async Task<AuthTokensDto?> TwoStepLoginAsync(LoginRequest request)
        {
            var loginBody = new
            {
                tenantSubdomainOrNit = request.TenantId,
                username = request.Email,
                email = request.Email,
                tenantId = request.TenantId,
                password = request.Password
            };
            var loginResp = await _httpClient.PostAsJsonAsync(
                AppSettings.Endpoints.Login, loginBody);
            if (!loginResp.IsSuccessStatusCode) return null;

            var challenge = await loginResp.Content.ReadFromJsonAsync<LoginChallengeDto>();
            if (challenge is null || string.IsNullOrEmpty(challenge.MfaChallengeToken)) return null;

            var verifyBody = new
            {
                mfaChallengeToken = challenge.MfaChallengeToken,
                totpCode = "000000",
                useBackupCode = false
            };
            var verifyResp = await _httpClient.PostAsJsonAsync(
                $"{AppSettings.GetApiBaseUrl()}/api/auth/mfa/verify", verifyBody);
            if (!verifyResp.IsSuccessStatusCode) return null;

            return await verifyResp.Content.ReadFromJsonAsync<AuthTokensDto>();
        }

        // DTOs locales para el nuevo contrato (Phase 0 US1).
        private sealed record LoginChallengeDto(string MfaChallengeToken, bool MustChangePassword);
        private sealed record AuthTokensDto(
            string AccessToken,
            string RefreshToken,
            DateTime AccessTokenExpiresAt,
            DateTime RefreshTokenExpiresAt,
            AuthenticatedUserDto? User);
        private sealed record AuthenticatedUserDto(Guid PublicId, string Username);

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
