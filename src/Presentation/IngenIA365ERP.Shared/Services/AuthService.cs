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

        // El inicio de sesion vive en CentralAuthClient. Lo que habia aqui
        // —LoginAsync, TryDevLoginAsync y TwoStepLoginAsync— hablaba con
        // /api/auth/dev/login, el atajo que existia para saltarse el segundo
        // factor. Con el MFA obligatorio, ese camino es el agujero.


        public async Task<bool> LogoutAsync()
        {
            try
            {
                var token = await GetTokenAsync();
                bool serverOk = true;

                if (!string.IsNullOrEmpty(token))
                {
                    // Se manda el REFRESH token, no el de acceso.
                    //
                    // Antes iba `new LogoutRequest { Token = token }` con el token de
                    // acceso. El endpoint enlaza un cuerpo `LogoutBody(string?
                    // RefreshToken)`, asi que la propiedad `token` no casaba con nada:
                    // llegaba null, el handler no encontraba sesion que cerrar y
                    // devolvia 200. Cerrar sesion respondia bien y no cerraba nada — el
                    // refresh token seguia siendo canjeable durante doce horas, asi que
                    // quien recuperara ese token de un equipo compartido volvia a entrar
                    // despues de que la persona se hubiera ido.
                    //
                    // El token de acceso sigue yendo en la cabecera, que la pone
                    // AuthBearerHandler; lo que el servidor necesita en el cuerpo para
                    // revocar la sesion es el de refresco.
                    var refreshToken = await _secureStorage.GetAsync(RefreshTokenKey);
                    var response = await _httpClient.PostAsJsonAsync(
                        AppSettings.Endpoints.Logout,
                        new { refreshToken });
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

        public void ClearTokenSilently()
        {
            _secureStorage.Remove(TokenKey);
            _secureStorage.Remove(RefreshTokenKey);
        }
    }
}
