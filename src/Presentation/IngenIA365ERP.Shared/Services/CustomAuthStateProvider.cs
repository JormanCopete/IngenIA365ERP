using Microsoft.AspNetCore.Components.Authorization;
using IngenIA365ERP.Shared.Services;
using System.Security.Claims;

namespace IngenIA365ERP.Shared.Services
{
    public class CustomAuthStateProvider : AuthenticationStateProvider
    {
        private readonly IAuthService _authService;
        private ClaimsPrincipal _anonymous = new ClaimsPrincipal(new ClaimsIdentity());

        public CustomAuthStateProvider(IAuthService authService)
        {
            _authService = authService;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var isAuthenticated = await _authService.IsAuthenticatedAsync();

            if (isAuthenticated)
            {
                var token = await _authService.GetTokenAsync();
                var identity = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, "user")
                }, "apiauth");
                var user = new ClaimsPrincipal(identity);
                return new AuthenticationState(user);
            }

            return new AuthenticationState(_anonymous);
        }

        public void NotifyUserAuthentication(string username)
        {
            var identity = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, username)
            }, "apiauth");
            var user = new ClaimsPrincipal(identity);
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
        }

        public void NotifyUserLogout()
        {
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_anonymous)));
        }
    }
}
