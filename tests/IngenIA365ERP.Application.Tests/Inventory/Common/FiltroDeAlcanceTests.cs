using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Common;

/// <summary>
/// Feature 012, T087 (T35, FR-009; data-model §5.2, §21): el filtro de alcance por bodega y punto de venta. Las
/// expresiones se prueban sobre <c>IQueryable</c> en memoria con filas de juguete: son genéricas por Id de bodega y
/// punto, así que no necesitan las entidades de inventario (fase 3 en adelante).
/// </summary>
public class FiltroDeAlcanceTests
{
    private const int Norte = 1, Sur = 2, Transito = 9, Caja1 = 11, Caja2 = 12;

    private sealed record Existencia(int WarehouseId, decimal Cantidad);

    private sealed record Sesion(int PointOfSaleId);

    /// <summary>Un documento: bodega de origen, de destino y las bodegas de los documentos de los que nace (vínculos).</summary>
    private sealed record Documento(string Numero, int? WarehouseId, int? DestinationWarehouseId, int[] BodegasDeOrigenes);

    private static AlcanceDeInventario Solo(params int[] bodegas) =>
        AlcanceDeInventario.Vacio with { Bodegas = bodegas.ToHashSet() };

    private static readonly Documento[] Documentos =
    [
        new("AJ-1", Norte, null, []),
        new("AJ-2", Sur, null, []),
        new("TR-1", Norte, Sur, []),
        new("TR-2", Sur, Norte, []),
        new("FP-1", null, null, [Norte, Sur]),
        new("FP-2", null, null, [Sur]),
        new("CA-1", null, null, []),
    ];

    [Fact]
    public void Las_existencias_se_filtran_por_bodega()
    {
        var filas = new[] { new Existencia(Norte, 5), new Existencia(Sur, 7), new Existencia(Transito, 1) }.AsQueryable();

        filas.PorBodega(Solo(Norte), e => e.WarehouseId).Select(e => e.WarehouseId).Should().Equal(Norte);
        filas.PorBodega(AlcanceDeInventario.Vacio, e => e.WarehouseId).Should().BeEmpty("sin asignaciones falla cerrado");
        filas.PorBodega(AlcanceDeInventario.Total, e => e.WarehouseId).Should().HaveCount(3);
    }

    [Fact]
    public void La_existencia_propia_de_la_bodega_de_transito_solo_con_alcance_total_o_asignacion_explicita()
    {
        var filas = new[] { new Existencia(Norte, 5), new Existencia(Transito, 1) }.AsQueryable();

        filas.PorBodega(Solo(Norte), e => e.WarehouseId).Select(e => e.WarehouseId).Should().NotContain(Transito);
        filas.PorBodega(Solo(Norte, Transito), e => e.WarehouseId).Select(e => e.WarehouseId).Should().Contain(Transito);
    }

    [Fact]
    public void Un_documento_se_ve_por_su_bodega_de_origen_o_de_destino()
    {
        var visibles = Documentos.AsQueryable()
            .DocumentosPorBodega(Solo(Norte), d => d.WarehouseId, d => d.DestinationWarehouseId, d => d.BodegasDeOrigenes)
            .Select(d => d.Numero);

        visibles.Should().BeEquivalentTo("AJ-1", "TR-1", "TR-2", "FP-1");
    }

    [Fact]
    public void Un_documento_sin_bodega_se_ve_si_alguna_bodega_de_sus_origenes_esta_en_el_alcance()
    {
        var visibles = Documentos.AsQueryable()
            .DocumentosPorBodega(Solo(Sur), d => d.WarehouseId, d => d.DestinationWarehouseId, d => d.BodegasDeOrigenes)
            .Select(d => d.Numero);

        visibles.Should().BeEquivalentTo("AJ-2", "TR-1", "TR-2", "FP-1", "FP-2");
        visibles.Should().NotContain("CA-1", "un documento sin bodega ni orígenes sólo lo ve el alcance total");
    }

    [Fact]
    public void Con_alcance_total_se_ven_todos_los_documentos()
    {
        Documentos.AsQueryable()
            .DocumentosPorBodega(AlcanceDeInventario.Total, d => d.WarehouseId, d => d.DestinationWarehouseId, d => d.BodegasDeOrigenes)
            .Should().HaveCount(Documentos.Length);
    }

    [Fact]
    public void Un_documento_sin_bodega_es_operable_solo_si_todas_sus_bodegas_de_origen_estan_en_el_alcance()
    {
        FiltroDeAlcance.DocumentoSinBodegaVisible(Solo(Norte), [Norte, Sur]).Should().BeTrue();
        FiltroDeAlcance.DocumentoSinBodegaOperable(Solo(Norte), [Norte, Sur]).Should().BeFalse();
        FiltroDeAlcance.DocumentoSinBodegaOperable(Solo(Norte, Sur), [Norte, Sur]).Should().BeTrue();
        FiltroDeAlcance.DocumentoSinBodegaOperable(Solo(Norte), []).Should().BeFalse("sin orígenes no hay de dónde tomar el alcance");
        FiltroDeAlcance.DocumentoSinBodegaOperable(AlcanceDeInventario.Total, []).Should().BeTrue();
    }

    [Fact]
    public void Las_sesiones_se_filtran_por_punto_de_venta()
    {
        var sesiones = new[] { new Sesion(Caja1), new Sesion(Caja2) }.AsQueryable();
        var alcance = AlcanceDeInventario.Vacio with { Puntos = new HashSet<int> { Caja2 } };

        sesiones.PorPunto(alcance, s => s.PointOfSaleId).Select(s => s.PointOfSaleId).Should().Equal(Caja2);
        sesiones.PorPunto(AlcanceDeInventario.Total, s => s.PointOfSaleId).Should().HaveCount(2);
    }

    [Fact]
    public async Task AsegurarAsync_responde_el_404_de_la_entidad_si_el_comando_toca_una_bodega_fuera()
    {
        var alcance = Substitute.For<IAlcanceDeInventario>();
        alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(Solo(Norte));

        (await alcance.AsegurarAsync([Norte])).IsSuccess.Should().BeTrue();
        var fuera = await alcance.AsegurarAsync([Norte, Sur]);
        fuera.Error.Code.Should().Be("Inventory.Warehouse.NotFound");

        var propio = await alcance.AsegurarAsync([Sur], siFuera: new Error("Inventory.Document.NotFound", "El documento no existe."));
        propio.Error.Code.Should().Be("Inventory.Document.NotFound");

        var punto = await alcance.AsegurarAsync([], puntos: [Caja1]);
        punto.Error.Code.Should().Be("Inventory.PointOfSale.NotFound");
    }
}
