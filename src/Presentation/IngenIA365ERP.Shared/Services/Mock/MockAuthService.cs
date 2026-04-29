using IngenIA365ERP.Shared.Models;

namespace IngenIA365ERP.Shared.Services.Mock
{
    public class MockAuthService : IAuthService
    {
        private readonly ISecureStorage _secureStorage;
        private const string TokenKey = "auth_token";

        // Credenciales validas para el mock
        private const string ValidEmail = "admin@ingenia365.com";
        private const string ValidPassword = "Admin123!";

        public MockAuthService(ISecureStorage secureStorage)
        {
            _secureStorage = secureStorage;
        }

        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            // Simular delay de red
            await Task.Delay(500);

            if (request.Email == ValidEmail && request.Password == ValidPassword)
            {
                var mockToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.mock_ingenia365erp_token";
                await _secureStorage.SetAsync(TokenKey, mockToken);

                return new LoginResponse
                {
                    Success = true,
                    Token = mockToken,
                    Message = "Login exitoso (Mock)",
                    User = new UserInfo
                    {
                        Email = ValidEmail,
                        Name = "Administrador del Sistema (Mock)"
                    }
                };
            }

            return new LoginResponse
            {
                Success = false,
                Message = "Usuario o contrasena incorrectos"
            };
        }

        public async Task<bool> LogoutAsync()
        {
            await Task.Delay(300);
            _secureStorage.Remove(TokenKey);
            return true;
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
