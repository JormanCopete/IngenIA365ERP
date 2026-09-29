using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Inventory.Periods;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Application.Inventory.Reports.Vistas;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Tests.Inventory.Catalog;
using IngenIA365ERP.Application.Tests.Inventory.Purchasing;
using IngenIA365ERP.Application.Tests.Inventory.Sales;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Warehousing;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Reports;

/// <summary>
/// Feature 012, US17, T947 (FR-086, FR-021; contracts/api.md §27, filas de I6): las vistas de analítica con un juego de datos conocido,
/// contrastadas con cifras calculadas a mano. La cooperativa es la de <see cref="VentasDePrueba"/> (P1 arroz 100 u a 1.000 con precio
/// 2.000; P3 aceite 50 u a 6.000 con precio 10.000, todo en PRIN el 25 de septiembre de 2026) con estas ventas confirmadas hoy:
/// <list type="bullet">
/// <item>a Ana, por la vendedora Luz: P1 × 10 (venta 20.000, costo 10.000) y P3 × 2 (venta 20.000, costo 12.000);</item>
/// <item>sin cliente ni vendedor, en el punto PV1: P1 × 5 (venta 10.000, costo 5.000);</item>
/// <item>una nota que devuelve 2 de P1 de la venta a Ana (−4.000 de venta, −2.000 de costo).</item>
/// </list>
/// Venta neta 46.000, costo 25.000, margen 21.000 (45,65 %); P1 26.000 contra 13.000 (50 %); P3 20.000 contra 12.000 (40 %).
/// </summary>
public class VistasAvanzadasTests
{
    private static readonly DateOnly Hoy = CatalogoDePrueba.Hoy;

    private static int Columna(TablaExportable tabla, string titulo) =>
        tabla.Columnas.Select((c, i) => (c, i)).First(x => x.c.Nombre == titulo && x.c.Clave is null).i;

    private static object? Celda(TablaExportable tabla, FilaExportable fila, string titulo) => fila.Valores[Columna(tabla, titulo)];

    private static FilaExportable Fila(TablaExportable tabla, string primera) =>
        tabla.Filas.Single(f => ((string)f.Valores[0]!).StartsWith(primera, StringComparison.Ordinal));

    private static FiltrosDeInformeDeInventario Septiembre() => new() { From = new DateOnly(2026, 9, 1), To = Hoy };

    private static AnaliticaDeInventario Analitica(VentasDePrueba v) => new(v.Db, new ValorizadoALaFecha(v.Db, v.K.Lector()));

    private static AnaliticaDeInventario Analitica(ComprasDePrueba c) => new(c.C.Db, new ValorizadoALaFecha(c.C.Db, c.K.Lector()));

    /// <summary>Guarda, paga en efectivo y confirma una venta con vendedor; falla la prueba si algo no pasa.</summary>
    private static async Task<InventoryDocument> VentaAsync(VentasDePrueba v, Person? cliente, Guid? vendedor, params SalesLineInput[] lineas)
    {
        var borrador = await v.GuardarAsync(v.Venta(cliente: cliente, vendedor: vendedor, lineas: lineas));
        borrador.IsSuccess.Should().BeTrue(borrador.IsFailure ? $"{borrador.Error.Code}: {borrador.Error.Message}" : string.Empty);
        var total = v.Documento(borrador.Value.PublicId).AmountDue;
        var conPago = await v.GuardarAsync(v.Venta(cliente: cliente, vendedor: vendedor, pagos: [v.Pago(v.Efectivo, total)], lineas: lineas), borrador.Value.PublicId);
        conPago.IsSuccess.Should().BeTrue(conPago.IsFailure ? $"{conPago.Error.Code}: {conPago.Error.Message}" : string.Empty);
        var r = await v.ConfirmarAsync(borrador.Value.PublicId);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return v.Documento(borrador.Value.PublicId);
    }

