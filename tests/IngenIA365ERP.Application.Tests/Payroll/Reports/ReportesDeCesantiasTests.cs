using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Payroll.Reports;
using IngenIA365ERP.Application.Payroll.Settlements.Severance;
using IngenIA365ERP.Application.Tests.Payroll.Settlements.Severance;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Reports;

/// <summary>
/// Feature 010 US2 (T052; contracts/archivos.md §3.1): la vista <c>consignacion-cesantias</c> del
/// centro de reportes —un bloque (sección) por fondo con su total, total general igual al de la
/// relación, filtro por fondo, hoja por fondo en Excel, fecha límite en las notas y auditoría sólo al
/// exportar a archivo.
/// </summary>
public class ReportesDeCesantiasTests
{
    private static ConsignacionCesantiasReportQueryHandler Handler(EscenarioDeCesantias e)
    {
        var tenant = Substitute.For<IngenIA365ERP.Application.Common.Interfaces.ICurrentTenantService>();
        tenant.TenantName.Returns("Cooperativa Demo");
        return new(e.D.Db, tenant, EscenarioDeCesantias.Contadora, e.D.Clock, e.D.AuditEmitter);
    }

    private static int Columna(TablaExportable t, string nombre) => t.Columnas.ToList().FindIndex(c => c.Nombre == nombre);

    [Fact]
    public async Task Un_bloque_por_fondo_con_total_y_el_total_general_igual_al_de_la_relacion()
    {
        var e = new EscenarioDeCesantias();
        var calculo = await e.CalcularAsync();
        var relacion = (await e.Relacion().Handle(new GetDepositScheduleQuery(calculo.Value.RunPublicId), CancellationToken.None)).Value;

        var r = await Handler(e).Handle(new ConsignacionCesantiasReportQuery(calculo.Value.RunPublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var t = r.Value;
        t.Titulo.Should().Be("Cooperativa Demo · Consignación de cesantías año 2026");
        t.HojaPorSeccion.Should().BeTrue("en Excel cada fondo va en su propia hoja");
        t.Columnas.Select(c => c.Nombre).Should().Contain(["Documento", "Apellidos y nombres", "Fecha de ingreso", "Salario base de liquidación", "Días liquidados", "Cesantías a consignar", "Estado"]);

        var secciones = t.Filas.Select(f => f.Seccion).Distinct().ToList();
        secciones.Should().HaveCount(2);
        secciones[0].Should().StartWith("Porvenir").And.Contain($"NIT {e.Porvenir.TaxId}");
        secciones[1].Should().StartWith("Proteccion");

        var cesantias = Columna(t, "Cesantías a consignar");
        var detallePorvenir = t.Filas.Where(f => f.Seccion == secciones[0] && !f.Resaltada).ToList();
        detallePorvenir.Should().HaveCount(2, "Alba y Gema");
        var totalPorvenir = t.Filas.Single(f => f.Seccion == secciones[0] && f.Resaltada);
        totalPorvenir.Valores[cesantias].Should().Be(detallePorvenir.Sum(f => (decimal)f.Valores[cesantias]!));
        totalPorvenir.Valores[cesantias].Should().Be(2_749_095m + 666_666.67m);
        ((string)totalPorvenir.Valores[Columna(t, "Estado")]!).Should().Be("Pendiente");

        t.Totales.Should().NotBeNull();
        t.Totales!.Valores[cesantias].Should().Be(relacion.GrandTotal, "el total general es el de la relación, que es la CxP a los fondos");
        t.Notas.Should().Contain(n => n.Contains("14/02/2027") && n.Contains("CESANTIAS_FECHA_LIMITE_CONSIGNACION"));
        t.Notas.Should().Contain(n => n.Contains("31/01/2027"));
    }

    [Fact]
    public async Task Con_fundId_sale_solo_ese_fondo_y_tras_marcar_consignado_el_estado_lo_dice()
    {
        var e = new EscenarioDeCesantias(conContabilidad: true);
        var calculo = await e.CalcularAsync();
        (await e.AprobarAsync(calculo.Value.RunPublicId)).IsSuccess.Should().BeTrue();
        (await e.Marcador().Handle(new MarkFundDepositedCommand(calculo.Value.RunPublicId, e.FondoProteccion.PublicId, new DateOnly(2027, 1, 10), "REF-9"), CancellationToken.None)).IsSuccess.Should().BeTrue();

        var r = await Handler(e).Handle(new ConsignacionCesantiasReportQuery(calculo.Value.RunPublicId, e.FondoProteccion.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var t = r.Value;
        t.Filas.Select(f => f.Seccion).Distinct().Should().ContainSingle().Which.Should().StartWith("Proteccion");
        t.Filas.Where(f => !f.Resaltada).Should().ContainSingle().Which.Valores[Columna(t, "Apellidos y nombres")].Should().Be("Fabio Prueba");
        ((string)t.Filas.First().Valores[Columna(t, "Estado")]!).Should().Be("Consignado el 10-01-2027, ref. REF-9");
        t.Totales!.Valores[Columna(t, "Cesantías a consignar")].Should().Be(2_315_761.67m);

        var ajeno = await Handler(e).Handle(new ConsignacionCesantiasReportQuery(calculo.Value.RunPublicId, Guid.NewGuid()), CancellationToken.None);
        ajeno.Error.Code.Should().Be("Payroll.SeveranceFund.NotFound");
    }

    [Fact]
    public async Task Exportar_a_archivo_audita_y_el_json_de_la_pantalla_no()
    {
        var e = new EscenarioDeCesantias();
        var calculo = await e.CalcularAsync();
        e.D.Audit.ClearReceivedCalls();

        (await Handler(e).Handle(new ConsignacionCesantiasReportQuery(calculo.Value.RunPublicId, null, "json"), CancellationToken.None)).IsSuccess.Should().BeTrue();
        await e.D.Audit.DidNotReceive().AppendAsync(Arg.Is<AuditEventDocument>(d => d.Action == AuditEventTypes.PayrollRunExported), Arg.Any<CancellationToken>());

        (await Handler(e).Handle(new ConsignacionCesantiasReportQuery(calculo.Value.RunPublicId, null, "xlsx"), CancellationToken.None)).IsSuccess.Should().BeTrue();
        await e.D.Audit.Received(1).AppendAsync(
            Arg.Is<AuditEventDocument>(d => d.Action == AuditEventTypes.PayrollRunExported && d.NewValuesJson!.Contains("consignacion-cesantias") && d.NewValuesJson.Contains("\"xlsx\"")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Una_corrida_que_no_es_de_cesantias_responde_KindMismatch()
    {
        var e = new EscenarioDeCesantias();
        var ordinaria = e.D.Borrador(e.D.Marzo);

        var r = await Handler(e).Handle(new ConsignacionCesantiasReportQuery(ordinaria.PublicId), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.Settlement.KindMismatch");
        (await Handler(e).Handle(new ConsignacionCesantiasReportQuery(Guid.NewGuid()), CancellationToken.None)).Error.Code.Should().Be("Payroll.Run.NotFound");
    }
}
