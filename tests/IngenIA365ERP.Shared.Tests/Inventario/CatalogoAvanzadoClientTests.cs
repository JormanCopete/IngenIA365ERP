using System.Net;
using FluentAssertions;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Inventario;

/// <summary>
/// Feature 012, I6, T936–T941 (US15; contracts/api.md §3.5, §5, §6.1, §10, §12): el parcial <c>InventarioClient.CatalogoAvanzado</c> —atributos,
/// variantes, componentes, lotes y series—, la búsqueda para la venta, el kardex por lote, el ensamble en el borrador de un ajuste y el conteo
/// por clase ABC. Toda escritura lleva la clave de su operación y ninguna petición pone <c>Authorization</c> a mano
/// (<c>ElTokenDeSesionLoPoneElHandler</c>). También la vista previa de las combinaciones que la ficha muestra antes de generar
/// (<see cref="VistaPreviaDeVariantes"/>) y los textos del estado de un lote. (nuevo)
/// </summary>
public class CatalogoAvanzadoClientTests
{
    private readonly ServidorDeIntegracion _servidor = new();
    private readonly InventarioClient _cliente;

    public CatalogoAvanzadoClientTests()
    {
        var (http, auth) = _servidor.ConSesion();
        _cliente = new InventarioClient(http, auth);
    }

    private static readonly object Vacio = Array.Empty<object>();

