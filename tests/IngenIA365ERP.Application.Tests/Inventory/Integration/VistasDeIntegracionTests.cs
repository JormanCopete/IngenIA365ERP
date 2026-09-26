using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Inventory.Reports;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Integration;

/// <summary>
/// Feature 012, T524 y T525 (FR-080, FR-081, SC-005; contracts/api.md §27): las vistas <c>messages</c>, <c>accounting-batches</c> y
/// <c>reconciliation</c> del centro de informes de Inventario, leyendo sólo la bandeja y la consulta de saldos de Contabilidad.
/// </summary>
public class VistasDeIntegracionTests
{
    private static int Columna(TablaExportable t, string nombre) => t.Columnas.ToList().FindIndex(c => c.Nombre == nombre && !c.EsOculta);

    [Fact]
    public async Task La_vista_messages_trae_cada_entrega_con_su_estado_y_el_documento()
    {
        var k = await KardexDePrueba.CrearAsync();
        var documento = await k.EntradaAsync(k.P1, 10m, 1000m);

        var r = await new MessagesReportQueryHandler(k.C.Db, k.Alcance, k.C.Reloj).Handle(new MessagesReportQuery(new FiltrosDeInformeDeInventario()), default);

        r.IsSuccess.Should().BeTrue();
        var fila = r.Value.Filas.Should().ContainSingle().Which;
        fila.Valores[Columna(r.Value, "Tipo")].Should().Be("AjusteInventarioAprobado");
        fila.Valores[Columna(r.Value, "Estado")].Should().Be(DeliveryStatus.Pending.ToString());
        fila.Valores[Columna(r.Value, "Destino")].Should().Be(IntegrationDestinations.Accounting);
        fila.Valores[^1].Should().Be(documento.ToString(), "la oculta _documento abre el documento");

        var soloRechazados = await new MessagesReportQueryHandler(k.C.Db, k.Alcance, k.C.Reloj)
            .Handle(new MessagesReportQuery(new FiltrosDeInformeDeInventario(), DeliveryStatus.Rejected), default);
        soloRechazados.Value.Filas.Should().BeEmpty();
    }

    [Fact]
    public async Task La_vista_accounting_batches_lista_los_lotes()
    {
        var p = await Periods.PeriodosDePrueba.CrearAsync();
        var ajp = p.K.Tipo("AJP").Id;
        p.K.Parametro(ParametrosDeInventario.ContabilidadModoDePaso, "PorLotes", ParameterScopeKind.DocumentType, ajp);
        p.K.Parametro(ParametrosDeInventario.ContabilidadDisparadorDeLote, "CierreDePeriodo", ParameterScopeKind.DocumentType, ajp);
        await p.AjusteAsync("AJP", new DateOnly(2026, 7, 10), p.K.P1, 10m, 1000m);
        (await p.CerrarAsync(2026, 7)).IsSuccess.Should().BeTrue();
        p.K.C.Reloj.AhoraLocal.Returns(new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.FromHours(-5)));

        var r = await new AccountingBatchesReportQueryHandler(p.K.C.Db, p.K.Alcance, p.K.C.Reloj)
            .Handle(new AccountingBatchesReportQuery(new FiltrosDeInformeDeInventario()), default);

        var fila = r.Value.Filas.Should().ContainSingle().Which;
        fila.Valores[Columna(r.Value, "Disparador")].Should().Be(BatchTrigger.PeriodClose.ToString());
        fila.Valores[Columna(r.Value, "Mensajes")].Should().Be(1);
        fila.Valores[Columna(r.Value, "Tarde")].Should().Be("No");
    }

    [Fact]
    public async Task La_conciliacion_compara_el_valorizado_del_conjunto_con_el_saldo_y_explica_lo_pendiente()
    {
        var k = await KardexDePrueba.CrearAsync();
        await k.EntradaAsync(k.P1, 10m, 1000m);
        var contabilidad = Substitute.For<IContabilidadParaInventario>();
        contabilidad.SaldosDeCuentasMapeadasAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(Result.Success<IReadOnlyList<ConjuntoDeCuentasDto>>(
        [
            new ConjuntoDeCuentasDto(["ABARR"], [new ParGrupoBodegaDto("ABARR", "*")],
                [new CuentaDelConjuntoDto("14350501", "Mercancías", "Inventario", [])], 0m),
        ]));

        var r = await new ConciliacionReportQueryHandler(k.C.Db, k.Lector(), k.C.Reloj, contabilidad)
            .Handle(new ConciliacionReportQuery(new FiltrosDeInformeDeInventario()), default);

        r.IsSuccess.Should().BeTrue();
        var conjunto = r.Value.Filas.Should().ContainSingle(f => f.Seccion == ConciliacionReportQueryHandler.SeccionConjuntos).Which;
        conjunto.Valores[Columna(r.Value, "Valorizado total")].Should().Be(10_000m);
        conjunto.Valores[Columna(r.Value, "Saldo contable")].Should().Be(0m);
        conjunto.Valores[Columna(r.Value, "Diferencia")].Should().Be(10_000m);
        conjunto.Valores[Columna(r.Value, "Pendientes")].Should().Be(10_000m, "el mensaje del ajuste todavía no llega al libro");
        conjunto.Valores[Columna(r.Value, "Sin explicar")].Should().Be(0m);
        r.Value.Filas.Should().Contain(f => f.Seccion == ConciliacionReportQueryHandler.SeccionBodegas);
    }

    [Fact]
    public async Task Sin_respuesta_de_contabilidad_la_conciliacion_lo_dice()
    {
        var k = await KardexDePrueba.CrearAsync();

        var r = await new ConciliacionReportQueryHandler(k.C.Db, k.Lector(), k.C.Reloj)
            .Handle(new ConciliacionReportQuery(new FiltrosDeInformeDeInventario()), default);

        r.Value.Filas.Should().NotContain(f => f.Seccion == ConciliacionReportQueryHandler.SeccionConjuntos);
        r.Value.Notas.Should().Contain(n => n.Contains("no respondió"));
    }
}
