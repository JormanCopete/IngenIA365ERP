using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Replenishment;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Enums.Inventory;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Kardex;

/// <summary>
/// Feature 012, T244 (FR-033; contracts/api.md §5): existencias por producto y bodega —física, reservada (0 hasta I6),
/// disponible = física − reservada, en tránsito hacia la bodega = despachos confirmados menos lo recibido—, valores sólo con
/// <c>Inventory.Costs.Read</c>, el valor de una bodega = cantidad × promedio del ámbito (nunca Σ <c>TotalCost</c> de la
/// bodega), y el alcance: sin bodega, las del alcance; el tránsito sólo con alcance total o asignación explícita. También las
/// vistas <c>kardex</c> y <c>stock</c> del centro de informes (T260).
/// </summary>
public class StockQueriesTests
{
    private static GetStockQueryHandler Lista(KardexDePrueba k) =>
        new(k.C.Db, k.Alcance, k.Permisos, new PosicionDeReposicion(k.C.Db), new ValorDeExistencias(k.C.Db, k.Lector(), k.C.Reloj));

    private static GetProductStockQueryHandler Detalle(KardexDePrueba k) =>
        new(k.C.Db, k.Alcance, k.Permisos, new PosicionDeReposicion(k.C.Db), new ValorDeExistencias(k.C.Db, k.Lector(), k.C.Reloj));

    private static async Task<KardexDePrueba> DosBodegasAsync()
    {
        var k = await KardexDePrueba.CrearAsync();
        await k.EntradaAsync(k.P1, 10m, 1000m);
        await k.EntradaAsync(k.P1, 10m, 1300m, k.Segunda);
        return k;
    }

    /// <summary>Un despacho confirmado de PRIN a B2 por <paramref name="cantidad"/> y una recepción confirmada de <paramref name="recibido"/>.</summary>
    private static void Despacho(KardexDePrueba k, decimal cantidad, decimal recibido)
    {
        var tipo = new InventoryDocumentType { Code = "TRD", Name = "Despacho", Class = DocumentClass.TransferDispatch, IsActive = true };
        var recepcionTipo = new InventoryDocumentType { Code = "TRR", Name = "Recepción", Class = DocumentClass.TransferReceipt, IsActive = true };
        k.C.Db.InventoryDocumentTypes.AddRange(tipo, recepcionTipo);
        k.C.Db.SaveChanges();

        var despacho = new InventoryDocument
        {
            Class = DocumentClass.TransferDispatch, DocumentTypeId = tipo.Id, OperationDate = Catalog.CatalogoDePrueba.Hoy, BranchId = k.Sucursal.Id,
            WarehouseId = k.Principal.Id, DestinationWarehouseId = k.Segunda.Id, TransitWarehouseId = k.Transito.Id, Prefix = "TRD", Number = 1,
        };
        var lineaDespacho = new InventoryDocumentLine { Document = despacho, LineNumber = 1, ProductId = k.ProductoId(k.P1), UnitId = 1, Quantity = cantidad, QuantityBase = cantidad };
        despacho.Lines.Add(lineaDespacho);
        despacho.Confirmar(1, DateTime.UtcNow);
        var recepcion = new InventoryDocument
        {
            Class = DocumentClass.TransferReceipt, DocumentTypeId = recepcionTipo.Id, OperationDate = Catalog.CatalogoDePrueba.Hoy, BranchId = k.Sucursal.Id,
            WarehouseId = k.Segunda.Id, Prefix = "TRR", Number = 1,
        };
        var lineaRecepcion = new InventoryDocumentLine { Document = recepcion, LineNumber = 1, ProductId = k.ProductoId(k.P1), UnitId = 1, Quantity = recibido, QuantityBase = recibido };
        recepcion.Lines.Add(lineaRecepcion);
        recepcion.Confirmar(1, DateTime.UtcNow);
        k.C.Db.InventoryDocuments.AddRange(despacho, recepcion);
        k.C.Db.SaveChanges();
        var vinculo = new DocumentLink { SourceDocumentId = despacho.Id, TargetDocumentId = recepcion.Id, Kind = DocumentLinkKind.ReceiptOf };
        vinculo.LineLinks.Add(new DocumentLineLink { DocumentLink = vinculo, SourceLineId = lineaDespacho.Id, TargetLineId = lineaRecepcion.Id, QuantityBase = recibido });
        k.C.Db.DocumentLinks.Add(vinculo);
        k.C.Db.SaveChanges();
    }