    private static async Task<VentasDePrueba> ConVentasAsync()
    {
        var v = await VentasDePrueba.CrearAsync();
        var aAna = await VentaAsync(v, v.Ana, v.Luz.PublicId, v.Linea(v.P1, 10m), v.Linea(v.P3, 2m));
        var enElPunto = await VentaAsync(v, null, null, v.Linea(v.P1, 5m));
        enElPunto.PointOfSaleId = v.Punto.Id;
        await v.Db.SaveChangesAsync();

        var lineaP1 = aAna.Lines.Single(l => !l.IsDeleted && l.ProductId == v.K.ProductoId(v.P1));
        var nota = await v.NotaAsync(new CreditNoteDraftInput(aAna.PublicId, "Devuelve dos", false, true, [new CreditNoteLineInput(lineaP1.PublicId, 2m)], []));
        nota.IsSuccess.Should().BeTrue(nota.IsFailure ? $"{nota.Error.Code}: {nota.Error.Message}" : string.Empty);
        var r = await v.ConfirmarAsync(nota.Value.PublicId);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return v;
    }

    private static Task<Result<TablaExportable>> MargenAsync(VentasDePrueba v, string? by, FiltrosDeInformeDeInventario? f = null) =>
        new MarginReportQueryHandler(v.Db, v.K.Alcance, v.K.Permisos, Analitica(v), v.Compras.C.Reloj)
            .Handle(new MarginReportQuery(f ?? Septiembre(), by), default);

    // ------------------------------------------------------------------------------------------------ margin --

    [Fact]
    public async Task El_margen_por_producto_resta_las_notas_de_la_venta_y_del_costo()
    {
        var v = await ConVentasAsync();

        var t = (await MargenAsync(v, "product")).Value;

        t.Columnas.Where(c => !c.EsOculta).Select(c => c.Nombre).Should().Equal("Dimensión", "Ventas netas", "Costo de venta", "Margen", "Margen (%)");
        var p1 = Fila(t, "P1");
        (Celda(t, p1, "Ventas netas"), Celda(t, p1, "Costo de venta"), Celda(t, p1, "Margen"), Celda(t, p1, "Margen (%)"))
            .Should().Be((26000m, 13000m, 13000m, 50.00m));
        var p3 = Fila(t, "P3");
        (Celda(t, p3, "Ventas netas"), Celda(t, p3, "Costo de venta"), Celda(t, p3, "Margen"), Celda(t, p3, "Margen (%)"))
            .Should().Be((20000m, 12000m, 8000m, 40.00m));
        t.Totales!.Valores.Skip(1).Should().Equal(46000m, 25000m, 21000m, 45.65m);
    }

    [Fact]
    public async Task El_margen_se_abre_por_las_cinco_dimensiones()
    {
        var v = await ConVentasAsync();

        var categoria = (await MargenAsync(v, "category")).Value;
        var vendedor = (await MargenAsync(v, "salesperson")).Value;
        var cliente = (await MargenAsync(v, "customer")).Value;
        var punto = (await MargenAsync(v, "point")).Value;

        categoria.Filas.Should().ContainSingle("P1 y P3 son de la misma categoría").Which.Valores[1].Should().Be(46000m);
        Celda(vendedor, Fila(vendedor, "Luz"), "Ventas netas").Should().Be(36000m);
        Celda(vendedor, Fila(vendedor, MarginReportQueryHandler.SinVendedor), "Ventas netas").Should().Be(10000m);
        cliente.Filas.Single(f => ((string)f.Valores[0]!).Contains("Ana")).Valores[1].Should().Be(36000m);
        cliente.Filas.Sum(f => (decimal)f.Valores[1]!).Should().Be(46000m);
        // La nota reintegra en la sesión de caja de PV1: resta donde ocurrió (10.000 − 4.000), no donde se vendió.
        Celda(punto, Fila(punto, "PV1"), "Ventas netas").Should().Be(6000m);
        Celda(punto, Fila(punto, MarginReportQueryHandler.SinPunto), "Ventas netas").Should().Be(40000m);
        foreach (var t in new[] { categoria, vendedor, cliente, punto })
            t.Totales!.Valores.Skip(1).Should().Equal(46000m, 25000m, 21000m, 45.65m);
    }

