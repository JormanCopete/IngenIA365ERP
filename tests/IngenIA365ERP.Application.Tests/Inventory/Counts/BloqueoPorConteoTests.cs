using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Counts;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Inventory.Parameters;

namespace IngenIA365ERP.Application.Tests.Inventory.Counts;

/// <summary>
/// Feature 012, US11, T384 (FR-040, US11-1; data-model §8): con <c>Conteo.BloquearMovimientos</c>, confirmar cualquier documento que
/// mueva un producto del conteo en esa bodega responde <c>Inventory.Count.ProductsLocked</c> con <c>data { countPublicId,
/// displayNumber, products[] }</c> —el borrador lo avisa con el mismo código—; en otra bodega o con otro producto pasa; sin bloqueo
/// pasa y lo movido se suma al teórico al cerrar (<c>MovementsAfterSnapshot</c>); descartar el conteo libera los productos.
/// </summary>
public class BloqueoPorConteoTests
{
    [Fact]
    public async Task Con_bloqueo_una_salida_de_un_producto_del_conteo_en_esa_bodega_se_rechaza()
    {
        var c = await ConteosDePrueba.CrearAsync();
        await c.EntradaAsync(c.K.P1, 10m, 1_000m);
        await c.EntradaAsync(c.K.P2, 4m, 500m);
        var conteo = await c.AbiertoAsync(c.Definicion(CountScope.Selection, productos: [c.K.P1]));

        var (salida, r) = await c.SalidaAsync(c.K.P1, 2m);

        r.Error.Code.Should().Be("Inventory.Count.ProductsLocked");
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { countPublicId = conteo, displayNumber = (string?)null, products = new[] { "P1" } });
        c.Documento(salida).Status.Should().Be(DocumentStatus.Draft);
        c.Fisico(c.K.P1).Should().Be(10m);

        var borrador = await c.Guardar().Handle(new SaveInventoryDraftCommand(salida, DocumentClassGroup.Adjustments,
            c.K.Borrador("AJN", causa: c.K.Causa(), lineas: [c.K.Linea(c.K.P1, 2m)])), default);
        borrador.Value.Warnings.Should().Contain(w => w.Code == "Inventory.Count.ProductsLocked", "el borrador lo avisa sin bloquear el guardado");
    }

    [Fact]
    public async Task Otro_producto_u_otra_bodega_pasan()
    {
        var c = await ConteosDePrueba.CrearAsync();
        await c.EntradaAsync(c.K.P1, 10m, 1_000m);
        await c.EntradaAsync(c.K.P2, 4m, 500m);
        await c.EntradaAsync(c.K.P1, 5m, 1_000m, bodega: c.B2);
        await c.AbiertoAsync(c.Definicion(CountScope.Selection, productos: [c.K.P1]));

        (await c.SalidaAsync(c.K.P2, 1m)).Confirmacion.IsSuccess.Should().BeTrue("P2 no está en el conteo");
        (await c.SalidaAsync(c.K.P1, 1m, c.B2)).Confirmacion.IsSuccess.Should().BeTrue("el conteo es de PRIN");
        (await c.EntradaAsync(c.K.P2, 1m, 500m)).Should().NotBeEmpty("una entrada de otro producto tampoco se bloquea");
    }

    [Fact]
    public async Task Una_entrada_tambien_se_bloquea_y_descartar_el_conteo_libera_los_productos()
    {
        var c = await ConteosDePrueba.CrearAsync();
        await c.EntradaAsync(c.K.P1, 10m, 1_000m);
        var conteo = await c.AbiertoAsync(c.Definicion(CountScope.All));

        var entrada = await c.Guardar().Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Adjustments,
            c.K.Borrador("AJP", lineas: [c.K.Linea(c.K.P1, 1m, 1_000m)])), default);
        (await c.Confirmacion().ConfirmarAsync(new PedidoDeConfirmacion(entrada.Value.PublicId, DocumentClassGroup.Adjustments), default))
            .Error.Code.Should().Be("Inventory.Count.ProductsLocked", "un conteo total cubre todo producto de la bodega, entre o salga");

        (await c.Descartar().Handle(new DiscardInventoryDraftCommand(conteo, DocumentClassGroup.Counts, "se reprograma"), default)).IsSuccess.Should().BeTrue();
        (await c.Confirmacion().ConfirmarAsync(new PedidoDeConfirmacion(entrada.Value.PublicId, DocumentClassGroup.Adjustments), default))
            .IsSuccess.Should().BeTrue("descartado el conteo, sus productos quedan libres");
        c.Fisico(c.K.P1).Should().Be(11m);
    }

    [Fact]
    public async Task Sin_bloqueo_la_salida_pasa_y_se_suma_al_teorico_al_cerrar()
    {
        var c = await ConteosDePrueba.CrearAsync();
        c.Parametro(ParametrosDeInventario.ConteoBloquearMovimientos, "false", c.PRIN);
        await c.EntradaAsync(c.K.P1, 10m, 1_000m);
        var conteo = await c.AbiertoAsync();

        (await c.SalidaAsync(c.K.P1, 3m)).Confirmacion.IsSuccess.Should().BeTrue();
        c.ComoUsuario(ConteosDePrueba.ContadorA);
        (await c.CapturarAsync(conteo, 1, c.Lectura(c.K.P1, 7m))).IsSuccess.Should().BeTrue();
        c.ComoUsuario(ConteosDePrueba.Jefe);
        var cerrado = await c.CerrarAsync(conteo);

        cerrado.IsSuccess.Should().BeTrue(cerrado.IsFailure ? cerrado.Error.Code : string.Empty);
        c.Lineas(conteo).Single().Should().BeEquivalentTo(new
        {
            TheoreticalQuantity = 10m, MovementsAfterSnapshot = (decimal?)-3m, CountedQuantity = (decimal?)7m, Difference = (decimal?)0m,
        }, o => o.ExcludingMissingMembers());
    }

    [Fact]
    public async Task El_conteo_no_se_bloquea_a_si_mismo_ni_a_lo_que_no_mueve_existencia()
    {
        var c = await ConteosDePrueba.CrearAsync();
        await c.EntradaAsync(c.K.P1, 10m, 1_000m);
        var conteo = await c.AbiertoAsync(c.Definicion(CountScope.All));

        var error = await c.Bloqueo().EvaluarAsync(c.Documento(conteo), null, default);

        error.Should().BeNull();
        BloqueoPorConteo.MueveExistencia(Domain.Inventory.Documents.InventoryEffect.CostOnly).Should().BeFalse();
        BloqueoPorConteo.MueveExistencia(Domain.Inventory.Documents.InventoryEffect.Both).Should().BeTrue();
    }
}
