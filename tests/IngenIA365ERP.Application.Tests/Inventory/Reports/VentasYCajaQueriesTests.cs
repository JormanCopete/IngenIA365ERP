using FluentAssertions;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Inventory.Cash;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Tests.Inventory.Sales;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Core;

namespace IngenIA365ERP.Application.Tests.Inventory.Reports;

/// <summary>
/// Feature 012, T623 (contracts/api.md §27; decisiones-transversales §2.12): las vistas de ventas y caja sobre una venta conocida de
/// <see cref="VentasDePrueba"/> —2 arroces a 2.000 y un aceite a 10.000 + IVA 1.900 = 15.900, cobrada con 5.000 en efectivo, 6.900 con
/// tarjeta (aprobación 123456, últimos 4 4242) y 4.000 con el bono BN-9, en la sesión de la caja CJ1—.
/// </summary>
public class VentasYCajaQueriesTests
{
    private static int Columna(TablaExportable tabla, string titulo) =>
        tabla.Columnas.Select((c, i) => (c, i)).First(x => x.c.Nombre == titulo && x.c.Clave is null).i;

    private static object? Celda(TablaExportable tabla, FilaExportable fila, string titulo) => fila.Valores[Columna(tabla, titulo)];

    private static async Task<(VentasDePrueba V, InventoryDocument Venta)> VentaAsync()
    {
        var v = await VentasDePrueba.CrearAsync();
        var venta = await v.VentaConfirmadaAsync(total =>
        [
            v.Pago(v.Efectivo, 5000m),
            new DocumentPaymentInput(v.Tarjeta.PublicId, 6900m, AuthorizationCode: "123456", Last4: "4242", CashSessionPublicId: v.Sesion.PublicId),
            v.Pago(v.Bono, total - 11900m, "BN-9"),
        ], lineas: [v.Linea(v.P1, 2m), v.Linea(v.P3, 1m)]);
        return (v, venta);
    }

    private static SesionesDeCaja Sesiones(VentasDePrueba v) => new(v.Db, v.K.Actor, v.K.Alcance, v.K.Permisos);

    [Fact]
    public async Task Ventas_por_medio_de_pago_suma_cada_medio()
    {
        var (v, _) = await VentaAsync();

        var t = (await new SalesByPaymentMeansReportQueryHandler(v.Db, v.K.Alcance, v.Compras.C.Reloj)
            .Handle(new SalesByPaymentMeansReportQuery(new FiltrosDeInformeDeInventario()), default)).Value;

        t.Filas.Select(f => ((string)Celda(t, f, "Medio")!, (decimal)Celda(t, f, "Recibido")!)).Should().BeEquivalentTo(
            [("BONO · BONO", 4000m), ("EFECTIVO · EFECTIVO", 5000m), ("VISA · VISA", 6900m)]);
        t.Totales!.Valores[Columna(t, "Neto")].Should().Be(15900m);
    }

    [Fact]
    public async Task Ventas_por_medio_de_pago_admite_varias_clases()
    {
        var (v, _) = await VentaAsync();

        var t = (await new SalesByPaymentMeansReportQueryHandler(v.Db, v.K.Alcance, v.Compras.C.Reloj)
            .Handle(new SalesByPaymentMeansReportQuery(new FiltrosDeInformeDeInventario(),
                Classes: [PaymentMeansClass.Cash, PaymentMeansClass.CreditCard]), default)).Value;

        t.Filas.Select(f => (string)Celda(t, f, "Medio")!).Should().BeEquivalentTo(["EFECTIVO · EFECTIVO", "VISA · VISA"]);
        t.Totales!.Valores[Columna(t, "Neto")].Should().Be(11900m);
    }

    private static async Task<TablaExportable> DetalleAsync(VentasDePrueba v, IReadOnlyList<Guid>? medios = null,
        IReadOnlyList<PaymentMeansClass>? clases = null, bool todos = false) =>
        (await new SalesPaymentsDetailReportQueryHandler(v.Db, v.K.Alcance, v.Compras.C.Reloj)
            .Handle(new SalesPaymentsDetailReportQuery(new FiltrosDeInformeDeInventario(), medios, clases, todos), default)).Value;

    [Fact]
    public async Task El_detalle_trae_una_fila_por_medio_con_el_total_de_la_venta()
    {
        var (v, venta) = await VentaAsync();

        var t = await DetalleAsync(v);

        t.Filas.Should().HaveCount(3);
        t.Filas.Select(f => ((string)Celda(t, f, "Medio")!, (decimal)Celda(t, f, "Valor del medio")!)).Should().BeEquivalentTo(
            [("EFECTIVO · EFECTIVO", 5000m), ("VISA · VISA", 6900m), ("BONO · BONO", 4000m)]);
        t.Filas.Should().OnlyContain(f => (decimal)Celda(t, f, "Total del documento")! == 15900m);
        t.Filas.Should().OnlyContain(f => (string?)f.Valores[t.Columnas.Count - 1] == venta.PublicId.ToString(), "la fila lleva a la venta");
        t.Totales!.Valores[Columna(t, "Total del documento")].Should().Be(15900m, "el total de la venta se suma una vez");
        t.Totales.Valores[Columna(t, "Valor del medio")].Should().Be(15900m);
        Celda(t, t.Filas[0], "Clase de medio").Should().Be("Efectivo");
        SalesPaymentsDetailReportQueryHandler.Vista.PersonalData.Should().BeTrue("lleva el cliente");
    }