    [Fact]
    public async Task El_margen_exige_costos_y_por_cliente_declara_datos_personales()
    {
        var v = await ConVentasAsync();
        v.K.Permisos.HasPermissionAsync("Inventory.Costs.Read", Arg.Any<CancellationToken>()).Returns(false);

        var r = await MargenAsync(v, "product");

        r.IsFailure.Should().BeTrue();
        r.Error.Should().Be(Error.NotFound);
        MarginReportQueryHandler.Vista.RequiredPermission.Should().Be("Inventory.Costs.Read");
        MarginReportQueryHandler.Vista.PersonalDataWhen.Should().Be("by=customer");
        MarginReportQueryHandler.Vista.TraeDatosPersonales(p => p == "by" ? "customer" : null).Should().BeTrue();
        MarginReportQueryHandler.Vista.TraeDatosPersonales(p => p == "by" ? "product" : null).Should().BeFalse();
    }

    [Fact]
    public async Task Una_dimension_desconocida_es_FilterInvalid()
    {
        var v = await ConVentasAsync();

        var r = await MargenAsync(v, "planeta");

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(AnaliticaDeInventario.FiltroInvalidoCodigo);
    }

    [Fact]
    public async Task El_margen_respeta_el_alcance()
    {
        var v = await ConVentasAsync();
        v.K.Alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(
            new AlcanceDeInventario(false, new HashSet<int> { v.K.Segunda.Id }, null, false, new HashSet<int>(), null));

        (await MargenAsync(v, "product")).Value.Filas.Should().BeEmpty("las ventas son de PRIN, fuera del alcance");
    }

    // ---------------------------------------------------------------------------------------------- turnover --

    [Fact]
    public async Task La_rotacion_por_producto_divide_el_costo_de_venta_por_el_inventario_promedio()
    {
        var v = await ConVentasAsync();

        var t = (await new TurnoverReportQueryHandler(v.Db, v.K.Alcance, v.K.Permisos, Analitica(v), v.Compras.C.Reloj)
            .Handle(new TurnoverReportQuery(Septiembre(), "product"), default)).Value;

        t.Columnas.Where(c => !c.EsOculta).Select(c => c.Nombre).Should().Equal(
            "Dimensión", "Costo de venta del período", "Inventario promedio", "Rotación", "Días de inventario");
        // P1: saldos 0 (31/08) y 87.000 (25/09) → promedio 43.500; costo 13.000 → 0,30 veces; 25 × 43.500 ÷ 13.000 = 83,65 días.
        var p1 = Fila(t, "P1");
        (Celda(t, p1, "Costo de venta del período"), Celda(t, p1, "Inventario promedio"), Celda(t, p1, "Rotación"), Celda(t, p1, "Días de inventario"))
            .Should().Be((13000m, 43500m, 0.30m, 83.65m));
        // P3: saldos 0 y 288.000 → 144.000; costo 12.000 → 0,08 veces; 25 × 144.000 ÷ 12.000 = 300 días.
        var p3 = Fila(t, "P3");
        (Celda(t, p3, "Costo de venta del período"), Celda(t, p3, "Inventario promedio"), Celda(t, p3, "Rotación"), Celda(t, p3, "Días de inventario"))
            .Should().Be((12000m, 144000m, 0.08m, 300.00m));
    }

