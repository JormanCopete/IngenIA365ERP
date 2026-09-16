using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Auditoria;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Tests.Seguridad;
using IngenIA365ERP.Shared.Tests.Sesion;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Auditoria;

/// <summary>
/// T088 — US7 (FR-051): el registrador de accesos deduplica la misma ruta consecutiva, manda en
/// orden, reintenta tres veces con espera y, si agota, avisa una sola vez por sesión sin frenar
/// nada. Sin sesión no manda.
/// </summary>
public class RegistroDeAccesosTests
{
    private static readonly DateTime Ahora = new(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan[] SinEspera = [TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero];

    private readonly ServidorFalso _servidor = new();
    private readonly NotificacionesGrabadas _avisos = new();
    private readonly CentralAuthClient _auth;
    private readonly RegistroDeAccesos _registro;

    public RegistroDeAccesosTests()
    {
        var sesion = new RenovadorDeSesion(new AlmacenEnMemoria(), new FabricaDeUnSoloServidor(_servidor), new RelojFijo(Ahora));
        var http = new HttpClient(_servidor, disposeHandler: false) { BaseAddress = new Uri("https://erp.pruebas") };
        _auth = new CentralAuthClient(http, sesion, new EstadoDeAutenticacionFalso());
        _registro = new RegistroDeAccesos(http, _auth, _avisos, NullLogger<RegistroDeAccesos>.Instance, SinEspera);
        _servidor.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.Accepted);
    }

    private async Task ConSesionAsync()
    {
        await _auth.AdoptSessionAsync(Jwt.ConVencimiento(Ahora.AddMinutes(15)), Ahora.AddMinutes(15), "refresh-1", Ahora.AddHours(12));
        _servidor.Peticiones.Clear();
    }

    [Fact]
    public async Task Manda_cada_ruta_una_vez_en_orden_y_omite_la_misma_ruta_consecutiva()
    {
        await ConSesionAsync();

        _registro.Registrar("https://erp.pruebas/contabilidad/plan-de-cuentas?x=1");
        _registro.Registrar("/contabilidad/plan-de-cuentas");
        _registro.Registrar("/contabilidad/comprobantes/3f2504e0-4f89-11d3-9a0c-0305e82c3301");
        _registro.Registrar("/contabilidad/plan-de-cuentas");
        await _registro.VaciarAsync();

        var enviadas = _servidor.A(RegistroDeAccesos.Ruta);
        enviadas.Should().HaveCount(3, "la segunda repetición consecutiva no es otro ingreso, la cuarta sí");
        var cuerpos = enviadas.Select(p => System.Text.Json.JsonSerializer.Deserialize<Cuerpo>(p.Cuerpo!, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!).ToList();
        cuerpos.Should().SatisfyRespectively(
            c => { c.Route.Should().Be("/contabilidad/plan-de-cuentas"); c.Title.Should().Be("Contabilidad › Plan de cuentas"); },
            c => { c.Route.Should().Be("/contabilidad/comprobantes/3f2504e0-4f89-11d3-9a0c-0305e82c3301"); c.Title.Should().Be("Contabilidad › Comprobantes", "el identificador no es parte del título"); },
            c => c.Route.Should().Be("/contabilidad/plan-de-cuentas"));
        enviadas.Should().OnlyContain(p => p.Bearer != null, "el acceso se atribuye con el token de la sesión");
        _avisos.Errores.Should().BeEmpty();
    }

    [Fact]
    public async Task Sin_sesion_no_manda_nada_ni_avisa()
    {
        _registro.Registrar("/contabilidad/comprobantes");
        await _registro.VaciarAsync();

        _servidor.Peticiones.Should().BeEmpty();
        _avisos.Errores.Should().BeEmpty();
    }

    [Fact]
    public async Task Reintenta_tres_veces_ante_un_fallo_del_servidor_y_al_agotar_avisa_una_sola_vez()
    {
        await ConSesionAsync();
        _servidor.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var avisos = new List<string>();
        _avisos.Advertencias = avisos;

        _registro.Registrar("/nomina/empleados");
        _registro.Registrar("/nomina/liquidacion");
        await _registro.VaciarAsync();

        _servidor.A(RegistroDeAccesos.Ruta).Should().HaveCount(6, "tres intentos por cada uno de los dos accesos");
        avisos.Should().ContainSingle().Which.Should().Be(RegistroDeAccesos.Aviso);
    }

    [Fact]
    public async Task Un_rechazo_del_servidor_no_se_reintenta_y_un_corte_de_red_si()
    {
        await ConSesionAsync();
        _servidor.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.NotFound);
        _registro.Registrar("/sin-cooperativa");
        await _registro.VaciarAsync();
        _servidor.A(RegistroDeAccesos.Ruta).Should().HaveCount(1, "un 404 (sin cooperativa activa o sin permiso) no mejora insistiendo");

        _servidor.Peticiones.Clear();
        var llamadas = 0;
        _servidor.Responder = (_, _) => ++llamadas < 3 ? throw new HttpRequestException("red caída") : new HttpResponseMessage(HttpStatusCode.Accepted);
        _registro.Registrar("/contabilidad/periodos");
        await _registro.VaciarAsync();
        _servidor.A(RegistroDeAccesos.Ruta).Should().HaveCount(3, "dos cortes de red y al tercero entra");
        _avisos.Errores.Should().BeEmpty();
    }

    private sealed record Cuerpo(string Route, string Title);

    [Theory]
    [InlineData("/", "Inicio")]
    [InlineData("/contabilidad/plan-de-cuentas", "Contabilidad › Plan de cuentas")]
    [InlineData("/nomina/liquidacion/2026/3", "Nomina › Liquidacion")]
    public void El_titulo_se_deriva_de_la_ruta_sin_identificadores(string ruta, string titulo) =>
        RegistroDeAccesos.TituloDe(RegistroDeAccesos.Normalizar(ruta)).Should().Be(titulo);
}