    [Fact]
    public async Task Fisica_reservada_disponible_y_valor_por_bodega_al_promedio_del_ambito()
    {
        var k = await DosBodegasAsync();

        var r = await Lista(k).Handle(new GetStockQuery(), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Items.Select(i => i.Warehouse.Code).Should().Equal("B2", "PRIN");
        var principal = r.Value.Items.Single(i => i.Warehouse.Code == "PRIN");
        principal.Physical.Should().Be(10m);
        principal.Reserved.Should().Be(0m, "reservado es 0 hasta I6");
        principal.Available.Should().Be(10m);
        principal.AverageCost.Should().Be(1150m, "el promedio es el del ámbito cooperativa");
        principal.Value.Should().Be(11500m, "10 × 1.150, no los 10.000 que entraron a esa bodega");
        r.Value.Items.Single(i => i.Warehouse.Code == "B2").Value.Should().Be(11500m, "y no los 13.000");
        r.Value.Items.Sum(i => i.Value).Should().Be(23000m, "la suma de las bodegas es el valor del ámbito");
    }

    [Fact]
    public async Task Sin_permiso_de_costos_los_valores_salen_nulos()
    {
        var k = await DosBodegasAsync();
        k.Permisos.HasPermissionAsync(PermisosDeGrupo.LeerCostos, Arg.Any<CancellationToken>()).Returns(false);

        var lista = await Lista(k).Handle(new GetStockQuery(), default);
        var detalle = await Detalle(k).Handle(new GetProductStockQuery(k.P1), default);

        lista.Value.Items.Should().OnlyContain(i => i.AverageCost == null && i.Value == null);
        lista.Value.Items.Should().OnlyContain(i => i.Physical == 10m, "las cantidades sí");
        detalle.Value.CostState.Should().BeNull();
        detalle.Value.Totals.Value.Should().BeNull();
        detalle.Value.ByWarehouse.Should().OnlyContain(b => b.Value == null);
    }

    [Fact]
    public async Task En_transito_hacia_la_bodega_es_lo_despachado_menos_lo_recibido()
    {
        var k = await DosBodegasAsync();
        Despacho(k, 4m, 1m);

        var r = await Lista(k).Handle(new GetStockQuery(WarehousePublicId: k.Segunda.PublicId), default);
        var detalle = await Detalle(k).Handle(new GetProductStockQuery(k.P1), default);

        r.Value.Items.Single().InTransitTo.Should().Be(3m);
        detalle.Value.InTransit.Should().ContainSingle().Which.Quantity.Should().Be(3m);
        detalle.Value.Totals.InTransit.Should().Be(3m);
        detalle.Value.ByWarehouse.Single(b => b.Warehouse.Code == "B2").InTransitTo.Should().Be(3m);
    }

    [Fact]
    public async Task Sin_bodega_suma_las_del_alcance_y_el_transito_solo_con_asignacion()
    {
        var k = await DosBodegasAsync();
        k.C.Db.StockBalances.Add(new Domain.Entities.Inventory.Projections.StockBalance { ProductId = k.ProductoId(k.P1), WarehouseId = k.Transito.Id, Physical = 2m });
        await k.C.Db.SaveChangesAsync();
        k.Alcance.ObtenerAsync(Arg.Any<CancellationToken>())
            .Returns(new AlcanceDeInventario(false, new HashSet<int> { k.Principal.Id }, k.Principal.Id, false, new HashSet<int>(), null));

        var lista = await Lista(k).Handle(new GetStockQuery(), default);
        var detalle = await Detalle(k).Handle(new GetProductStockQuery(k.P1), default);

        lista.Value.Items.Should().ContainSingle().Which.Warehouse.Code.Should().Be("PRIN");
        detalle.Value.Totals.Physical.Should().Be(10m, "sólo las bodegas del alcance");
        detalle.Value.ByWarehouse.Should().ContainSingle();
    }

    [Fact]
    public async Task Con_alcance_total_se_ve_la_existencia_propia_del_transito()
    {
        var k = await DosBodegasAsync();
        k.C.Db.StockBalances.Add(new Domain.Entities.Inventory.Projections.StockBalance { ProductId = k.ProductoId(k.P1), WarehouseId = k.Transito.Id, Physical = 2m });
        await k.C.Db.SaveChangesAsync();

        var lista = await Lista(k).Handle(new GetStockQuery(), default);

        lista.Value.Items.Should().Contain(i => i.Warehouse.Code == "TR01" && i.Warehouse.IsTransit);
    }

    [Fact]
    public async Task Una_bodega_fuera_del_alcance_es_404()
    {
        var k = await DosBodegasAsync();
        k.Alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Vacio);

        var r = await Lista(k).Handle(new GetStockQuery(WarehousePublicId: k.Principal.PublicId), default);

        r.Error.Code.Should().Be("Inventory.Warehouse.NotFound");
    }