    [Fact]
    public async Task La_rotacion_se_abre_por_categoria_y_por_grupo_contable_y_exige_costos()
    {
        var v = await ConVentasAsync();
        var handler = new TurnoverReportQueryHandler(v.Db, v.K.Alcance, v.K.Permisos, Analitica(v), v.Compras.C.Reloj);

        var categoria = (await handler.Handle(new TurnoverReportQuery(Septiembre(), "category"), default)).Value;
        var grupo = (await handler.Handle(new TurnoverReportQuery(Septiembre(), "accountingGroup"), default)).Value;

        // 25.000 sobre (43.500 + 144.000) = 187.500 → 0,13 veces; 25 × 187.500 ÷ 25.000 = 187,5 días.
        var fila = categoria.Filas.Should().ContainSingle().Which;
        fila.Valores.Skip(1).Take(4).Should().Equal(25000m, 187500m, 0.13m, 187.50m);
        grupo.Filas.Sum(f => (decimal)f.Valores[1]!).Should().Be(25000m);

        v.K.Permisos.HasPermissionAsync("Inventory.Costs.Read", Arg.Any<CancellationToken>()).Returns(false);
        (await handler.Handle(new TurnoverReportQuery(Septiembre(), "product"), default)).Error.Should().Be(Error.NotFound);
        TurnoverReportQueryHandler.Vista.RequiredPermission.Should().Be("Inventory.Costs.Read");
    }

    // --------------------------------------------------------------------------------------------------- abc --

    private static Task<Result<TablaExportable>> AbcAsync(VentasDePrueba v, string? basis) =>
        new AbcReportQueryHandler(v.K.Alcance, v.K.Permisos, Analitica(v), v.K.Lector(), v.Compras.C.Reloj)
            .Handle(new AbcReportQuery(Septiembre(), basis), default);

    [Fact]
    public async Task El_abc_por_ventas_clasifica_con_los_umbrales_vigentes()
    {
        var v = await ConVentasAsync();

        var t = (await AbcAsync(v, "sales")).Value;

        t.Columnas.Where(c => !c.EsOculta).Select(c => c.Nombre).Should().Equal("Producto", "Valor", "Participación (%)", "Acumulado (%)", "Clase");
        t.Columnas.Where(c => c.EsOculta).Select(c => c.Clave).Should().Equal("_producto");
        t.Filas.Select(f => ((string)f.Valores[0]!)[..2]).Should().Equal("P1", "P3");
        // 26.000 y 20.000 de 46.000: 56,52 % (A, el primero siempre) y 100 % (C con 80/15/5).
        t.Filas[0].Valores.Skip(1).Take(4).Should().Equal(26000m, 56.52m, 56.52m, "A");
        t.Filas[1].Valores.Skip(1).Take(4).Should().Equal(20000m, 43.48m, 100.00m, "C");

        v.Parametro(ParametrosDeInventario.InformesUmbralesAbc, "50/50/0");
        var conOtros = (await AbcAsync(v, "sales")).Value;
        conOtros.Filas[1].Valores[4].Should().Be("B", "con 50/50/0, P3 cierra el 100 % dentro de A + B");
    }

    [Fact]
    public async Task El_abc_por_consumo_mide_las_salidas_al_costo()
    {
        var v = await ConVentasAsync();

        var t = (await AbcAsync(v, "consumption")).Value;

        // Salidas al costo: P1 15.000 (10 + 5 a 1.000; la devolución es entrada) y P3 12.000.
        t.Filas.Select(f => (((string)f.Valores[0]!)[..2], f.Valores[1], f.Valores[4])).Should().Equal(("P1", 15000m, "A"), ("P3", 12000m, "C"));
    }

    [Fact]
    public async Task Una_base_desconocida_es_FilterInvalid()
    {
        var v = await ConVentasAsync();

        (await AbcAsync(v, "suerte")).Error.Code.Should().Be(AnaliticaDeInventario.FiltroInvalidoCodigo);
    }

    // ----------------------------------------------------------------------------------- no-movement y expiring --

    /// <summary>P2, 4 u a 500 en B2 desde el 1 de mayo: 147 días quieto al 25 de septiembre.</summary>
    private static async Task QuietoDesdeMayoAsync(VentasDePrueba v)
    {
        var (_, r) = await v.K.AjusteAsync(v.K.Borrador("AJP", v.K.Segunda, fecha: new DateOnly(2026, 5, 1), lineas: [v.K.Linea(v.K.P2, 4m, 500m)]));
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
    }

