using FluentAssertions;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Payroll.OpeningBalances;
using IngenIA365ERP.Application.Payroll.Reports;
using IngenIA365ERP.Application.Payroll.Settlements.Settlement;
using IngenIA365ERP.Application.Payroll.Terminations;
using IngenIA365ERP.Application.Tests.Payroll.Settlements.Settlement;
using IngenIA365ERP.Domain.Enums.Payroll;
using MediatR;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Reports;

/// <summary>
/// Feature 010, US3 (T071): las vistas <c>terminaciones</c> y <c>saldos-iniciales-prestaciones</c> del
/// centro de reportes salen como <see cref="TablaExportable"/> con los mismos números que las
/// pantallas: la lista de terminaciones y el listado de saldos iniciales.
/// </summary>
public class ReportesDeTerminacionesTests
{
    private static ISender SenderReal(DefinitivaDePrueba p)
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<ListTerminationsQuery>(), Arg.Any<CancellationToken>())
            .Returns(ci => new ListTerminationsQueryHandler(p.D.Db).Handle(ci.Arg<ListTerminationsQuery>(), ci.Arg<CancellationToken>()));
        sender.Send(Arg.Any<ListBenefitBalancesQuery>(), Arg.Any<CancellationToken>())
            .Returns(ci => new ListBenefitBalancesQueryHandler(p.D.Db, p.D.Policies, p.D.Clock).Handle(ci.Arg<ListBenefitBalancesQuery>(), ci.Arg<CancellationToken>()));
        return sender;
    }

    [Fact]
    public async Task Terminaciones_entre_fechas_con_motivo_indemnizacion_neto_y_estado()
    {
        var p = new DefinitivaDePrueba();
        p.ConContabilidad();
        var t = await p.RegistrarAnaAsync();
        (await p.Aprobar().Handle(new ApproveSettlementCommand(t.RunPublicId, true), CancellationToken.None)).IsSuccess.Should().BeTrue();
        var indemnizacion = t.Lines.Single(l => l.ConceptCode == "INDEMNIZACION").Amount;

        var r = await new TerminacionesReportQueryHandler(p.D.Db, SenderReal(p)).Handle(new TerminacionesReportQuery(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var tabla = r.Value;
        tabla.Titulo.Should().Be("Terminaciones de contrato");
        tabla.Columnas.Select(c => c.Nombre).Should().ContainInOrder("Empleado", "Documento", "Retiro", "Motivo", "Genera indemnización", "Contrato", "Indemnización", "Neto", "Estado");
        var fila = tabla.Filas.Should().ContainSingle().Subject;
        fila.Valores[0].Should().Be("Ana Prueba");
        fila.Valores[3].Should().Be("Despido sin justa causa");
        fila.Valores[4].Should().Be("Sí");
        fila.Valores[5].Should().Be("Término fijo");
        fila.Valores[6].Should().Be(indemnizacion);
        fila.Valores[7].Should().Be(t.Totals.Net);
        fila.Valores[8].Should().Be("Liquidada");
        fila.Seccion.Should().Be("Liquidada");
        tabla.Totales!.Valores[7].Should().Be(t.Totals.Net);
        tabla.Notas.Should().Contain(n => n.Contains("1 liquidada(s)"));

        var fuera = await new TerminacionesReportQueryHandler(p.D.Db, SenderReal(p)).Handle(new TerminacionesReportQuery(new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31)), CancellationToken.None);
        fuera.Value.Filas.Should().BeEmpty("el retiro del 15 de septiembre no cae en el rango");
    }

    [Fact]
    public async Task Saldos_iniciales_por_empleado_con_los_faltantes_marcados()
    {
        var p = new DefinitivaDePrueba(conCartera: false);
        p.D.Politica(Domain.Payroll.Policies.CompanyPolicyKeys.ArranqueNominaFecha, "2026-01-01", new DateOnly(2026, 1, 1));
        p.D.SaldoInicial(p.D.Ana, new DateOnly(2025, 12, 31), prima: 1_000_000m, cesantias: 1_900_000m, intereses: 228_000m, diasVacaciones: 14.25m);
        var sinSaldo = p.D.Empleado("Beto", 1_800_000m, new DateTime(2025, 6, 1));

        var r = await new SaldosInicialesPrestacionesReportQueryHandler(SenderReal(p)).Handle(new SaldosInicialesPrestacionesReportQuery(new DateOnly(2026, 9, 20)), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var tabla = r.Value;
        tabla.Titulo.Should().Be("Saldos iniciales de prestaciones");
        tabla.Filas.Should().HaveCount(2);
        var ana = tabla.Filas.Single(f => (string)f.Valores[0]! == "Ana Prueba");
        ana.Valores[3].Should().Be("Sí", "ingresó antes del arranque");
        ana.Valores[5].Should().Be(14.25m);
        ana.Valores[6].Should().Be(1_900_000m);
        ana.Valores[8].Should().Be(1_000_000m);
        ana.Valores[9].Should().Be("Editable");
        var beto = tabla.Filas.Single(f => (string)f.Valores[0]! == "Beto Prueba");
        beto.Valores[9].Should().Be("Falta");
        beto.Seccion.Should().Be("Sin saldo");
        tabla.Totales!.Valores[6].Should().Be(1_900_000m);
        tabla.Notas.Should().Contain(n => n.Contains("1 ingresaron antes del arranque y no tienen saldo"));
        _ = sinSaldo;
    }
}
