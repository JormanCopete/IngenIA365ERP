using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Security;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Sesion;

/// <summary>
/// El dueño de la sesión en el cliente. Lo que antes no existía: el access token
/// vencía a los quince minutos y nadie canjeaba el refresh.
/// </summary>
public class RenovadorDeSesionTests
{
    private static readonly DateTime Ahora = new(2026, 9, 10, 12, 0, 0, DateTimeKind.Utc);

    private readonly RelojFijo _reloj = new(Ahora);
    private readonly AlmacenEnMemoria _almacen = new();
    private readonly ServidorFalso _servidor = new();

    private RenovadorDeSesion Nuevo() => new(_almacen, new FabricaDeUnSoloServidor(_servidor), _reloj);

    private async Task<RenovadorDeSesion> ConSesionAsync(TimeSpan leQuedaAlAccess, DateTime? tope = null)
    {
        var r = Nuevo();
        await r.AdoptarAsync(
            Jwt.ConVencimiento(Ahora.Add(leQuedaAlAccess)), Ahora.Add(leQuedaAlAccess),
            "refresh-1", tope ?? Ahora.AddHours(12));
        _servidor.Peticiones.Clear();
        return r;
    }

    private void ElServidorRenueva(string access = "access-2", string refresh = "refresh-2", DateTime? tope = null)
    {
        _servidor.Responder = (req, _) => ServidorFalso.Json(HttpStatusCode.OK, new
        {
            accessToken = access,
            accessTokenExpiresAt = Ahora.AddMinutes(15),
            refreshToken = refresh,
            refreshTokenExpiresAt = tope ?? Ahora.AddHours(11),
            expiresInSeconds = 900,
        });
    }

    [Fact]
    public async Task Con_el_access_lejos_de_vencer_no_toca_la_red()
    {
        var r = await ConSesionAsync(TimeSpan.FromMinutes(10));

        var token = await r.TokenVigenteAsync();

        token.Should().Be(r.AccessToken);
        _servidor.Peticiones.Should().BeEmpty();
    }

    [Fact]
    public async Task Por_vencer_canjea_el_refresh_adopta_y_persiste()
    {
        var r = await ConSesionAsync(TimeSpan.FromSeconds(30));
        ElServidorRenueva();
        var avisos = 0;
        r.SesionRenovada += () => avisos++;

        var token = await r.TokenVigenteAsync();

        token.Should().Be("access-2");
        r.RefreshToken.Should().Be("refresh-2");
        r.VencimientoDeSesion.Should().Be(Ahora.AddHours(11), "el tope lo dice el servidor");
        _almacen.Datos[RenovadorDeSesion.ClaveAccessToken].Should().Be("access-2");
        _almacen.Datos[RenovadorDeSesion.ClaveRefreshToken].Should().Be("refresh-2");
        _almacen.Datos.Should().ContainKey(RenovadorDeSesion.ClaveVencimientoDeSesion);
        avisos.Should().Be(1);

        var canje = _servidor.A("/api/auth/refresh").Should().ContainSingle().Subject;
        canje.Metodo.Should().Be(HttpMethod.Post);
        canje.Cuerpo.Should().Contain("refresh-1");
        canje.SinSesion.Should().BeTrue("los handlers tienen que dejarla pasar sin adjuntar el access vencido");
        canje.Bearer.Should().BeNull();
    }

    [Fact]
    public async Task Diez_peticiones_a_la_vez_hacen_un_solo_canje()
    {
        // La rotación invalida el refresh usado: un segundo canje con el mismo token
        // es «reuso» y mata la familia. Por eso sólo puede haber uno en vuelo.
        var r = await ConSesionAsync(TimeSpan.FromSeconds(30));
        var puerta = new TaskCompletionSource();
        _servidor.AntesDeResponder = _ => puerta.Task;
        ElServidorRenueva();

        var enVuelo = Enumerable.Range(0, 10).Select(_ => r.TokenVigenteAsync()).ToArray();
        await Task.Delay(50);
        puerta.SetResult();
        var tokens = await Task.WhenAll(enVuelo);

        tokens.Should().OnlyContain(t => t == "access-2");
        _servidor.A("/api/auth/refresh").Should().HaveCount(1);
    }

    [Fact]
    public async Task Si_el_servidor_rechaza_el_refresh_la_sesion_termina_con_su_codigo()
    {
        var r = await ConSesionAsync(TimeSpan.FromSeconds(30));
        _servidor.Responder = (_, _) => ServidorFalso.SobreDeError(
            HttpStatusCode.Unauthorized, "Identity.RefreshToken.SessionExpired");
        string? codigo = null;
        r.SesionTerminada += c => codigo = c;

        var ok = await r.RenovarAsync();

        ok.Should().BeFalse();
        codigo.Should().Be("Identity.RefreshToken.SessionExpired");
        r.TieneSesion.Should().BeFalse();
        r.VencimientoDeSesion.Should().BeNull();
        _almacen.Datos.Should().NotContainKeys(
            RenovadorDeSesion.ClaveAccessToken,
            RenovadorDeSesion.ClaveRefreshToken,
            RenovadorDeSesion.ClaveVencimientoDeSesion);
    }