    private static Task<Result<TablaExportable>> SinMovimientoAsync(VentasDePrueba v, int? dias) =>
        new NoMovementReportQueryHandler(v.K.Alcance, v.K.Permisos, Analitica(v), v.K.Lector(), v.Compras.C.Reloj)
            .Handle(new NoMovementReportQuery(new FiltrosDeInformeDeInventario(), dias), default);

    [Fact]
    public async Task Sin_movimiento_usa_Informes_DiasSinMovimiento_por_defecto_y_days_si_viene()
    {
        var v = await ConVentasAsync();
        await QuietoDesdeMayoAsync(v);

        var porDefecto = (await SinMovimientoAsync(v, null)).Value;
        var con200 = (await SinMovimientoAsync(v, 200)).Value;

        porDefecto.Columnas.Where(c => !c.EsOculta).Select(c => c.Nombre).Should().Equal(
            "Producto", "Bodega", "Último movimiento", "Días sin movimiento", "Cantidad", "Valor");
        porDefecto.Columnas.Where(c => c.EsOculta).Select(c => c.Clave).Should().Equal("_producto", "_bodega");
        var fila = porDefecto.Filas.Should().ContainSingle("con 90 días sólo P2 en B2 está quieto; P1 y P3 se movieron hoy").Which;
        fila.Valores.Take(6).Should().Equal($"P2 · Frijol", "B2", new DateOnly(2026, 5, 1), 147, 4m, 2000m);
        con200.Filas.Should().BeEmpty("147 días no llegan a 200");
    }

    [Fact]
    public async Task Sin_movimiento_sin_permiso_de_costos_deja_el_valor_vacio()
    {
        var v = await ConVentasAsync();
        await QuietoDesdeMayoAsync(v);
        v.K.Permisos.HasPermissionAsync("Inventory.Costs.Read", Arg.Any<CancellationToken>()).Returns(false);

        var t = (await SinMovimientoAsync(v, null)).Value;

        Celda(t, t.Filas.Single(), "Valor").Should().BeNull();
        t.Notas.Should().Contain(n => n.Contains("Inventory.Costs.Read"));
    }

    [Fact]
    public async Task El_deterioro_suma_el_motivo_sin_movimiento()
    {
        var v = await ConVentasAsync();
        await QuietoDesdeMayoAsync(v);

        var t = (await new ImpairmentReportQueryHandler(v.Db, v.K.Alcance, v.K.Permisos, new ValorizadoALaFecha(v.Db, v.K.Lector()), v.K.Lector(),
                v.Compras.C.Reloj, Analitica(v))
            .Handle(new ImpairmentReportQuery(new FiltrosDeInformeDeInventario()), default)).Value;

        var quieto = t.Filas.Single(f => ((string)f.Valores[8]!).StartsWith(ImpairmentReportQueryHandler.MotivoSinMovimiento, StringComparison.Ordinal));
        quieto.Valores[0].Should().Be("P2 · Frijol");
        quieto.Valores[1].Should().Be("B2");
        ((string)quieto.Valores[8]!).Should().Contain("147 días").And.Contain("2026-05-01");
        quieto.Valores[7].Should().BeNull("sin movimiento es un aviso, no un valor de indicio");
    }

