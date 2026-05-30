using Microsoft.AspNetCore.Components.Authorization;
using IngenIA365ERP.Shared.Services.Security;
using System.Security.Claims;

namespace IngenIA365ERP.Shared.Services
{
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private readonly IAuthService _authService;
        private readonly ClaimsPrincipal _anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        public CustomAuthStateProvider(IAuthService authService)
        {
            _authService = authService;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var token = await _authService.GetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return new AuthenticationState(_anonymous);
            }

            return new AuthenticationState(BuildPrincipalFromJwt(token));
        }

        public void NotifyUserAuthentication(string token)
        {
            // El parámetro `token` reemplaza al `username` legacy: ahora el provider
            // decodifica el JWT y emite los claims reales (sub, perm, roles…).
            // PermissionGate y compañía leen esos claims para gobernar la UI.
            var principal = string.IsNullOrEmpty(token)
                ? _anonymous
                : BuildPrincipalFromJwt(token);
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(principal)));
        }

        public void NotifyUserLogout()
        {
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_anonymous)));
        }

        private static ClaimsPrincipal BuildPrincipalFromJwt(string jwt)
        {
            var claims = JwtClaimsExtractor.Extract(jwt);
            var identity = new ClaimsIdentity(claims, authenticationType: "jwt",
                nameType: "username", roleType: "roles");
            return new ClaimsPrincipal(identity);
        }
    }
}
