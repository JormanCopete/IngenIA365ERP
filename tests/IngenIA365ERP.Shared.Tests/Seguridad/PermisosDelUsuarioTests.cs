using System.Net;
using System.Security.Claims;
using FluentAssertions;
using IngenIA365ERP.Shared.Services;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Tests.Sesion;
using Microsoft.AspNetCore.Components.Authorization;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Seguridad;

/// <summary>
/// Feature 008, FR-011: los permisos del usuario en la cooperativa activa se piden una vez y
/// fallan cerrado en dos grados — en silencio sin sesión o cooperativa (prerender), con un
/// aviso único si la API falla con sesión.
/// </summary>
public class PermisosDelUsuarioTests
{
    private static readonly DateTime Ahora = new(2026, 9, 13, 12, 0, 0, DateTimeKind.Utc);

    private readonly RelojFijo _reloj = new(Ahora);
    private readonly AlmacenEnMemoria _almacen = new();
    private readonly ServidorFalso _servidor = new();
    private readonly EstadoDeAutenticacionFalso _estado = new();
    private readonly NotificacionesGrabadas _avisos = new();
    private readonly RenovadorDeSesion _sesion;
    private readonly CentralAuthClient _auth;
    private readonly PermisosDelUsuario _permisos;

    public PermisosDelUsuarioTests()
    {
        _sesion = new RenovadorDeSesion(_almacen, new FabricaDeUnSoloServidor(_servidor), _reloj);
        var http = new HttpClient(_servidor, disposeHandler: false) { BaseAddress = new Uri("https://erp.pruebas") };
        _auth = new CentralAuthClient(http, _sesion, _estado);
        _permisos = new PermisosDelUsuario(http, _auth, _estado, _avisos);
    }

    private async Task ConSesionAsync(params (string Tipo, string Valor)[] claims)
    {
        _estado.Claims = claims;
        // Como al entrar o al elegir cooperativa: el cliente adopta el token y dispara Authenticated.
        await _auth.AdoptSessionAsync(Jwt.ConVencimiento(Ahora.AddMinutes(15)), Ahora.AddMinutes(15), "refresh-1", Ahora.AddHours(12));
        _servidor.Peticiones.Clear();
    }

    private void ElServidorResponde(params string[] codigos) =>
        _servidor.Responder = (_, _) => ServidorFalso.Json(HttpStatusCode.OK, new { isGlobalMasterAdmin = false, permissions = codigos });

    [Fact]
    public async Task Sin_sesion_no_pregunta_ni_avisa_y_no_recuerda_el_nada()
    {
        var estado = await _permisos.ObtenerAsync();

        estado.Provisional.Should().BeTrue();
        estado.Codigos.Should().BeEmpty();
        _servidor.Peticiones.Should().BeEmpty("el prerender del Web no tiene a quién preguntar");
        _avisos.Errores.Should().BeEmpty("sin sesión no es un fallo: es silencio");

        // Un instante después hay sesión: la próxima consulta sí mira.
        await ConSesionAsync(("active_tenant_id", "coop-1"));
        ElServidorResponde("Core.People.View");
        (await _permisos.TieneAsync("Core.People.View")).Should().BeTrue();
        _servidor.A("/api/admin/permissions/mine").Should().HaveCount(1);
    }

    [Fact]
    public async Task Con_sesion_pero_sin_cooperativa_tampoco_pregunta()
    {
        await ConSesionAsync(("sub", "ana"));

        var estado = await _permisos.ObtenerAsync();

        estado.Provisional.Should().BeTrue();
        _servidor.Peticiones.Should().BeEmpty();
        _avisos.Errores.Should().BeEmpty();
    }

    [Fact]
    public async Task Con_cooperativa_pregunta_una_sola_vez_y_recuerda()
    {
        await ConSesionAsync(("active_tenant_id", "coop-1"));
        ElServidorResponde("Core.People.View", "Core.People.Create");

        // Varios gates en la misma pantalla preguntan a la vez.
        var respuestas = await Task.WhenAll(
            _permisos.TieneAsync("Core.People.Create"),
            _permisos.TieneAsync("Core.People.View"),
            _permisos.TieneAsync("Core.People.Delete"));

        respuestas.Should().Equal(true, true, false);
        _servidor.A("/api/admin/permissions/mine").Should().HaveCount(1, "una petición por cooperativa, no por gate");
        _servidor.Peticiones[0].Bearer.Should().NotBeNullOrEmpty("va con el token de la sesión");
        (await _permisos.TieneAlgunoAsync(["Core.People.Delete", "Core.People.Create"])).Should().BeTrue();
        _servidor.A("/api/admin/permissions/mine").Should().HaveCount(1, "lo cargado se recuerda");
    }