    [Fact]
    public async Task Solo_con_existencia_y_bajo_el_punto_de_reorden()
    {
        var k = await DosBodegasAsync();
        k.C.Db.ReorderPolicies.Add(new ReorderPolicy { ProductId = k.ProductoId(k.P1), WarehouseId = k.Principal.Id, MinimumQuantity = 5m, ReorderPoint = 12m, MaximumQuantity = 30m });
        k.C.Db.StockBalances.Add(new Domain.Entities.Inventory.Projections.StockBalance { ProductId = k.ProductoId(k.P2), WarehouseId = k.Principal.Id });
        await k.C.Db.SaveChangesAsync();

        var conExistencia = await Lista(k).Handle(new GetStockQuery(OnlyWithStock: true), default);
        var bajo = await Lista(k).Handle(new GetStockQuery(BelowReorderPoint: true), default);

        conExistencia.Value.Items.Should().OnlyContain(i => i.Product.Code == "P1");
        var fila = bajo.Value.Items.Should().ContainSingle().Subject;
        fila.Warehouse.Code.Should().Be("PRIN");
        fila.Position.Should().Be(10m);
        fila.ReorderPoint.Should().Be(12m);
    }

    [Fact]
    public async Task El_detalle_trae_la_existencia_por_ubicacion_y_el_estado_de_costo_del_ambito()
    {
        var k = await DosBodegasAsync();

        var r = await Detalle(k).Handle(new GetProductStockQuery(k.P1), default);

        r.Value.Totals.Physical.Should().Be(20m);
        r.Value.Totals.Value.Should().Be(23000m);
        r.Value.ByLocation.Should().HaveCount(2).And.OnlyContain(l => l.Location.Code == "GENERAL" && l.Quantity == 10m);
        r.Value.CostState.Should().BeEquivalentTo(new { Scope = CostScope.Cooperative, Method = CostMethod.WeightedAverage, Quantity = 20m, AverageCost = 1150m, Value = 23000m },
            o => o.ExcludingMissingMembers());
    }

    [Fact]
    public async Task Un_producto_inexistente_es_404()
    {
        var k = await DosBodegasAsync();

        var r = await Detalle(k).Handle(new GetProductStockQuery(Guid.NewGuid()), default);

        r.Error.Code.Should().Be("Inventory.Product.NotFound");
    }

    // ------------------------------------------------------------------------------------------ vistas --

