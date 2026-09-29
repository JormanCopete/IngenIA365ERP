using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Inventory.Periods;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Application.Inventory.Reports.Vistas;
using IngenIA365ERP.Application.Tests.Inventory.Sales;
using IngenIA365ERP.Domain.Inventory.Parameters;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Reports;

/// <summary>
/// Feature 012, T559 (T624; FR-021; contracts/api.md §27 fila <c>impairment</c>; decisiones-transversales §2.12): los indicios de
/// deterioro con un juego conocido —la cooperativa de <see cref="VentasDePrueba"/>: P1 (arroz) 100 u a 1.000 con precio general 2.000
/// y P3 (aceite) 50 u a 6.000 con precio general 10.000—. Con gastos de venta del 50 %, el VNR del aceite es 5.000 y el del arroz 1.000:
/// sólo el aceite sale, con indicio (6.000 − 5.000) × 50 = 50.000; un producto sin precio en la lista general dice qué precio falta; sin
/// <c>Inventory.Costs.Read</c> es el 404 genérico; y el alcance por bodega decide qué filas se ven.
/// </summary>
public class ImpairmentReportQueryTests
{
    private static int Columna(TablaExportable tabla, string titulo) =>
        tabla.Columnas.Select((c, i) => (c, i)).First(x => x.c.Nombre == titulo && x.c.Clave is null).i;

    private static object? Celda(TablaExportable tabla, FilaExportable fila, string titulo) => fila.Valores[Columna(tabla, titulo)];

    private static Task<Result<TablaExportable>> ConsultarAsync(VentasDePrueba v, FiltrosDeInformeDeInventario? filtros = null) =>
        new ImpairmentReportQueryHandler(v.Db, v.K.Alcance, v.K.Permisos, new ValorizadoALaFecha(v.Db, v.K.Lector()), v.K.Lector(), v.Compras.C.Reloj,
                new AnaliticaDeInventario(v.Db, new ValorizadoALaFecha(v.Db, v.K.Lector())))
            .Handle(new ImpairmentReportQuery(filtros ?? new FiltrosDeInformeDeInventario()), default);

    [Fact]
    public async Task Solo_sale_lo_que_cuesta_mas_que_su_valor_neto_realizable()
    {
        var v = await VentasDePrueba.CrearAsync();
        v.Parametro(ParametrosDeInventario.InformesDeterioroPorcentajeGastosVenta, "0.5");

        var t = (await ConsultarAsync(v)).Value;

        var fila = t.Filas.Should().ContainSingle().Which;
        ((string)Celda(t, fila, "Producto")!).Should().StartWith("P3");
        Celda(t, fila, "Bodega").Should().Be("PRIN");
        Celda(t, fila, "Cantidad").Should().Be(50m);
        Celda(t, fila, "Costo unitario").Should().Be(6000m);
        Celda(t, fila, "Precio de la lista general").Should().Be(10000m);
        Celda(t, fila, "Gastos de venta").Should().Be(5000m);
        Celda(t, fila, "Valor neto realizable").Should().Be(5000m);
        Celda(t, fila, "Indicio (valor)").Should().Be(50000m);
        Celda(t, fila, "Motivo").Should().Be(ImpairmentReportQueryHandler.MotivoCostoSobreVnr);
        t.Totales!.Valores[Columna(t, "Indicio (valor)")].Should().Be(50000m);
    }

