using System.ComponentModel.DataAnnotations;

namespace IngenIA365ERP.Shared.Models
{
    public class LoginRequest
    {
        [Required(ErrorMessage = "El correo electronico es requerido")]
        [EmailAddress(ErrorMessage = "Formato de correo invalido")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contrasena es requerida")]
        [MinLength(6, ErrorMessage = "La contrasena debe tener al menos 6 caracteres")]
        public string Password { get; set; } = string.Empty;

        public string TenantId { get; set; } = "dev_tenant";
    }
}
