using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Security;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Sesion;

/// <summary>
/// «Duplicar pestaña» copia el sessionStorage: dos pestañas con el mismo refresh. Hasta el 2026-11-14 la segunda en renovar
/// canjeaba un refresh ya gastado, el servidor veía un reuso e invalidaba la familia, y las dos salían con «Tu sesión terminó».
/// </summary>
public class PestanasGemelasTests
{
    private static readonly DateTime Ahora = new(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc);

    private readonly RelojFijo _reloj = new(Ahora);
    private readonly ServidorFalso _servidor = new();
    private readonly BuzonCompartido _navegador = new();

    private async Task<(RenovadorDeSesion Pestana, AlmacenEnMemoria Almacen)> PestanaAsync(TimeSpan leQuedaAlAccess)
    {
        var almacen = new AlmacenEnMemoria();
        var r = new RenovadorDeSesion(almacen, new FabricaDeUnSoloServidor(_servidor), _reloj, _navegador);
        await r.AdoptarAsync(Jwt.ConVencimiento(Ahora.Add(leQuedaAlAccess)), Ahora.Add(leQuedaAlAccess), "refresh-1", Ahora.AddHours(12));
        return (r, almacen);
    }

    private void ElServidorRotaCadaCanje()
    {
        _servidor.Responder = (_, n) => ServidorFalso.Json(HttpStatusCode.OK, new
        {
            accessToken = $"access-{n + 1}",
            accessTokenExpiresAt = Ahora.AddMinutes(15),
            refreshToken = $"refresh-{n + 1}",
            refreshTokenExpiresAt = Ahora.AddHours(11),
            expiresInSeconds = 900,
        });
    }

    [Fact]
    public async Task La_gemela_adopta_lo_que_canjeo_la_otra_y_no_vuelve_a_canjear_el_refresh_gastado()
    {
        var (a, _) = await PestanaAsync(TimeSpan.FromSeconds(30));
        var (b, almacenB) = await PestanaAsync(TimeSpan.FromSeconds(30));
        _servidor.Peticiones.Clear();
        ElServidorRotaCadaCanje();

        (await a.TokenVigenteAsync()).Should().Be("access-2");
        (await b.TokenVigenteAsync()).Should().Be("access-2", "la gemela toma los tokens del buzón");

        _servidor.A("/api/auth/refresh").Should().ContainSingle("un segundo canje de refresh-1 sería un reuso");
        b.RefreshToken.Should().Be("refresh-2");
        almacenB.Datos[RenovadorDeSesion.ClaveRefreshToken].Should().Be("refresh-2", "lo adoptado queda en la pestaña para un F5");
    }

    [Fact]
    public async Task La_gemela_sigue_la_cadena_si_la_otra_renovo_varias_veces()
    {
        var (a, _) = await PestanaAsync(TimeSpan.FromSeconds(30));
        var (b, _) = await PestanaAsync(TimeSpan.FromSeconds(30));
        _servidor.Peticiones.Clear();
        ElServidorRotaCadaCanje();

        await a.TokenVigenteAsync();
        await a.RenovarAsync(RenovadorDeSesion.MotivoDeRenovacion.Explicita);
        a.RefreshToken.Should().Be("refresh-3");

        await b.RenovarAsync(RenovadorDeSesion.MotivoDeRenovacion.PorVencer);

        b.RefreshToken.Should().Be("refresh-3");
        _servidor.A("/api/auth/refresh").Should().HaveCount(2, "sólo los dos de la pestaña A");
    }

    [Fact]
    public async Task Una_pestana_abierta_aparte_no_encuentra_sucesor_y_canjea_lo_suyo()
    {
        var (a, _) = await PestanaAsync(TimeSpan.FromSeconds(30));
        var almacen = new AlmacenEnMemoria();
        var otra = new RenovadorDeSesion(almacen, new FabricaDeUnSoloServidor(_servidor), _reloj, _navegador);
        await otra.AdoptarAsync(Jwt.ConVencimiento(Ahora.AddSeconds(30)), Ahora.AddSeconds(30), "refresh-propio", Ahora.AddHours(12));
        _servidor.Peticiones.Clear();
        ElServidorRotaCadaCanje();

        await a.TokenVigenteAsync();
        await otra.TokenVigenteAsync();

        _servidor.A("/api/auth/refresh").Should().HaveCount(2);
        _servidor.A("/api/auth/refresh")[1].Cuerpo.Should().Contain("refresh-propio");
    }

    [Fact]
    public async Task La_gemela_abandonada_cierra_por_inactividad_sin_revocar_la_sesion_de_la_que_se_usa()
    {
        var (a, _) = await PestanaAsync(TimeSpan.FromMinutes(15));
        var (b, _) = await PestanaAsync(TimeSpan.FromMinutes(15));
        _servidor.Peticiones.Clear();
        ElServidorRotaCadaCanje();

        _reloj.Avanzar(TimeSpan.FromMinutes(14.5));
        a.RegistrarActividad();
        await a.TokenVigenteAsync(); // A sigue trabajando y renueva
        _reloj.Avanzar(TimeSpan.FromMinutes(16));
        string? codigo = null;
        b.SesionTerminada += c => codigo = c;

        await b.VigilarAsync();

        codigo.Should().Be(RenovadorDeSesion.CodigoInactividad);
        b.TieneSesion.Should().BeFalse();
        _servidor.A("/api/auth/logout").Should().BeEmpty("el logout revocaría la familia y sacaría a la pestaña A");
        a.TieneSesion.Should().BeTrue();
    }

    [Fact]
    public async Task Sin_gemela_el_cierre_por_inactividad_sigue_avisando_al_servidor()
    {
        var (b, _) = await PestanaAsync(TimeSpan.FromMinutes(15));
        _servidor.Peticiones.Clear();
        _reloj.Avanzar(TimeSpan.FromMinutes(30));

        await b.VigilarAsync();

        _servidor.A("/api/auth/logout").Should().ContainSingle();
    }

    /// <summary>El buzón y el turno de un navegador, compartidos por las pestañas de la prueba.</summary>
    private sealed class BuzonCompartido : ICoordinacionEntrePestanas
    {
        private readonly SemaphoreSlim _turno = new(1, 1);
        private readonly Dictionary<string, SucesorDeSesion> _buzon = new(StringComparer.Ordinal);

        public async Task<IAsyncDisposable> TomarTurnoAsync(CancellationToken ct = default)
        {
            await _turno.WaitAsync(ct);
            return new Soltar(_turno);
        }

        public Task<SucesorDeSesion?> SucesorDeAsync(string refreshGastado) =>
            Task.FromResult(_buzon.TryGetValue(refreshGastado, out var s) ? s : null);

        public Task PublicarSucesorAsync(string refreshGastado, SucesorDeSesion sucesor)
        {
            _buzon[refreshGastado] = sucesor;
            return Task.CompletedTask;
        }

        private sealed class Soltar(SemaphoreSlim turno) : IAsyncDisposable
        {
            public ValueTask DisposeAsync()
            {
                turno.Release();
                return ValueTask.CompletedTask;
            }
        }
    }
}
