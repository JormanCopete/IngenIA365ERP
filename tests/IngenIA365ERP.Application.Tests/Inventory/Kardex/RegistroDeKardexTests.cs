using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Kardex;

/// <summary>
/// Feature 012, T240 (FR-001 a FR-004, FR-034; data-model §3.1–§3.4): <see cref="RegistroDeKardex"/>, el único escritor del kardex
/// y sus proyecciones, por el ciclo común real. Una entrada y una salida dejan hechos con el signo de su <c>Kind</c>, cantidad
/// distinta de cero y <c>TotalCost = round(QuantityBase × UnitCost)</c>, en el ámbito de costo que diga el parámetro, y
/// actualizan existencia, detalle en la ubicación por defecto y estado de costo; una salida que deja el disponible bajo cero se
/// rechaza con todas las líneas que fallan, salvo el negativo permitido por bodega; el tránsito nunca es origen; un producto
/// bloqueado no se mueve. La concurrencia real (dos salidas del último disponible) la prueba la e2e
/// <c>ConcurrenciaDeExistenciasTests</c>.
/// </summary>
public class RegistroDeKardexTests
{
    private static string Codigo<T>(Result<T> r) => r.IsFailure ? r.Error.Code : "(éxito)";

    private static object Dato(Error error, string campo) =>
        error.Should().BeOfType<ErrorConDatos>().Subject.Data.GetType().GetProperty(campo)!.GetValue(((ErrorConDatos)error).Data)!;