    [Fact]
    public async Task Los_lotes_vencidos_y_proximos_a_vencer_salen_por_lote_con_su_motivo()
    {
        // I6, T933 (FR-026; api.md §27 fila impairment): vencido = todo su valor al costo; próximo a vencer = aviso sin valor.
        var v = await VentasDePrueba.CrearAsync();
        v.K.Entrega = Domain.Common.Parametros.EntregaDelComercio.I6;
        var leche = (await v.Compras.C.ProductoAsync(v.Compras.C.Alta("LCH", "Leche") with { TracksLot = true, TracksExpiry = true })).PublicId;
        var hoy = Catalog.CatalogoDePrueba.Hoy;
        var (_, r) = await v.K.AjusteAsync(v.K.Borrador("AJP", lineas:
        [
            v.K.Linea(leche, 4m, 2_000m) with { LotCode = "VIEJO", ExpiryDate = hoy.AddDays(-3) },
            v.K.Linea(leche, 6m, 2_000m) with { LotCode = "PRONTO", ExpiryDate = hoy.AddDays(10) },
            v.K.Linea(leche, 5m, 2_000m) with { LotCode = "LEJOS", ExpiryDate = hoy.AddDays(200) },
        ]));
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);

        var t = (await ConsultarAsync(v)).Value;

        var deLotes = t.Filas.Where(f => ((string)Celda(t, f, "Motivo")!).Contains("lote ")).ToList();
        deLotes.Select(f => ((string)Celda(t, f, "Motivo")!, Celda(t, f, "Cantidad"), Celda(t, f, "Indicio (valor)"))).Should().BeEquivalentTo(new (string, object?, object?)[]
        {
            ($"{ImpairmentReportQueryHandler.MotivoVencido}: lote VIEJO venció el {hoy.AddDays(-3):yyyy-MM-dd}", 4m, 8_000m),
            ($"{ImpairmentReportQueryHandler.MotivoProximoAVencer}: lote PRONTO vence el {hoy.AddDays(10):yyyy-MM-dd}", 6m, null),
        }, "el lote que vence en 200 días no es indicio (Informes.DiasProximoAVencer = 30)");
    }

    [Fact]
    public async Task Sin_gastos_de_venta_nada_cuesta_mas_de_lo_que_se_recupera()
    {
        var v = await VentasDePrueba.CrearAsync();

        (await ConsultarAsync(v)).Value.Filas.Should().BeEmpty("el defecto de Informes.DeterioroPorcentajeGastosVenta es 0");
    }

    [Fact]
    public async Task Sin_precio_en_la_lista_general_dice_que_precio_falta()
    {
        var v = await VentasDePrueba.CrearAsync();
        await v.K.EntradaAsync(v.K.P2, 10m, 500m);

        var t = (await ConsultarAsync(v)).Value;

        var fila = t.Filas.Should().ContainSingle().Which;
        ((string)Celda(t, fila, "Producto")!).Should().StartWith("P2");
        ((string)Celda(t, fila, "Motivo")!).Should().Contain("Falta el precio de P2 en UND en la lista general");
        Celda(t, fila, "Valor neto realizable").Should().BeNull();
    }

    [Fact]
    public async Task Sin_permiso_de_costos_la_vista_es_404()
    {
        var v = await VentasDePrueba.CrearAsync();
        v.K.Permisos.HasPermissionAsync("Inventory.Costs.Read", Arg.Any<CancellationToken>()).Returns(false);

        var r = await ConsultarAsync(v);

        r.IsFailure.Should().BeTrue();
        r.Error.Should().Be(Error.NotFound);
        ImpairmentReportQueryHandler.Vista.RequiredPermission.Should().Be("Inventory.Costs.Read");
    }

    [Fact]
    public async Task Respeta_el_alcance_por_bodega()
    {
        var v = await VentasDePrueba.CrearAsync();
        v.Parametro(ParametrosDeInventario.InformesDeterioroPorcentajeGastosVenta, "0.5");
        v.K.Alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(
            new AlcanceDeInventario(false, new HashSet<int> { v.K.Segunda.Id }, null, true, new HashSet<int>(), null));

        var fuera = (await ConsultarAsync(v)).Value;
        var filtroFuera = await ConsultarAsync(v, new FiltrosDeInformeDeInventario { Warehouse = v.K.Principal.PublicId });

        fuera.Filas.Should().BeEmpty("la existencia está en PRIN, fuera del alcance");
        filtroFuera.IsFailure.Should().BeTrue("una bodega fuera del alcance es el 404 de la bodega");
    }
}
