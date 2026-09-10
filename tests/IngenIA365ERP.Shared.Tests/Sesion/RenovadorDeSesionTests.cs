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

    [Fact]
    public void Lee_el_exp_del_jwt_y_no_se_cae_con_basura()
    {
        RenovadorDeSesion.LeerVencimientoDelJwt(Jwt.ConVencimiento(Ahora)).Should().Be(Ahora);
        RenovadorDeSesion.LeerVencimientoDelJwt("no.es.jwt").Should().BeNull();
        RenovadorDeSesion.LeerVencimientoDelJwt("").Should().BeNull();
    }
}