    [Fact]
    public async Task Por_vencer_usa_Informes_DiasProximoAVencer_por_defecto_y_days_si_viene()
    {
        var v = await VentasDePrueba.CrearAsync();
        v.K.Entrega = Domain.Common.Parametros.EntregaDelComercio.I6;
        var leche = (await v.Compras.C.ProductoAsync(v.Compras.C.Alta("LCH", "Leche") with { TracksLot = true, TracksExpiry = true })).PublicId;
        var (_, r) = await v.K.AjusteAsync(v.K.Borrador("AJP", lineas:
        [
            v.K.Linea(leche, 4m, 2_000m) with { LotCode = "VIEJO", ExpiryDate = Hoy.AddDays(-3) },
            v.K.Linea(leche, 6m, 2_000m) with { LotCode = "PRONTO", ExpiryDate = Hoy.AddDays(10) },
            v.K.Linea(leche, 5m, 2_000m) with { LotCode = "LEJOS", ExpiryDate = Hoy.AddDays(200) },
        ]));
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var handler = new ExpiringReportQueryHandler(v.K.Alcance, v.K.Permisos, Analitica(v), v.K.Lector(), v.Compras.C.Reloj);

        var porDefecto = (await handler.Handle(new ExpiringReportQuery(new FiltrosDeInformeDeInventario(), null), default)).Value;
        var conAnio = (await handler.Handle(new ExpiringReportQuery(new FiltrosDeInformeDeInventario(), 365), default)).Value;

        porDefecto.Columnas.Where(c => !c.EsOculta).Select(c => c.Nombre).Should().Equal("Producto", "Lote", "Vence", "Días", "Bodega", "Cantidad", "Valor");
        porDefecto.Columnas.Where(c => c.EsOculta).Select(c => c.Clave).Should().Equal("_producto", "_lote", "_bodega");
        porDefecto.Filas.Should().ContainSingle("con 30 días sólo PRONTO; VIEJO ya venció (es deterioro) y LEJOS falta mucho")
            .Which.Valores.Take(7).Should().Equal("LCH · Leche", "PRONTO", Hoy.AddDays(10), 10, "PRIN", 6m, 12000m);
        conAnio.Filas.Select(f => f.Valores[1]).Should().Equal("PRONTO", "LEJOS");
    }

    // ----------------------------------------------------------------------------------- purchase-suggestion --

    /// <summary>Compras: 3 docenas de P1 del proveedor B (36 a 1.300), una merma de 23 (quedan 13) y la política 10 / 50 / 15 de P1 y de P2 en PRIN.</summary>
    private static async Task<ComprasDePrueba> ConCompraYMermaAsync()
    {
        var c = await ComprasDePrueba.CrearAsync();
        await c.RecepcionDeTresDocenasAsync(c.ProveedorB);
        var (_, r) = await c.K.AjusteAsync(c.K.Borrador("AJN", causa: c.K.Causa(), lineas: [c.K.Linea(c.P1, 23m)]));
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        foreach (var p in new[] { c.P1, c.K.P2 })
            c.C.Db.ReorderPolicies.Add(new ReorderPolicy { ProductId = c.K.ProductoId(p), WarehouseId = c.K.Principal.Id, MinimumQuantity = 10m, MaximumQuantity = 50m, ReorderPoint = 15m });
        await c.C.Db.SaveChangesAsync();
        return c;
    }

    private static Task<Result<TablaExportable>> SugeridoAsync(ComprasDePrueba c, Guid? proveedor = null) =>
        new PurchaseSuggestionReportQueryHandler(c.C.Db, c.K.Alcance, c.K.Permisos,
                new IngenIA365ERP.Application.Inventory.Replenishment.EvaluacionDeReposicion(c.C.Db, new IngenIA365ERP.Application.Inventory.Replenishment.PosicionDeReposicion(c.C.Db)), Analitica(c))
            .Handle(new PurchaseSuggestionReportQuery(new FiltrosDeInformeDeInventario(), proveedor), default);

    [Fact]
    public async Task El_sugerido_de_compras_trae_el_37_el_ultimo_costo_y_el_proveedor_habitual()
    {
        var c = await ConCompraYMermaAsync();

        var t = (await SugeridoAsync(c)).Value;

        t.Columnas.Where(x => !x.EsOculta).Select(x => x.Nombre).Should().Equal(
            "Producto", "Bodega", "Posición", "Mínimo", "Punto de reorden", "Máximo", "Sugerido", "Último costo", "Proveedor habitual");
        t.Columnas.Where(x => x.EsOculta).Select(x => x.Clave).Should().Equal("_producto");
        var p1 = Fila(t, "P1");
        p1.Valores.Skip(1).Take(7).Should().Equal("PRIN", 13m, 10m, 15m, 50m, 37m, 1300m);
        ((string)Celda(t, p1, "Proveedor habitual")!).Should().Contain("Abarrotes La 14");
        var p2 = Fila(t, "P2");
        Celda(t, p2, "Sugerido").Should().Be(50m);
        Celda(t, p2, "Proveedor habitual").Should().BeNull("P2 nunca se recibió");
    }