    [Fact]
    public async Task Si_la_api_falla_con_sesion_queda_cerrado_y_avisa_una_sola_vez()
    {
        await ConSesionAsync(("active_tenant_id", "coop-1"));
        _servidor.Responder = (_, _) => ServidorFalso.SobreDeError(HttpStatusCode.InternalServerError, "Generic.Unexpected");

        (await _permisos.TieneAsync("Core.People.View")).Should().BeFalse();
        (await _permisos.TieneAsync("Core.People.Create")).Should().BeFalse();

        var estado = await _permisos.ObtenerAsync();
        estado.Provisional.Should().BeFalse("la API respondió; ese «nada» sí se recuerda");
        _servidor.A("/api/admin/permissions/mine").Should().HaveCount(1, "no se insiste en cada gate");
        _avisos.Errores.Should().HaveCount(1).And.ContainMatch("*No se pudieron cargar tus permisos*");
    }

    [Fact]
    public async Task El_maestro_global_puede_todo_sin_preguntar()
    {
        await ConSesionAsync(("is_global_master_admin", "true"));

        (await _permisos.TieneAsync("Core.People.Delete")).Should().BeTrue();
        _servidor.Peticiones.Should().BeEmpty("el maestro no lleva permisos por cooperativa");
    }

    [Fact]
    public async Task Cambiar_de_cooperativa_vuelve_a_preguntar_y_avisa_a_los_gates()
    {
        await ConSesionAsync(("active_tenant_id", "coop-1"));
        ElServidorResponde("Core.People.View");
        (await _permisos.TieneAsync("Core.People.Create")).Should().BeFalse();
        var avisado = 0;
        _permisos.Cambiaron += () => avisado++;

        // Elegir otra cooperativa acuña un token nuevo: Authenticated.
        await ConSesionAsync(("active_tenant_id", "coop-2"));
        ElServidorResponde("Core.People.View", "Core.People.Create");

        avisado.Should().BeGreaterThan(0, "los gates se recalculan sin F5");
        (await _permisos.TieneAsync("Core.People.Create")).Should().BeTrue();
        _servidor.A("/api/admin/permissions/mine").Should().HaveCount(1, "una petición nueva para la cooperativa nueva");
    }

    [Fact]
    public async Task Salir_deja_todo_cerrado()
    {
        await ConSesionAsync(("active_tenant_id", "coop-1"));
        ElServidorResponde("Core.People.View");
        (await _permisos.TieneAsync("Core.People.View")).Should().BeTrue();

        _estado.Claims = [];
        await _auth.LogoutAsync();

        (await _permisos.TieneAsync("Core.People.View")).Should().BeFalse();
    }
}

/// <summary>El estado de autenticación del navegador, con los claims que la prueba decida.</summary>
internal sealed class EstadoDeAutenticacionFalso : AuthenticationStateProvider
{
    public (string Tipo, string Valor)[] Claims { get; set; } = [];

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var identidad = Claims.Length == 0
            ? new ClaimsIdentity()
            : new ClaimsIdentity(Claims.Select(c => new Claim(c.Tipo, c.Valor)), "pruebas");
        return Task.FromResult(new AuthenticationState(new ClaimsPrincipal(identidad)));
    }
}

/// <summary>Las notificaciones que la pantalla habría mostrado.</summary>
internal sealed class NotificacionesGrabadas : INotificationService
{
    public event Func<string, string, NotificationType, Task>? OnNotify { add { } remove { } }
    public List<string> Errores { get; } = [];
    public Task SuccessAsync(string message) => Task.CompletedTask;
    public Task ErrorAsync(string message) { Errores.Add(message); return Task.CompletedTask; }
    public Task WarningAsync(string message) => Task.CompletedTask;
    public Task InfoAsync(string message) => Task.CompletedTask;
}
