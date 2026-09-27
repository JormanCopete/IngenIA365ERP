using FluentAssertions;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Inventory.Cash;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Tests.Inventory.Sales;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;

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