    [Fact]
    public async Task Una_entrada_y_una_salida_dejan_hechos_con_su_signo_y_actualizan_las_tres_proyecciones()
    {
        var k = await KardexDePrueba.CrearAsync();
        await k.EntradaAsync(k.P1, 10m, 1000m);

        var (_, salida) = await k.AjusteAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(k.P1, 4m)]));

        salida.IsSuccess.Should().BeTrue(salida.IsFailure ? salida.Error.Message : string.Empty);
        var p1 = k.ProductoId(k.P1);
        var hechos = await k.C.Db.KardexEntries.OrderBy(e => e.Id).ToListAsync();
        hechos.Should().HaveCount(2);
        hechos[0].Kind.Should().Be(KardexEntryKind.Entry);
        hechos[0].QuantityBase.Should().Be(10m);
        hechos[0].TotalCost.Should().Be(10000m);
        hechos[1].Kind.Should().Be(KardexEntryKind.Exit);
        hechos[1].QuantityBase.Should().Be(-4m, "la salida lleva cantidad negativa");
        hechos[1].UnitCost.Should().Be(1000m);
        hechos[1].TotalCost.Should().Be(-4000m, "TotalCost = round(QuantityBase × UnitCost)");
        hechos.Should().OnlyContain(e => e.CostScopeWarehouseId == 0, "sin parámetro, el ámbito es la cooperativa");
        hechos.Should().OnlyContain(e => e.CostMethod == CostMethod.WeightedAverage);
        hechos.Should().OnlyContain(e => e.Kind != KardexEntryKind.CostAdjustment ? e.QuantityBase != 0m : e.QuantityBase == 0m);

        var existencia = await k.C.Db.StockBalances.SingleAsync(s => s.ProductId == p1 && s.WarehouseId == k.Principal.Id);
        existencia.Physical.Should().Be(6m);
        existencia.Reserved.Should().Be(0m);
        existencia.LastMovementDate.Should().Be(CatalogoDePruebaHoy());

        var general = k.Principal.Locations.Single().Id;
        var detalle = await k.C.Db.StockDetails.SingleAsync(s => s.ProductId == p1 && s.WarehouseId == k.Principal.Id);
        detalle.LocationId.Should().Be(general, "sin ubicación en la línea, la por defecto de la bodega");
        detalle.Quantity.Should().Be(6m);

        var costo = await k.C.Db.CostStates.SingleAsync(c => c.ProductId == p1);
        costo.ScopeWarehouseId.Should().Be(0);
        costo.Quantity.Should().Be(6m);
        costo.Value.Should().Be(6000m);
        costo.AverageCost.Should().Be(1000m);
        costo.LastUnitCost.Should().Be(1000m);
    }

    private static DateOnly CatalogoDePruebaHoy() => Catalog.CatalogoDePrueba.Hoy;

    [Fact]
    public async Task La_confirmacion_escribe_costo_y_ubicacion_en_la_linea_y_el_costo_total_del_documento()
    {
        var k = await KardexDePrueba.CrearAsync();
        await k.EntradaAsync(k.P1, 10m, 1000m);

        var (documento, r) = await k.AjusteAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(k.P1, 3m)]));

        r.IsSuccess.Should().BeTrue();
        var guardado = await k.C.Db.InventoryDocuments.Include(d => d.Lines).SingleAsync(d => d.PublicId == documento);
        var linea = guardado.Lines.Single();
        linea.UnitCost.Should().Be(1000m);
        linea.TotalCost.Should().Be(3000m);
        linea.LocationId.Should().Be(k.Principal.Locations.Single().Id);
        guardado.CostTotal.Should().Be(3000m);
    }

    [Fact]
    public async Task Con_ambito_bodega_el_hecho_y_el_estado_de_costo_van_por_bodega()
    {
        var k = await KardexDePrueba.CrearAsync();
        k.Parametro(ParametrosDeInventario.CosteoAmbito, "Bodega");

        await k.EntradaAsync(k.P1, 10m, 1000m);
        await k.EntradaAsync(k.P1, 10m, 1300m, k.Segunda);

        var hechos = await k.C.Db.KardexEntries.ToListAsync();
        hechos.Should().Contain(e => e.WarehouseId == k.Principal.Id && e.CostScopeWarehouseId == k.Principal.Id);
        hechos.Should().Contain(e => e.WarehouseId == k.Segunda.Id && e.CostScopeWarehouseId == k.Segunda.Id);
        var estados = await k.C.Db.CostStates.OrderBy(c => c.ScopeWarehouseId).ToListAsync();
        estados.Select(c => c.AverageCost).Should().Equal(1000m, 1300m);
    }

    [Fact]
    public async Task Una_salida_que_deja_el_disponible_bajo_cero_se_rechaza_con_cada_linea_que_falla()
    {
        var k = await KardexDePrueba.CrearAsync();
        await k.EntradaAsync(k.P1, 5m, 1000m);
        await k.EntradaAsync(k.P2, 1m, 500m);

        var (_, r) = await k.AjusteAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(k.P1, 4m), k.Linea(k.P1, 2m), k.Linea(k.P2, 3m)]));

        Codigo(r).Should().Be("Inventory.Stock.Insufficient");
        Dato(r.Error, "lineNumber").Should().Be(2, "la primera que no cabe: la línea 1 sí cabe, la 2 ya no");
        Dato(r.Error, "requested").Should().Be(2m);
        Dato(r.Error, "available").Should().Be(1m, "lo que queda después de la línea 1");
        var lineas = Dato(r.Error, "lines").Should().BeAssignableTo<IReadOnlyList<InventoryErrors.LineaSinExistencia>>().Subject;
        lineas.Select(l => l.LineNumber).Should().Equal(2, 3);
        lineas[1].ProductCode.Should().Be("P2");
        lineas[1].Available.Should().Be(1m);
        lineas[1].WarehousePublicId.Should().Be(k.Principal.PublicId);

        k.C.Db.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged && e.Entity is Domain.Entities.Inventory.Transactions.KardexEntry)
            .Should().BeEmpty("un rechazo no deja nada a medio escribir");
        (await k.C.Db.KardexEntries.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Con_el_negativo_permitido_en_la_bodega_la_salida_pasa()
    {
        var k = await KardexDePrueba.CrearAsync();
        await k.EntradaAsync(k.P1, 2m, 1000m);
        k.Parametro(ParametrosDeInventario.ExistenciasStockNegativoPermitido, "true", ParameterScopeKind.Warehouse, k.Principal.Id);

        var (_, r) = await k.AjusteAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(k.P1, 5m)]));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        (await k.C.Db.StockBalances.SingleAsync(s => s.WarehouseId == k.Principal.Id)).Physical.Should().Be(-3m);
        (await k.C.Db.CostStates.SingleAsync()).Quantity.Should().Be(-3m);
    }

    [Fact]
    public async Task El_negativo_de_otra_bodega_no_alcanza_a_esta()
    {
        var k = await KardexDePrueba.CrearAsync();
        await k.EntradaAsync(k.P1, 2m, 1000m);
        k.Parametro(ParametrosDeInventario.ExistenciasStockNegativoPermitido, "true", ParameterScopeKind.Warehouse, k.Segunda.Id);

        var (_, r) = await k.AjusteAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(k.P1, 5m)]));

        Codigo(r).Should().Be("Inventory.Stock.Insufficient");
    }

    [Fact]
    public async Task El_transito_nunca_es_origen_de_una_salida()
    {
        var k = await KardexDePrueba.CrearAsync();

        var (_, r) = await k.AjusteAsync(k.Borrador("AJN", k.Transito, k.Causa(), lineas: [k.Linea(k.P1, 1m)]));

        Codigo(r).Should().Be("Inventory.Document.TransitNotAllowed");
    }

    [Fact]
    public async Task Un_producto_bloqueado_no_se_mueve()
    {
        var k = await KardexDePrueba.CrearAsync();
        var guardado = await k.GuardarAsync(k.Borrador("AJP", lineas: [k.Linea(k.P1, 1m, 100m)]));
        k.C.Producto(k.P1).Status = ProductStatus.Blocked;
        await k.C.Db.SaveChangesAsync();

        var r = await k.ConfirmarAsync(guardado.Value.PublicId);

        Codigo(r).Should().Be("Inventory.Product.Blocked");
        (await k.C.Db.KardexEntries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task El_cerrojo_recibe_bodega_estado_de_costo_existencia_y_detalle_de_lo_que_se_mueve()
    {
        var k = await KardexDePrueba.CrearAsync();

        await k.EntradaAsync(k.P1, 1m, 100m);

        var pedido = k.Bloqueos.Should().ContainSingle().Subject;
        var p1 = k.ProductoId(k.P1);
        pedido.Bodegas.Should().Contain(k.Principal.Id);
        pedido.EstadosDeCosto.Should().ContainSingle().Which.Should().Be(new ClaveDeEstadoDeCosto(p1, 0, CostMethod.WeightedAverage));
        pedido.Existencias.Should().ContainSingle().Which.Should().Be(new ClaveDeExistencia(p1, k.Principal.Id));
        pedido.Detalles.Should().ContainSingle().Which.Should().Be(new ClaveDeDetalleDeExistencia(p1, k.Principal.Id, k.Principal.Locations.Single().Id, null));
        await k.Cerrojo.Received().BloquearAsync(Arg.Any<PedidoDeCerrojo>(), Arg.Any<CancellationToken>());
    }
}
