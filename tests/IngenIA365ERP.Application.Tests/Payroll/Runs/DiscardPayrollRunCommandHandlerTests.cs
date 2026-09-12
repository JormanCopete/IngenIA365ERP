using FluentAssertions;
using IngenIA365ERP.Application.Payroll.Novelties.RecurringNovelties;
using IngenIA365ERP.Application.Payroll.Novelties.RegisterNovelty;
using IngenIA365ERP.Application.Payroll.Runs.ApprovePayrollRun;
using IngenIA365ERP.Application.Payroll.Runs.CalculatePayrollRun;
using IngenIA365ERP.Application.Payroll.Runs.DiscardPayrollRun;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Payroll.Runs;

/// <summary>
/// Feature 006 US1: un borrador con fechas equivocadas se descarta y el período vuelve a
/// Abierto sin aprobar ni reversar. Nada se borra; las recurrentes generadas se anulan para
/// no duplicarse en el próximo cálculo; las manuales quedan.
/// </summary>
public class DiscardPayrollRunCommandHandlerTests
{
    private static async Task<Guid> Calculada(NominaTestData d)
    {
        var c = await new CreateRecurringNoveltyCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker)
            .Handle(new CreateRecurringNoveltyCommand(d.Ana.PublicId, "LIBRANZA", null, 80_000m, new DateTime(2026, 1, 1), null, 6, null), CancellationToken.None);
        c.IsSuccess.Should().BeTrue(c.Error.Message);
        var horas = await new RegisterNoveltyCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker, d.CarryOver)
            .Handle(new RegisterNoveltyCommand { PeriodPublicId = d.Marzo.PublicId, EmployeePublicId = d.Ana.PublicId, ConceptCode = "HEX_NOCTURNA", Quantity = 4m }, CancellationToken.None);
        horas.IsSuccess.Should().BeTrue(horas.Error.Message);
        var calc = await new CalculatePayrollRunCommandHandler(d.Db, d.Loader, d.Recurrentes, d.Lock, d.Clock, d.User, NullLogger<CalculatePayrollRunCommandHandler>.Instance)
            .Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);
        calc.IsSuccess.Should().BeTrue(calc.Error.Message);
        return calc.Value.RunPublicId;
    }

    private static DiscardPayrollRunCommandHandler Descartador(NominaTestData d) =>
        new(d.Db, d.Lock, d.Clock, d.User, d.AuditEmitter);

    [Fact]
    public async Task Descartar_deja_la_corrida_marcada_el_periodo_abierto_anula_las_recurrentes_y_conserva_las_manuales()
    {
        var d = new NominaTestData();
        var runId = await Calculada(d);
        (await d.Db.PayPeriods.SingleAsync(p => p.Id == d.Marzo.Id)).Status.Should().Be(PayPeriodStatus.Calculated);

        var r = await Descartador(d).Handle(new DiscardPayrollRunCommand(runId, "Fechas equivocadas"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.RecurrentesAnuladas.Should().Be(1);
        var run = await d.Db.PayrollRuns.SingleAsync(x => x.PublicId == runId);
        run.Status.Should().Be(PayrollRunStatus.Superseded);
        run.DiscardReason.Should().Be("Fechas equivocadas");
        run.DiscardedBy.Should().NotBeNullOrEmpty();
        run.DiscardedAt.Should().NotBeNull();
        (await d.Db.PayrollRunLines.CountAsync()).Should().BeGreaterThan(0, "nada se borra");
        var periodo = await d.Db.PayPeriods.SingleAsync(p => p.Id == d.Marzo.Id);
        periodo.Status.Should().Be(PayPeriodStatus.Open);
        periodo.RunPublicId.Should().BeNull();
        var novedades = await d.Db.PayrollNovelties.Where(n => n.PayPeriodId == d.Marzo.Id).ToListAsync();
        novedades.Single(n => n.Origin == NoveltyOrigin.Recurring).Status.Should().Be(NoveltyStatus.Cancelled);
        novedades.Single(n => n.Origin == NoveltyOrigin.Manual).Status.Should().Be(NoveltyStatus.Active);
        (await d.Db.PayrollRecurringNovelties.SingleAsync()).InstallmentsIssued.Should().Be(0, "la cuota se cuenta al aprobar, nunca se contó");
    }

    [Fact]
    public async Task Tras_descartar_se_puede_recalcular_y_la_recurrente_vuelve_una_sola_vez()
    {
        var d = new NominaTestData();
        var runId = await Calculada(d);
        (await Descartador(d).Handle(new DiscardPayrollRunCommand(runId, "Corregir"), CancellationToken.None)).IsSuccess.Should().BeTrue();

        var calc = await new CalculatePayrollRunCommandHandler(d.Db, d.Loader, d.Recurrentes, d.Lock, d.Clock, d.User, NullLogger<CalculatePayrollRunCommandHandler>.Instance)
            .Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);

        calc.IsSuccess.Should().BeTrue(calc.Error.Message);
        calc.Value.Version.Should().Be(2);
        (await d.Db.PayrollNovelties.CountAsync(n => n.PayPeriodId == d.Marzo.Id && n.Origin == NoveltyOrigin.Recurring && n.Status == NoveltyStatus.Active))
            .Should().Be(1, "una activa; la anulada del borrador descartado no cuenta");
    }

    [Fact]
    public async Task Una_aprobada_no_se_descarta_se_reversa()
    {
        var d = new NominaTestData();
        d.ConfigurarContabilidad();
        var runId = await Calculada(d);
        var contadora = NominaTestData.UsuarioDePrueba("contadora@demo", 11);
        var apr = await new ApprovePayrollRunCommandHandler(d.Db, new PayrollAccountingPoster(d.Db, d.Clock, contadora), d.Policies, d.Permissions, d.Clock, contadora,
                new PayrollAuditEmitter(d.Audit, contadora, d.Clock, NullLogger<PayrollAuditEmitter>.Instance))
            .Handle(new ApprovePayrollRunCommand(runId, Confirm: true), CancellationToken.None);
        apr.IsSuccess.Should().BeTrue(apr.Error.Message);

        var r = await Descartador(d).Handle(new DiscardPayrollRunCommand(runId, "x"), CancellationToken.None);

        r.IsSuccess.Should().BeFalse();
        r.Error.Code.Should().Be("Payroll.RunNotDraft");
    }

    [Fact]
    public async Task Sin_motivo_no_se_descarta()
    {
        var d = new NominaTestData();
        var runId = await Calculada(d);

        var r = await Descartador(d).Handle(new DiscardPayrollRunCommand(runId, "  "), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.ReasonRequired");
        (await d.Db.PayPeriods.SingleAsync(p => p.Id == d.Marzo.Id)).Status.Should().Be(PayPeriodStatus.Calculated);
    }
}
