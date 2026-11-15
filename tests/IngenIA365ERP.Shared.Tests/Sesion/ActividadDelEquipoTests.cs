using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Security;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Sesion;

/// <summary>
/// El uso del computador cuenta como actividad sólo si la cooperativa lo enciende (Security.Session.CountDeviceActivity,
/// pedido del dueño del 2026-11-10), y la consulta de esa marca no cuenta: si contara, la pestaña se mantendría viva sola.
/// </summary>
public class ActividadDelEquipoTests
{
    private static readonly DateTime Ahora = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    private readonly RelojFijo _reloj = new(Ahora);
    private readonly AlmacenEnMemoria _almacen = new();
    private readonly ServidorFalso _servidor = new();

    private async Task<RenovadorDeSesion> ConSesionAsync(bool cuenta)
    {
        var r = new RenovadorDeSesion(_almacen, new FabricaDeUnSoloServidor(_servidor), _reloj);
        await r.AdoptarAsync(Jwt.ConVencimiento(Ahora.AddMinutes(15)), Ahora.AddMinutes(15), "refresh-1", Ahora.AddHours(12));
        _servidor.Responder = (req, _) => req.RequestUri!.AbsolutePath == "/api/sesion/politica-de-la-cooperativa"
            ? ServidorFalso.Json(HttpStatusCode.OK, new { countDeviceActivity = cuenta })
            : new HttpResponseMessage(HttpStatusCode.OK);
        _servidor.Peticiones.Clear();
        return r;
    }

    [Fact]
    public async Task Apagado_el_uso_del_computador_no_cuenta()
    {
        var r = await ConSesionAsync(cuenta: false);
        await r.CargarPoliticaDeLaCooperativaAsync();
        _reloj.Avanzar(TimeSpan.FromMinutes(20));

        r.RegistrarActividadDelEquipo();

        r.ContarActividadDelEquipo.Should().BeFalse();
        r.InactividadRestante.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task Encendido_el_uso_del_computador_reinicia_el_reloj_de_inactividad()
    {
        var r = await ConSesionAsync(cuenta: true);
        await r.CargarPoliticaDeLaCooperativaAsync();
        _reloj.Avanzar(TimeSpan.FromMinutes(20));

        r.RegistrarActividadDelEquipo();

        r.ContarActividadDelEquipo.Should().BeTrue();
        r.InactividadRestante.Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task Leer_la_politica_no_cuenta_como_actividad_y_se_lee_una_sola_vez()
    {
        var r = await ConSesionAsync(cuenta: true);
        _reloj.Avanzar(TimeSpan.FromMinutes(20));

        await r.CargarPoliticaDeLaCooperativaAsync();
        await r.CargarPoliticaDeLaCooperativaAsync();

        r.InactividadRestante.Should().Be(TimeSpan.FromMinutes(10));
        _servidor.Peticiones.Should().ContainSingle();
    }

    [Fact]
    public async Task Al_cerrar_sesion_vuelve_a_apagado()
    {
        var r = await ConSesionAsync(cuenta: true);
        await r.CargarPoliticaDeLaCooperativaAsync();

        r.Limpiar();

        r.ContarActividadDelEquipo.Should().BeFalse();
    }
}
