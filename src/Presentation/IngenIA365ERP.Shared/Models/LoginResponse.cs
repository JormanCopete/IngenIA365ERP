namespace IngenIA365ERP.Shared.Models
{
    public class LoginResponse
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public string? Message { get; set; }
        public UserInfo? User { get; set; }
    }

    public class UserInfo
    {
        public string? Email { get; set; }
        public string? Name { get; set; }
    }
}