    [Fact]
    public async Task El_sugerido_filtra_por_proveedor_y_sin_costos_no_muestra_el_ultimo_costo()
    {
        var c = await ConCompraYMermaAsync();

        var delB = (await SugeridoAsync(c, c.ProveedorB.PublicId)).Value;
        var delA = (await SugeridoAsync(c, c.ProveedorA.PublicId)).Value;
        c.K.Permisos.HasPermissionAsync("Inventory.Costs.Read", Arg.Any<CancellationToken>()).Returns(false);
        var sinCostos = (await SugeridoAsync(c)).Value;

        delB.Filas.Select(f => ((string)f.Valores[0]!)[..2]).Should().Equal("P1");
        delA.Filas.Should().BeEmpty();
        Celda(sinCostos, Fila(sinCostos, "P1"), "Último costo").Should().BeNull();
    }

    // --------------------------------------------------------------------------------------- shrinkage-cap --

    private static Task<Result<TablaExportable>> TopeAsync(ComprasDePrueba c) =>
        new ShrinkageCapReportQueryHandler(c.C.Db, c.K.Alcance, c.K.Permisos, Analitica(c), c.K.Lector(), c.C.Reloj)
            .Handle(new ShrinkageCapReportQuery(new FiltrosDeInformeDeInventario(), 2026), default);

    [Fact]
    public async Task Con_el_tope_en_cero_la_vista_dice_que_no_esta_parametrizado()
    {
        var c = await ConCompraYMermaAsync();

        var t = (await TopeAsync(c)).Value;

        t.Columnas.Select(x => x.Nombre).Should().Equal(
            "Inventario inicial", "Compras del año", "Base", "Tope legal (%)", "Tope", "Faltantes y mermas", "Exceso");
        // Compras 36 × 1.300 = 46.800; merma 23 × 1.300 = 29.900.
        t.Filas.Should().ContainSingle().Which.Valores.Should().Equal(0m, 46800m, 46800m, null, null, 29900m, null);
        t.Notas.Should().Contain(n => n.Contains(ShrinkageCapReportQueryHandler.TopeNoParametrizado));
    }

    [Fact]
    public async Task Con_el_tope_parametrizado_calcula_tope_y_exceso_con_su_norma()
    {
        var c = await ConCompraYMermaAsync();
        c.C.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeInventario.Modulo, Key = ParametrosDeInventario.InformesTopeFaltantesPorcentaje, ScopeKind = ParameterScopeKind.None,
            Value = "0.03", ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba", LegalSource = "Norma de prueba",
        });
        await c.C.Db.SaveChangesAsync();

        var t = (await TopeAsync(c)).Value;

        // Tope = 46.800 × 3 % = 1.404; exceso = 29.900 − 1.404 = 28.496.
        t.Filas.Single().Valores.Should().Equal(0m, 46800m, 46800m, 3.00m, 1404m, 29900m, 28496m);
        t.Notas.Should().Contain(n => n.Contains("Norma de prueba"));
        t.Notas.Should().Contain(n => n.Contains("MERMA"), "los faltantes se detallan por causa");
    }

    [Fact]
    public async Task El_tope_exige_costos()
    {
        var c = await ConCompraYMermaAsync();
        c.K.Permisos.HasPermissionAsync("Inventory.Costs.Read", Arg.Any<CancellationToken>()).Returns(false);

        (await TopeAsync(c)).Error.Should().Be(Error.NotFound);
        ShrinkageCapReportQueryHandler.Vista.RequiredPermission.Should().Be("Inventory.Costs.Read");
    }
}