    [Fact]
    public async Task Las_lecturas_van_por_su_ruta_sin_clave()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, Vacio);
        var producto = Guid.NewGuid();
        var bodega = Guid.NewGuid();

        (await _cliente.ListarAtributosDeVarianteAsync(incluirInactivos: true)).IsSuccess.Should().BeTrue();
        (await _cliente.ListarVariantesAsync(producto)).IsSuccess.Should().BeTrue();
        (await _cliente.ListarLotesAsync(producto, bodega, incluirVencidos: true)).IsSuccess.Should().BeTrue();
        (await _cliente.ListarSeriesAsync(producto, bodega, enExistencia: true)).IsSuccess.Should().BeTrue();
        (await _cliente.ListarLotesAsync(producto)).IsSuccess.Should().BeTrue();

        _servidor.Vistas.Select(v => v.Ruta).Should().Equal(
            "/api/inventory/variant-attributes?includeInactive=true",
            $"/api/inventory/products/{producto}/variants",
            $"/api/inventory/lots?productPublicId={producto}&warehousePublicId={bodega}&includeExpired=true",
            $"/api/inventory/serials?productPublicId={producto}&warehousePublicId={bodega}&inStock=true",
            $"/api/inventory/lots?productPublicId={producto}");
        _servidor.Vistas.Should().OnlyContain(v => v.Metodo == "GET" && v.Clave == null && v.Authorization == null);
    }

    [Fact]
    public async Task Los_componentes_se_leen_y_se_reemplazan_con_la_clave_de_la_operacion()
    {
        var producto = Guid.NewGuid();
        var componente = Guid.NewGuid();
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK,
            new { productPublicId = producto, code = "CMB", kind = 3, components = Array.Empty<object>() });

        (await _cliente.ObtenerComponentesAsync(producto)).IsSuccess.Should().BeTrue();
        var r = await _cliente.FijarComponentesAsync(producto, new ComponentesRequest([new ComponentePedidoRequest(componente, 2m)]), new ClaveDeOperacion());

        r.IsSuccess.Should().BeTrue(r.ErrorMessage);
        r.Value!.Code.Should().Be("CMB");
        var escritura = _servidor.Vistas[^1];
        (escritura.Metodo, escritura.Ruta).Should().Be(("PUT", $"/api/inventory/products/{producto}/components"));
        escritura.Cuerpo.Should().Contain($"\"componentProductPublicId\":\"{componente}\"").And.Contain("\"quantity\":2");
        escritura.Clave.Should().NotBeNullOrWhiteSpace();
        escritura.Authorization.Should().BeNull();
        _servidor.Vistas[0].Clave.Should().BeNull("la lectura no lleva clave");
    }

    [Fact]
    public async Task El_atributo_nuevo_va_por_POST_y_el_existente_por_PUT_y_generar_variantes_lleva_los_valores()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new { publicId = Guid.NewGuid() });
        var atributo = Guid.NewGuid();
        var plantilla = Guid.NewGuid();
        var valor = Guid.NewGuid();
        var pedido = new AtributoDeVarianteRequest("TALLA", "Talla", [new ValorDeAtributoRequest("S", "Pequeña", 1)]);

        await _cliente.GuardarAtributoDeVarianteAsync(null, pedido, new ClaveDeOperacion());
        await _cliente.GuardarAtributoDeVarianteAsync(atributo, pedido, new ClaveDeOperacion());
        await _cliente.GenerarVariantesAsync(plantilla, new GenerarVariantesRequest([new AtributoElegidoRequest(atributo, [valor])],
            [new VarianteAjustadaRequest("TALLA=S", Barcode: "7701234567890")]), new ClaveDeOperacion());

        _servidor.Vistas.Select(v => (v.Metodo, v.Ruta)).Should().Equal(
            ("POST", "/api/inventory/variant-attributes"),
            ("PUT", $"/api/inventory/variant-attributes/{atributo}"),
            ("POST", $"/api/inventory/products/{plantilla}/variants"));
        _servidor.Vistas.Should().OnlyContain(v => v.Clave != null && v.Authorization == null);
        _servidor.Vistas[2].Cuerpo.Should().Contain($"\"valuePublicIds\":[\"{valor}\"]").And.Contain("\"variantKey\":\"TALLA=S\"");
    }

    [Fact]
    public async Task La_busqueda_para_la_venta_y_el_kardex_por_lote_llevan_su_filtro()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new { exact = (object?)null, items = Vacio });
        var producto = Guid.NewGuid();

        await _cliente.BuscarProductosAsync("cam", paraVenta: true);
        await _cliente.KardexAsync(producto, null, null, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), lote: "L-01");

        _servidor.Vistas[0].Ruta.Should().Be("/api/inventory/products/search?q=cam&forSale=true");
        _servidor.Vistas[1].Ruta.Should().Contain("lot=L-01");
    }

    [Fact]
    public async Task El_borrador_de_un_ensamble_lleva_el_kit_y_la_cantidad()
    {
        _servidor.Responder = _ => ServidorDeIntegracion.Json(HttpStatusCode.OK, new { publicId = Guid.NewGuid() });
        var kit = Guid.NewGuid();
        var borrador = new BorradorDeInventarioRequest(Guid.NewGuid(), null, Guid.NewGuid(), null, null, null, null, null, null, null, null, null, null, [],
            Assembly: new EnsambleRequest(kit, 5m));

        await _cliente.GuardarAjusteAsync(null, borrador, new ClaveDeOperacion());

        _servidor.Vistas.Single().Cuerpo.Should().Contain($"\"assembly\":{{\"kitProductPublicId\":\"{kit}\",\"quantity\":5}}");
    }

    [Fact]
    public void La_vista_previa_combina_los_valores_con_la_clave_ordenada_por_atributo_como_el_servidor()
    {
        var combinaciones = VistaPreviaDeVariantes.Combinaciones("CAM",
        [
            new VistaPreviaDeVariantes.Atributo("TALLA", [new("S", "Pequeña"), new("M", "Mediana")]),
            new VistaPreviaDeVariantes.Atributo("COLOR", [new("AZUL", "Azul"), new("ROJO", "Rojo")]),
        ], existentes: ["COLOR=AZUL;TALLA=S"]);

        combinaciones.Select(c => (c.VariantKey, c.Existe)).Should().Equal(
            ("COLOR=AZUL;TALLA=S", true),
            ("COLOR=ROJO;TALLA=S", false),
            ("COLOR=AZUL;TALLA=M", false),
            ("COLOR=ROJO;TALLA=M", false));
        combinaciones[1].Nombre.Should().Be("CAM Pequeña Rojo", "el nombre sigue el orden en que se eligieron los atributos");
        VistaPreviaDeVariantes.Combinaciones("CAM", [new VistaPreviaDeVariantes.Atributo("TALLA", [])], []).Should().BeEmpty();
    }

    [Fact]
    public void El_estado_de_un_lote_se_lee_en_castellano()
    {
        TextosDeInventario.EstadoDeLote("Current").Should().Be("Vigente");
        TextosDeInventario.EstadoDeLote("ExpiringSoon").Should().Be("Próximo a vencer");
        TextosDeInventario.EstadoDeLote("Expired").Should().Be("Vencido");
        TextosDeInventario.EstadoDeLote("NoExpiry").Should().Be("Sin vencimiento");
    }
}