    [Fact]
    public async Task Sin_red_no_hay_veredicto_y_la_sesion_sigue()
    {
        var r = await ConSesionAsync(TimeSpan.FromSeconds(30));
        _servidor.Responder = (_, _) => throw new HttpRequestException("sin wifi");
        var terminada = false;
        r.SesionTerminada += _ => terminada = true;

        var token = await r.TokenVigenteAsync();

        token.Should().Be(r.AccessToken, "sale con el que hay; si vuelve 401 el handler reintenta");
        terminada.Should().BeFalse();
        r.RefreshToken.Should().Be("refresh-1");
    }

    [Fact]
    public async Task Un_5xx_es_transitorio_como_sin_red()
    {
        var r = await ConSesionAsync(TimeSpan.FromSeconds(30));
        _servidor.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.BadGateway);
        var terminada = false;
        r.SesionTerminada += _ => terminada = true;

        (await r.RenovarAsync()).Should().BeFalse();

        terminada.Should().BeFalse();
        r.TieneSesion.Should().BeTrue();
    }

    [Fact]
    public async Task Restaura_desde_el_storage_incluido_el_tope()
    {
        var tope = Ahora.AddHours(7);
        var previo = await ConSesionAsync(TimeSpan.FromMinutes(10), tope);
        previo.Should().NotBeNull();

        var r = Nuevo(); // otro proceso: tras un F5 la memoria arranca vacía
        (await r.RestaurarAsync()).Should().BeTrue();

        r.AccessToken.Should().Be(previo.AccessToken);
        r.RefreshToken.Should().Be("refresh-1");
        r.VencimientoDeSesion.Should().Be(tope);
        r.AccessTokenExpiresAt.Should().Be(Ahora.AddMinutes(10), "lo lee del claim exp del JWT");
    }

    [Fact]
    public async Task Adoptar_sin_tope_conserva_el_que_habia()
    {
        // Una elevación tras inscribir el segundo factor no reinicia las doce horas.
        var tope = Ahora.AddHours(3);
        var r = await ConSesionAsync(TimeSpan.FromMinutes(10), tope);

        await r.AdoptarAsync("access-b", Ahora.AddMinutes(15), "refresh-b", refreshTokenExpiresAt: null);

        r.VencimientoDeSesion.Should().Be(tope);
        r.RefreshToken.Should().Be("refresh-b");
    }

    [Fact]
    public async Task Sin_refresh_devuelve_el_access_que_hay_aunque_venza()
    {
        var r = Nuevo();
        await r.AdoptarAsync(Jwt.ConVencimiento(Ahora.AddSeconds(10)), Ahora.AddSeconds(10), null, null);

        var token = await r.TokenVigenteAsync();

        token.Should().Be(r.AccessToken);
        _servidor.Peticiones.Should().BeEmpty();
    }

    // ---------- Inactividad ----------

    [Fact]
    public async Task Una_pestana_activa_rota_sola_antes_del_limite_y_eso_no_cuenta_como_actividad()
    {
        // Entró en T. A los 28 min no ha vuelto a pedir nada, pero sigue dentro de
        // los 30: el servidor mide la inactividad entre rotaciones, así que se rota
        // para no ser expulsada por un servidor que no ve cada petición. La rotación
        // NO reinicia el reloj de actividad: eso lo hace sólo la persona.
        var r = await ConSesionAsync(TimeSpan.FromMinutes(15));
        ElServidorRenueva();
        _reloj.Avanzar(TimeSpan.FromMinutes(28));

        await r.VigilarAsync();

        _servidor.A("/api/auth/refresh").Should().HaveCount(1);
        r.AccessToken.Should().Be("access-2");
        r.InactividadRestante.Should().Be(TimeSpan.FromMinutes(2), "la última actividad sigue siendo el ingreso");
        r.TieneSesion.Should().BeTrue();

        // Un segundo latido no vuelve a rotar: acaba de hacerlo.
        await r.VigilarAsync();
        _servidor.A("/api/auth/refresh").Should().HaveCount(1);
    }

    [Fact]
    public async Task Sin_actividad_durante_el_limite_la_pestana_cierra_su_sesion()
    {
        var r = await ConSesionAsync(TimeSpan.FromMinutes(15));
        ElServidorRenueva();
        string? codigo = null;
        r.SesionTerminada += c => codigo = c;
        _reloj.Avanzar(TimeSpan.FromMinutes(30));

        await r.VigilarAsync();

        codigo.Should().Be(RenovadorDeSesion.CodigoInactividad);
        r.TieneSesion.Should().BeFalse();
        _almacen.Datos.Should().NotContainKeys(RenovadorDeSesion.ClaveAccessToken, RenovadorDeSesion.ClaveRefreshToken);
        var despedida = _servidor.A("/api/auth/logout").Should().ContainSingle("avisa al servidor para que revoque el refresh").Subject;
        despedida.SinSesion.Should().BeTrue();
        despedida.Bearer.Should().NotBeNull();
        despedida.Cuerpo.Should().Contain("refresh-1");
    }

    [Fact]
    public async Task La_actividad_pospone_el_corte()
    {
        var r = await ConSesionAsync(TimeSpan.FromMinutes(15));
        ElServidorRenueva();
        var terminada = false;
        r.SesionTerminada += _ => terminada = true;

        _reloj.Avanzar(TimeSpan.FromMinutes(29));
        await r.VigilarAsync();           // rota (28 ≥ 30 − 2), no corta
        r.RegistrarActividad();           // la persona pidió algo
        _reloj.Avanzar(TimeSpan.FromMinutes(29));
        await r.VigilarAsync();

        terminada.Should().BeFalse();
        r.TieneSesion.Should().BeTrue();
        r.InactividadRestante.Should().Be(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Seguir_trabajando_rota_y_reinicia_el_reloj_de_inactividad()
    {
        var r = await ConSesionAsync(TimeSpan.FromMinutes(15));
        ElServidorRenueva();
        _reloj.Avanzar(TimeSpan.FromMinutes(27));

        var sigue = await r.SeguirTrabajandoAsync();

        sigue.Should().BeTrue();
        r.InactividadRestante.Should().Be(TimeSpan.FromMinutes(30));
        _servidor.A("/api/auth/refresh").Should().HaveCount(1, "es actividad que el servidor tiene que ver");
    }

    [Fact]
    public async Task La_politica_se_lee_del_servidor_y_sin_el_valen_los_defectos()
    {
        var r = Nuevo();
        r.Politica.Should().Be(PoliticaDeSesion.PorDefecto);

        _servidor.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.BadGateway);
        await r.CargarPoliticaAsync();
        r.Politica.Should().Be(PoliticaDeSesion.PorDefecto, "sin respuesta valen los defectos, que son los del servidor");

        _servidor.Responder = (_, _) => ServidorFalso.Json(HttpStatusCode.OK, new { inactivityMinutes = 10, maxDurationHours = 8 });
        await r.CargarPoliticaAsync();
        r.Politica.Inactividad.Should().Be(TimeSpan.FromMinutes(10));
        r.Politica.DuracionMaxima.Should().Be(TimeSpan.FromHours(8));
        var vista = _servidor.A("/api/auth/session-policy").Last();
        vista.SinSesion.Should().BeTrue();
        vista.Bearer.Should().BeNull();

        // Con 10 minutos, el mantenimiento se adelanta a los 8 y el corte llega a los 10.
        await r.AdoptarAsync(Jwt.ConVencimiento(Ahora.AddMinutes(15)), Ahora.AddMinutes(15), "refresh-1", Ahora.AddHours(8));
        ElServidorRenueva();
        _reloj.Avanzar(TimeSpan.FromMinutes(8));
        await r.VigilarAsync();
        _servidor.A("/api/auth/refresh").Should().HaveCount(1);
        _reloj.Avanzar(TimeSpan.FromMinutes(2));
        await r.VigilarAsync();
        r.TieneSesion.Should().BeFalse();
    }

    [Fact]
    public async Task Un_200_que_no_es_json_es_transitorio_y_no_termina_la_sesion()
    {
        // Un portal cautivo o un proxy caído responden 200 con HTML.
        var r = await ConSesionAsync(TimeSpan.FromSeconds(30));
        _servidor.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>portal</html>", System.Text.Encoding.UTF8, "text/html"),
        };
        var terminada = false;
        r.SesionTerminada += _ => terminada = true;

        (await r.RenovarAsync()).Should().BeFalse();

        terminada.Should().BeFalse();
        r.TieneSesion.Should().BeTrue();
    }

    [Fact]
    public void Lee_el_exp_del_jwt_y_no_se_cae_con_basura()
    {
        RenovadorDeSesion.LeerVencimientoDelJwt(Jwt.ConVencimiento(Ahora)).Should().Be(Ahora);
        RenovadorDeSesion.LeerVencimientoDelJwt("no.es.jwt").Should().BeNull();
        RenovadorDeSesion.LeerVencimientoDelJwt("").Should().BeNull();
    }
}