    [Fact]
    public async Task La_vista_kardex_parte_del_saldo_inicial_y_acumula_cantidad_y_valor()
    {
        var k = await DosBodegasAsync();
        (await k.AjusteAsync(k.Borrador("AJN", causa: k.Causa(), lineas: [k.Linea(k.P1, 5m)]))).Confirmacion.IsSuccess.Should().BeTrue();
        var filtros = new FiltrosDeInformeDeInventario { Product = k.P1, From = Catalog.CatalogoDePrueba.Hoy, To = Catalog.CatalogoDePrueba.Hoy };

        var r = await new KardexReportQueryHandler(k.C.Db, k.Alcance, k.Permisos, k.C.Reloj).Handle(new KardexReportQuery(filtros), default);

        r.IsSuccess.Should().BeTrue();
        var filas = r.Value.Filas;
        filas.Should().HaveCount(4, "saldo inicial y tres movimientos");
        filas[0].Valores[2].Should().Be("Saldo inicial");
        filas[0].Valores[9].Should().Be(0m);
        filas[^1].Valores[9].Should().Be(15m, "saldo en cantidad");
        filas[^1].Valores[13].Should().Be(17250m, "23.000 − 5 × 1.150");
        filas[^1].Valores[14].Should().Be(1150m, "costo promedio");
        r.Value.Columnas.Where(c => c.EsOculta).Select(c => c.Clave).Should().Equal("_documento", "_producto", "_bodega");
    }

    [Fact]
    public async Task La_vista_kardex_de_una_bodega_en_ambito_cooperativa_valora_la_cantidad_al_promedio()
    {
        var k = await DosBodegasAsync();
        var filtros = new FiltrosDeInformeDeInventario { Product = k.P1, Warehouse = k.Principal.PublicId, From = Catalog.CatalogoDePrueba.Hoy };

        var r = await new KardexReportQueryHandler(k.C.Db, k.Alcance, k.Permisos, k.C.Reloj).Handle(new KardexReportQuery(filtros), default);

        var ultima = r.Value.Filas[^1];
        ultima.Valores[9].Should().Be(10m);
        ultima.Valores[13].Should().Be(10000m, "cuando entró a PRIN el promedio era 1.000");
        r.Value.Filas.Should().HaveCount(2, "sólo los movimientos de PRIN");
    }

    [Fact]
    public async Task La_vista_kardex_exige_el_producto()
    {
        var k = await DosBodegasAsync();

        var r = await new KardexReportQueryHandler(k.C.Db, k.Alcance, k.Permisos, k.C.Reloj)
            .Handle(new KardexReportQuery(new FiltrosDeInformeDeInventario()), default);

        r.Error.Code.Should().Be("Validation.Invalid");
    }

    [Fact]
    public async Task La_vista_kardex_sin_permiso_de_costos_deja_vacias_las_columnas_de_valor_y_lo_dice()
    {
        var k = await DosBodegasAsync();
        k.Permisos.HasPermissionAsync(PermisosDeGrupo.LeerCostos, Arg.Any<CancellationToken>()).Returns(false);

        var r = await new KardexReportQueryHandler(k.C.Db, k.Alcance, k.Permisos, k.C.Reloj)
            .Handle(new KardexReportQuery(new FiltrosDeInformeDeInventario { Product = k.P1 }), default);

        r.Value.Filas.Should().OnlyContain(f => f.Valores[10] == null && f.Valores[13] == null);
        r.Value.Notas.Should().Contain(n => n.Contains("Inventory.Costs.Read"));
    }

    [Fact]
    public async Task La_vista_stock_trae_existencia_transito_y_reorden_por_bodega()
    {
        var k = await DosBodegasAsync();
        Despacho(k, 4m, 1m);

        var r = await new StockReportQueryHandler(k.C.Db, k.Alcance, new PosicionDeReposicion(k.C.Db))
            .Handle(new StockReportQuery(new FiltrosDeInformeDeInventario()), default);

        r.Value.Filas.Should().HaveCount(2);
        var b2 = r.Value.Filas.Single(f => (string)f.Valores[0]! == "B2");
        b2.Valores[3].Should().Be(10m);
        b2.Valores[5].Should().Be(10m);
        b2.Valores[6].Should().Be(3m);
    }
}
