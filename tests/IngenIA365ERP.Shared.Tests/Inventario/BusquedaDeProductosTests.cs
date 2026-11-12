using System.Net;
using System.Text;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Tests.Seguridad;
using IngenIA365ERP.Shared.Tests.Sesion;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Inventario;

/// <summary>
/// 2026-11-12 (decisión del dueño): el modo de búsqueda de productos de la cooperativa (<c>Comercial.BusquedaDeProductos</c>). La leyenda
/// del campo sale del modo —antes cada pantalla la escribía a mano y no cambiaba con él—, lo que no se reconoce vale la ventana con F2,
/// y el modo se pide una vez por cooperativa: cambiar de cooperativa lo vuelve a pedir.
/// </summary>
public class BusquedaDeProductosTests
{
    private static readonly DateTime Ahora = new(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("EnElCampo", ModoDeBusquedaDeProductos.EnElCampo)]
    [InlineData("enelcampo", ModoDeBusquedaDeProductos.EnElCampo)]
    [InlineData("VentanaF2", ModoDeBusquedaDeProductos.VentanaF2)]
    [InlineData("", ModoDeBusquedaDeProductos.VentanaF2)]
    [InlineData(null, ModoDeBusquedaDeProductos.VentanaF2)]
    [InlineData("Lista", ModoDeBusquedaDeProductos.VentanaF2)]
    public void Lo_que_no_se_reconoce_es_la_ventana_con_F2(string? valor, ModoDeBusquedaDeProductos esperado)
    {
        TextosDeBusquedaDeProductos.Interpretar(valor).Should().Be(esperado);
    }

    [Fact]
    public void La_leyenda_dice_lo_que_hace_cada_modo()
    {
        var ventana = TextosDeBusquedaDeProductos.Leyenda(ModoDeBusquedaDeProductos.VentanaF2, conCantidad: true);
        var enElCampo = TextosDeBusquedaDeProductos.Leyenda(ModoDeBusquedaDeProductos.EnElCampo, conCantidad: true);

        ventana.Should().Contain("F2 busca por nombre").And.Contain("3*código").And.NotContain("↑ ↓");
        enElCampo.Should().Contain("↑ ↓").And.Contain("Esc").And.NotContain("F2");
        TextosDeBusquedaDeProductos.Leyenda(ModoDeBusquedaDeProductos.EnElCampo, conCantidad: false).Should().NotContain("3*");
        TextosDeBusquedaDeProductos.Ayuda(ModoDeBusquedaDeProductos.VentanaF2, conCantidad: true).Should().Contain("F2");
        TextosDeBusquedaDeProductos.AccionDeF2(ModoDeBusquedaDeProductos.VentanaF2).Should().Contain("por nombre");
        TextosDeBusquedaDeProductos.AccionDeF2(ModoDeBusquedaDeProductos.EnElCampo).Should().Contain("campo");
    }

    [Fact]
    public void La_cooperativa_sale_del_access()
    {
        BusquedaDeProductosDeLaCooperativa.CooperativaDe(ConCooperativa("coop-1")).Should().Be("coop-1");
        BusquedaDeProductosDeLaCooperativa.CooperativaDe(Jwt.ConVencimiento(Ahora)).Should().BeNull();
        BusquedaDeProductosDeLaCooperativa.CooperativaDe("no.es.jwt").Should().BeNull();
        BusquedaDeProductosDeLaCooperativa.CooperativaDe(null).Should().BeNull();
    }

    [Fact]
    public async Task Se_pide_una_vez_por_cooperativa_y_sin_respuesta_vale_la_ventana()
    {
        var (servicio, servidor, auth) = Servicio();
        servidor.Respuesta = "{\"mode\":\"EnElCampo\"}";
        await auth.AdoptSessionAsync(ConCooperativa("coop-1"), Ahora.AddMinutes(15), "refresh-1", Ahora.AddHours(12));

        (await servicio.ModoAsync()).Should().Be(ModoDeBusquedaDeProductos.EnElCampo);
        (await servicio.ModoAsync()).Should().Be(ModoDeBusquedaDeProductos.EnElCampo);
        servidor.Rutas.Should().Equal("/api/inventory/products/search-mode");

        servidor.Respuesta = null;
        await auth.AdoptSessionAsync(ConCooperativa("coop-2"), Ahora.AddMinutes(15), "refresh-2", Ahora.AddHours(12));

        (await servicio.ModoAsync()).Should().Be(ModoDeBusquedaDeProductos.VentanaF2, "otra cooperativa, otra pregunta; sin respuesta, el defecto");
        servidor.Rutas.Should().HaveCount(2);
    }

    private static (BusquedaDeProductosDeLaCooperativa, ServidorDeModo, CentralAuthClient) Servicio()
    {
        var servidor = new ServidorDeModo();
        var almacen = new AlmacenEnMemoria();
        var reloj = new RelojFijo(Ahora);
        var sesion = new RenovadorDeSesion(almacen, new FabricaDeUnSoloServidor(servidor), reloj);
        var http = new HttpClient(servidor, disposeHandler: false) { BaseAddress = new Uri("https://erp.pruebas") };
        var auth = new CentralAuthClient(http, sesion, new EstadoDeAutenticacionFalso());
        return (new BusquedaDeProductosDeLaCooperativa(new InventarioClient(http, auth), sesion, reloj), servidor, auth);
    }

    /// <summary>Un access sin firma válida con el vencimiento y la cooperativa activa.</summary>
    private static string ConCooperativa(string cooperativa)
    {
        static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var exp = new DateTimeOffset(Ahora.AddMinutes(15), TimeSpan.Zero).ToUnixTimeSeconds();
        return $"{B64("{\"alg\":\"RS256\"}")}.{B64($"{{\"sub\":\"ana\",\"exp\":{exp},\"active_tenant_id\":\"{cooperativa}\"}}")}.firma";
    }

    /// <summary>Responde el modo (o 500 sin él) y anota las rutas pedidas.</summary>
    private sealed class ServidorDeModo : HttpMessageHandler
    {
        public string? Respuesta { get; set; }
        public List<string> Rutas { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Rutas.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(Respuesta is null
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Respuesta, Encoding.UTF8, "application/json") });
        }
    }
}
