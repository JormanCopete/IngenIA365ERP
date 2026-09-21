using FluentAssertions;
using IngenIA365ERP.Application.Payroll.Runs.ApprovePayrollRun;
using IngenIA365ERP.Application.Payroll.Runs.CalculatePayrollRun;
using IngenIA365ERP.Application.Payroll.Runs.Queries;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Tests.Payroll.Runs;

/// <summary>T096 — US4: comparativo contra el período anterior aprobado y verificaciones de cuadre.</summary>
public class ComparisonAndBalanceQueriesTests
{
    private static async Task<Guid> Calcular(NominaTestData d, Domain.Entities.Payroll.PayPeriod periodo)
    {
        var h = new CalculatePayrollRunCommandHandler(d.Db, d.Loader, d.Recurrentes, d.Lock, d.Clock, d.User, NullLogger<CalculatePayrollRunCommandHandler>.Instance);
        var r = await h.Handle(new CalculatePayrollRunCommand(periodo.PublicId), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.Error.Message);
        return r.Value.RunPublicId;
    }

    private static async Task Aprobar(NominaTestData d, Guid runId)
    {
        var contadora = NominaTestData.UsuarioDePrueba("contadora@demo", 9);
        var h = new ApprovePayrollRunCommandHandler(d.Db, d.Contabilizador(contadora), d.Policies, d.Permissions, d.Clock, contadora,
            new PayrollAuditEmitter(d.Audit, contadora, d.Clock, NullLogger<PayrollAuditEmitter>.Instance), d.StaleMarker);
        var r = await h.Handle(new ApprovePayrollRunCommand(runId, true), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.Error.Message);
    }

    [Fact]
    public async Task El_comparativo_marca_variaciones_sobre_el_umbral_nuevos_y_retirados()
    {
        var d = new NominaTestData();
        d.ConfigurarContabilidad();
        var beto = d.Empleado("Beto", 1_750_905m, new DateTime(2024, 1, 1));

        // Febrero aprobado con Ana y Beto.
        var febrero = d.Periodo(new DateTime(2026, 2, 1), new DateTime(2026, 2, 28), PayPeriodStatus.Open, planilla: 99);
        d.PeriodoContable(2026, 2);
        d.Db.SaveChanges();
        await Aprobar(d, await Calcular(d, febrero));

        // Marzo: Beto se retiró antes, Ana ganó más (aumento), Carla es nueva.
        beto.TerminationDate = new DateTime(2026, 2, 28); beto.Status = -1;
        d.Ana.Salary = 2_600_000m;
        var carla = d.Empleado("Carla", 3_000_000m, new DateTime(2026, 3, 1));
        d.Db.SaveChanges();
        var runMarzo = await Calcular(d, d.Marzo);

        var r = await new GetRunComparisonQueryHandler(d.Db, d.Policies).Handle(new GetRunComparisonQuery(runMarzo), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.PreviousPeriodPublicId.Should().Be(febrero.PublicId);
        r.Value.ThresholdPercent.Should().Be(10m);
        var ana = r.Value.Rows.Single(x => x.EmployeePublicId == d.Ana.PublicId);
        ana.PreviousNet.Should().Be(2_089_095m);
        ana.VariationPercent.Should().BeGreaterThan(10m);
        ana.OverThreshold.Should().BeTrue();
        r.Value.Rows.Single(x => x.EmployeePublicId == carla.PublicId).NewEmployee.Should().BeTrue();
        r.Value.Rows.Single(x => x.EmployeePublicId == beto.PublicId).LeftEmployee.Should().BeTrue();
    }

    [Fact]
    public async Task Sin_periodo_anterior_aprobado_el_comparativo_lo_dice_y_no_falla()
    {
        var d = new NominaTestData();
        var runId = await Calcular(d, d.Marzo);

        var r = await new GetRunComparisonQueryHandler(d.Db, d.Policies).Handle(new GetRunComparisonQuery(runId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        r.Value.PreviousPeriodPublicId.Should().BeNull();
        r.Value.Rows.Should().ContainSingle().Which.PreviousNet.Should().BeNull();
    }

    [Fact]
    public async Task El_cuadre_verifica_neto_totales_y_comprobante()
    {
        var d = new NominaTestData();
        d.ConfigurarContabilidad();
        var runId = await Calcular(d, d.Marzo);

        var antes = await new GetRunBalanceCheckQueryHandler(d.Db, d.SaldosDeProvision).Handle(new GetRunBalanceCheckQuery(runId), CancellationToken.None);
        antes.Value.EarningsMinusDeductionsEqualsNet.Should().BeTrue();
        antes.Value.EmployerAndProvisionsOutsideNet.Should().BeTrue();
        antes.Value.LinesMatchEmployeeTotals.Should().BeTrue();
        antes.Value.AccountingDocumentBalanced.Should().BeNull("todavía no hay comprobante");

        await Aprobar(d, runId);
        var despues = await new GetRunBalanceCheckQueryHandler(d.Db, d.SaldosDeProvision).Handle(new GetRunBalanceCheckQuery(runId), CancellationToken.None);
        despues.Value.AccountingDocumentBalanced.Should().BeTrue();
        despues.Value.Details.Should().Contain(x => x.Contains("NM-"));
    }

    [Fact]
    public async Task La_exportacion_lleva_una_fila_por_linea_con_su_explicacion()
    {
        var d = new NominaTestData();
        var runId = await Calcular(d, d.Marzo);

        var r = await new ExportRunQueryHandler(d.Db, d.AuditEmitter).Handle(new ExportRunQuery(runId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.FileName.Should().EndWith("_v1.csv");
        var texto = System.Text.Encoding.UTF8.GetString(r.Value.Content);
        texto.Should().Contain("Empleado;Documento;Días;Concepto").And.Contain("SALARIO").And.Contain("[Salario por tramos]");
        texto.Split('\n').Count(l => l.Contains("Ana Prueba")).Should().Be(d.Db.PayrollRunLines.Count());
    }
}