    [Fact]
    public async Task El_detalle_filtra_por_varios_medios_y_puede_mostrar_todos_los_de_la_venta()
    {
        var (v, _) = await VentaAsync();

        var soloElegidos = await DetalleAsync(v, medios: [v.Efectivo.PublicId, v.Bono.PublicId]);
        soloElegidos.Filas.Select(f => (string)Celda(soloElegidos, f, "Medio")!).Should().BeEquivalentTo(["EFECTIVO · EFECTIVO", "BONO · BONO"]);
        soloElegidos.Notas.Should().Contain(n => n.Contains("Todos los medios de la venta"));

        var todos = await DetalleAsync(v, medios: [v.Efectivo.PublicId], todos: true);
        todos.Filas.Should().HaveCount(3, "la venta que usó efectivo muestra también la tarjeta y el bono");

        var porClase = await DetalleAsync(v, clases: [PaymentMeansClass.CreditCard]);
        porClase.Filas.Should().ContainSingle().Which.Valores[Columna(porClase, "Valor del medio")].Should().Be(6900m);

        var inexistente = await DetalleAsync(v, medios: [Guid.NewGuid()]);
        inexistente.Filas.Should().BeEmpty("un medio que no existe no muestra todo");
    }

    [Fact]
    public async Task Pagos_con_tarjeta_lleva_la_aprobacion_y_los_ultimos_cuatro()
    {
        var (v, venta) = await VentaAsync();

        var t = (await new CardPaymentsReportQueryHandler(v.Db, v.K.Alcance, v.Compras.C.Reloj)
            .Handle(new CardPaymentsReportQuery(new FiltrosDeInformeDeInventario()), default)).Value;

        var fila = t.Filas.Should().ContainSingle().Which;
        Celda(t, fila, "Últimos 4").Should().Be("4242");
        Celda(t, fila, "Aprobación").Should().Be("123456");
        Celda(t, fila, "Valor").Should().Be(6900m);
        fila.Valores[^1].Should().Be(venta.PublicId.ToString(), "la columna oculta _documento");
        CardPaymentsReportQueryHandler.Vista.PersonalData.Should().BeTrue();
    }

    [Fact]
    public async Task Ventas_por_sesion_cuenta_la_venta_de_oficina_cobrada_en_la_sesion()
    {
        var (v, _) = await VentaAsync();

        var t = (await new SalesBySessionReportQueryHandler(v.Db, v.K.Alcance, Sesiones(v), v.Compras.C.Reloj)
            .Handle(new SalesBySessionReportQuery(new FiltrosDeInformeDeInventario()), default)).Value;

        var fila = t.Filas.Should().ContainSingle().Which;
        Celda(t, fila, "Caja").Should().Be("CJ1");
        Celda(t, fila, "Documentos").Should().Be(1);
        Celda(t, fila, "Ventas brutas").Should().Be(14000m);
        Celda(t, fila, "Impuestos").Should().Be(1900m);
        Celda(t, fila, "Neto").Should().Be(15900m);
        SalesBySessionReportQueryHandler.Vista.PersonalDataWhen.Should().Be("groupBy=customer");
    }

    [Fact]
    public async Task Bonos_redimidos_lista_el_numero_y_el_valor()
    {
        var (v, _) = await VentaAsync();

        var t = (await new VoucherRedemptionsReportQueryHandler(v.Db, v.K.Alcance, v.Compras.C.Reloj)
            .Handle(new VoucherRedemptionsReportQuery(new FiltrosDeInformeDeInventario()), default)).Value;

        var fila = t.Filas.Should().ContainSingle().Which;
        Celda(t, fila, "Número de bono").Should().Be("BN-9");
        Celda(t, fila, "Valor").Should().Be(4000m);
        Celda(t, fila, "Estado").Should().Be("Redimido");
    }

    [Fact]
    public async Task La_sesion_de_caja_muestra_el_esperado_por_medio_y_exige_la_sesion()
    {
        var (v, _) = await VentaAsync();
        var handler = new CashSessionReportQueryHandler(v.Db, v.K.Alcance, Sesiones(v));

        var t = (await handler.Handle(new CashSessionReportQuery(new FiltrosDeInformeDeInventario { Session = v.Sesion.PublicId }), default)).Value;
        var sinSesion = await handler.Handle(new CashSessionReportQuery(new FiltrosDeInformeDeInventario()), default);

        var efectivo = t.Filas.Single(f => f.Seccion == CashSessionReportQueryHandler.SeccionPorMedio && ((string)f.Valores[0]!).StartsWith("EFECTIVO"));
        Celda(t, efectivo, "Ventas").Should().Be(5000m);
        Celda(t, efectivo, "Esperado").Should().Be(5000m);
        efectivo.Valores[^1].Should().Be(v.Sesion.PublicId.ToString(), "la columna oculta _sesion");
        sinSesion.IsFailure.Should().BeTrue();
    }
}
