using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Cartera;
using IngenIA365ERP.Shared.Services.Http;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Cartera;

/// <summary>
/// Feature 014, US6 (T179; contracts/api.md §5 y §6): el CDAT y la tasa negociada en <see cref="CaptacionesClient"/>. Constituir, fondear, cambiar la
/// instrucción, renovar, redimir, decidir y retirar la solicitud y registrar o terminar una regla llevan su <c>Idempotency-Key</c> y van a sus rutas; las
/// simulaciones y las lecturas van sin clave; ninguna pone <c>Authorization</c>; las tasas viajan como fracción. (nuevo)
/// </summary>
public class CdatClientTests
{
    private static readonly Guid Cdat = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid Otra = Guid.Parse("99999999-8888-7777-6666-555555555555");

    private readonly ServidorDeCartera _servidor = new();
    private readonly CaptacionesClient _cliente;

    public CdatClientTests()
    {
        var (http, auth) = _servidor.Sesion();
        _cliente = new CaptacionesClient(http, auth);
    }

    public static IEnumerable<object[]> Escrituras()
    {
        yield return E("constituir", "POST", "/api/lending/term-deposits", (c, k) => c.ConstituirCdatAsync(new ConstituirCdatRequest { Amount = 10_000_000m }, k));
        yield return E("fondear", "POST", $"/api/lending/term-deposits/{Cdat}/fund", (c, k) => c.FondearCdatAsync(Cdat, new FondearCdatRequest(Otra, [], null, null), k));
        yield return E("instrucción", "PUT", $"/api/lending/term-deposits/{Cdat}/maturity-instruction",
            (c, k) => c.CambiarInstruccionDeCdatAsync(Cdat, new InstruccionDeCdatRequest(TextosDeCdat.CancelarYPagar, null, "Lo pide el titular"), k));
        yield return E("renovar", "POST", $"/api/lending/term-deposits/{Cdat}/renew",
            (c, k) => c.RenovarCdatAsync(Cdat, new RenovarCdatRequest(360, 30, TextosDeCdat.Vencidos, "Renovación"), k));
        yield return E("redimir", "POST", $"/api/lending/term-deposits/{Cdat}/redeem",
            (c, k) => c.RedimirCdatAsync(Cdat, new RedimirCdatRequest(new DestinoDeRedencionRequest(TextosDeCdat.DestinoCuenta, Otra), "huella", "Lo necesita"), k));
        yield return E("aprobar", "POST", $"/api/lending/negotiated-rate-requests/{Cdat}/approve",
            (c, k) => c.AprobarTasaAsync(Cdat, new DecidirTasaNegociadaRequest("Asociado de cinco años"), k));
        yield return E("negar", "POST", $"/api/lending/negotiated-rate-requests/{Cdat}/reject", (c, k) => c.NegarTasaAsync(Cdat, new DecidirTasaNegociadaRequest("No"), k));
        yield return E("retirar", "POST", $"/api/lending/negotiated-rate-requests/{Cdat}/withdraw", (c, k) => c.RetirarTasaAsync(Cdat, "Desiste", k));
        yield return E("regla", "POST", $"/api/lending/lines/{Cdat}/negotiated-rate-rules",
            (c, k) => c.RegistrarReglaDeTasaAsync(Cdat, new ReglaDeTasaNegociadaRequest { Code = "ANT5", MaxMarginEa = 0.015m, Reason = "Consejo" }, k));
        yield return E("terminar regla", "POST", $"/api/lending/lines/{Cdat}/negotiated-rate-rules/{Otra}/end",
            (c, k) => c.TerminarReglaDeTasaAsync(Cdat, Otra, new TerminarReglaDeTasaRequest(new DateOnly(2027, 1, 1), "Fin"), k));
    }

    private static object[] E(string nombre, string metodo, string ruta, Func<CaptacionesClient, ClaveDeOperacion, Task> accion) => [nombre, metodo, ruta, accion];

    [Theory]
    [MemberData(nameof(Escrituras))]
    public async Task Cada_escritura_lleva_su_clave_y_no_pone_Authorization(string nombre, string metodo, string ruta,
        Func<CaptacionesClient, ClaveDeOperacion, Task> accion)
    {
        await accion(_cliente, new ClaveDeOperacion());

        var vista = _servidor.Vistas.Should().ContainSingle(nombre).Subject;
        vista.Metodo.Should().Be(metodo);
        vista.Ruta.Should().Be(ruta);
        vista.Clave.Should().NotBeNullOrWhiteSpace($"{nombre} es una escritura");
        vista.Authorization.Should().BeNull("la cabecera la pone sólo el handler de la sesión");
    }

    [Fact]
    public async Task Las_simulaciones_y_las_lecturas_van_sin_clave()
    {
        _servidor.Responder = _ => ServidorDeCartera.Json(HttpStatusCode.OK, new { });
        await _cliente.SimularCdatAsync(new SimularCdatRequest(Cdat, 10_000_000m, 180, 30, TextosDeCdat.Vencidos, null, 0.012m, null, null, null));
        await _cliente.SimularRedencionAsync(Cdat, new DateOnly(2027, 4, 13));
        await _cliente.CdatAsync(Cdat);

        _servidor.Vistas.Should().HaveCount(3);
        _servidor.Vistas.Should().OnlyContain(v => v.Clave == null && v.Authorization == null);
        _servidor.Vistas.Select(v => v.Ruta).Should().Equal("/api/lending/term-deposits/simulate", $"/api/lending/term-deposits/{Cdat}/redeem/simulate",
            $"/api/lending/term-deposits/{Cdat}");
    }

    [Fact]
    public void Los_puntos_se_muestran_desde_la_fraccion()
    {
        TextosDeCdat.Puntos(0.102m).Should().Be("10,2 %");
        TextosDeCdat.Puntos(null).Should().Be("—");
        TextosDeCdat.Periodicidad(TextosDeCdat.AlVencimiento).Should().Be("Al vencimiento");
        TextosDeCdat.PlazoDeEmision(2).Should().Be("De 6 a 12 meses");
    }
}
