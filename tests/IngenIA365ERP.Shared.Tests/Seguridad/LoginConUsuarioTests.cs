using System.ComponentModel.DataAnnotations;
using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Pages.Security;
using IngenIA365ERP.Shared.Services;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Tests.Sesion;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Seguridad;

/// <summary>
/// Feature 017 (T060, US4): el formulario de ingreso acepta un usuario sin «@» y la pista aparece con
/// «Credenciales inválidas». El repositorio no tiene pruebas de componente (bUnit); se prueban las dos piezas
/// que deciden: el modelo del formulario —que antes exigía formato de correo— y el cliente, que manda
/// <c>identifier</c> tal cual y devuelve el código del que depende mostrar la pista. El recorrido en pantalla
/// lo cubre la e2e de ingreso por usuario.
/// </summary>
public class LoginConUsuarioTests
{
    private static readonly DateTime Ahora = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);

    private static IList<ValidationResult> Validar(object modelo)
    {
        var errores = new List<ValidationResult>();
        Validator.TryValidateObject(modelo, new ValidationContext(modelo), errores, validateAllProperties: true);
        return errores;
    }

    [Theory]
    [InlineData("1234567")]
    [InlineData("1.234.567")]
    [InlineData("CE987654")]
    [InlineData("ana@gmail.com")]
    public void El_formulario_acepta_correo_o_usuario(string identificador)
    {
        var modelo = new Login.LoginInput { Email = identificador, Password = "clave-de-prueba" };

        Validar(modelo).Should().BeEmpty();
    }

    [Fact]
    public void El_formulario_exige_algo_en_correo_o_usuario()
    {
        var modelo = new Login.LoginInput { Email = "", Password = "clave-de-prueba" };

        Validar(modelo).Should().ContainSingle(e => e.MemberNames.Contains(nameof(Login.LoginInput.Email)));
    }

    [Fact]
    public async Task El_cliente_manda_el_usuario_como_identifier_y_devuelve_el_codigo_de_la_pista()
    {
        var servidor = new ServidorFalso
        {
            Responder = (_, _) => ServidorFalso.SobreDeError(HttpStatusCode.Unauthorized, "Identity.InvalidCredentials"),
        };
        var almacen = new AlmacenEnMemoria();
        var sesion = new RenovadorDeSesion(almacen, new FabricaDeUnSoloServidor(servidor), new RelojFijo(Ahora));
        var cadena = new RenovacionDeSesionHandler(sesion) { InnerHandler = new AuthBearerHandler(almacen) { InnerHandler = servidor } };
        var http = new HttpClient(cadena, disposeHandler: false) { BaseAddress = new Uri("https://erp.pruebas") };
        var auth = new CentralAuthClient(http, sesion, new EstadoDeAutenticacionFalso());

        var r = await auth.LoginAsync("1234567", "clave-equivocada");

        r.IsSuccess.Should().BeFalse();
        r.ErrorCode.Should().Be("Identity.InvalidCredentials", "con este código la pantalla muestra la pista de los prefijos");
        var peticion = servidor.A("/api/auth/login").Should().ContainSingle().Subject;
        peticion.Cuerpo.Should().Contain("\"identifier\":\"1234567\"");
    }
}
