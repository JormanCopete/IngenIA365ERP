using FluentAssertions;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Payroll.Novelties.RecurringNovelties;
using IngenIA365ERP.Application.Payroll.Runs.ApprovePayrollRun;
using IngenIA365ERP.Application.Payroll.Runs.CalculatePayrollRun;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Tests.Payroll.Novelties;

/// <summary>T117 — FR-007: la recurrente entra en cada cálculo con su cuota, no duplica, y sólo cuenta la cuota al aprobar.</summary>
public class RecurringNoveltiesTests
{
    private static readonly ICurrentUserService Contadora = NominaTestData.UsuarioDePrueba("contadora@demo", 9);

    private static CalculatePayrollRunCommandHandler Calculador(NominaTestData d) =>
        new(d.Db, d.Loader, d.Recurrentes, d.Lock, d.Clock, d.User, NullLogger<CalculatePayrollRunCommandHandler>.Instance);

    private static async Task Aprobar(NominaTestData d, Guid runId)
    {
        var poster = d.Contabilizador(Contadora);
        var audit = new PayrollAuditEmitter(d.Audit, Contadora, CooperativaDePrueba.Actual, d.Clock, NullLogger<PayrollAuditEmitter>.Instance);
        var r = await new ApprovePayrollRunCommandHandler(d.Db, poster, d.Policies, d.Permissions, d.Clock, Contadora, audit)
            .Handle(new ApprovePayrollRunCommand(runId, Confirm: true), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.Error.Message);
    }

    private static async Task<Guid> Crear(NominaTestData d, Employee e, string concepto, decimal? cantidad, decimal? valor, DateTime desde, int? cuotas)
    {
        var h = new CreateRecurringNoveltyCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker);
        var r = await h.Handle(new CreateRecurringNoveltyCommand(e.PublicId, concepto, cantidad, valor, desde, null, cuotas, "Préstamo de la cooperativa"), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.Error.Message);
        return r.Value;
    }

    [Fact]
    public async Task Crear_valida_el_concepto_y_desactualiza_el_borrador_que_la_cubre()
    {
        var d = new NominaTestData();
        var run = d.Borrador(d.Marzo);
        var h = new CreateRecurringNoveltyCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker);

        (await h.Handle(new CreateRecurringNoveltyCommand(d.Ana.PublicId, "INCAP_GENERAL", null, 1m, new DateTime(2026, 3, 1), null, null, null), CancellationToken.None))
            .Error.Code.Should().Be("Payroll.ConceptNotApplicable", "por fechas no puede ser recurrente");
        (await h.Handle(new CreateRecurringNoveltyCommand(d.Ana.PublicId, "LIBRANZA", 1m, null, new DateTime(2026, 3, 1), null, null, null), CancellationToken.None))
            .Error.Code.Should().Be("Payroll.ConceptRequiresAmount");
        (await h.Handle(new CreateRecurringNoveltyCommand(d.Ana.PublicId, "NO_EXISTE", null, 1m, new DateTime(2026, 3, 1), null, null, null), CancellationToken.None))
            .Error.Code.Should().Be("Payroll.ConceptNotFound");

        var id = await Crear(d, d.Ana, "LIBRANZA", null, 120_000m, new DateTime(2026, 3, 1), 3);

        var r = await d.Db.PayrollRecurringNovelties.SingleAsync(x => x.PublicId == id);
        r.IsActive.Should().BeTrue();
        r.InstallmentsIssued.Should().Be(0);
        r.TotalInstallments.Should().Be(3);
        (await d.Db.PayrollRuns.SingleAsync(x => x.Id == run.Id)).Status.Should().Be(PayrollRunStatus.Stale);
    }

    [Fact]
    public async Task Cuotas_1_a_3_entran_en_tres_periodos_y_la_cuarta_no()
    {
        var d = new NominaTestData();
        d.ConfigurarContabilidad();
        d.PeriodoContable(2026, 4);
        d.PeriodoContable(2026, 5);
        d.PeriodoContable(2026, 6);
        await d.Db.SaveChangesAsync();
        var id = await Crear(d, d.Ana, "LIBRANZA", null, 120_000m, new DateTime(2026, 3, 1), 3);

        var periodos = new[]
        {
            d.Marzo,
            d.Periodo(new DateTime(2026, 4, 1), new DateTime(2026, 4, 30), PayPeriodStatus.Open),
            d.Periodo(new DateTime(2026, 5, 1), new DateTime(2026, 5, 31), PayPeriodStatus.Open),
            d.Periodo(new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), PayPeriodStatus.Open),
        };

        for (var i = 0; i < 3; i++)
        {
            var calc = await Calculador(d).Handle(new CalculatePayrollRunCommand(periodos[i].PublicId), CancellationToken.None);
            calc.IsSuccess.Should().BeTrue(calc.Error.Message);
            var novedad = await d.Db.PayrollNovelties.SingleAsync(n => n.PayPeriodId == periodos[i].Id && n.Origin == NoveltyOrigin.Recurring);
            novedad.InstallmentNumber.Should().Be(i + 1);
            novedad.InstallmentTotal.Should().Be(3);
            novedad.Amount.Should().Be(120_000m);
            (await d.Db.PayrollRunLines.AnyAsync(l => l.ConceptCode == "LIBRANZA" && l.NoveltyId == novedad.Id)).Should().BeTrue("la corrida la liquidó");
            (await d.Db.PayrollRecurringNovelties.SingleAsync(x => x.PublicId == id)).InstallmentsIssued.Should().Be(i, "la cuota se cuenta al aprobar, no al calcular");
            await Aprobar(d, calc.Value.RunPublicId);
            (await d.Db.PayrollRecurringNovelties.SingleAsync(x => x.PublicId == id)).InstallmentsIssued.Should().Be(i + 1);
        }

        var cuarto = await Calculador(d).Handle(new CalculatePayrollRunCommand(periodos[3].PublicId), CancellationToken.None);
        cuarto.IsSuccess.Should().BeTrue(cuarto.Error.Message);
        (await d.Db.PayrollNovelties.AnyAsync(n => n.PayPeriodId == periodos[3].Id && n.Origin == NoveltyOrigin.Recurring)).Should().BeFalse("ya emitió sus tres cuotas");
    }

    [Fact]
    public async Task Recalcular_no_duplica_la_novedad_recurrente()
    {
        var d = new NominaTestData();
        await Crear(d, d.Ana, "LIBRANZA", null, 50_000m, new DateTime(2026, 1, 1), null);

        (await Calculador(d).Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await Calculador(d).Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None)).IsSuccess.Should().BeTrue();

        var recurrentes = await d.Db.PayrollNovelties.Where(n => n.PayPeriodId == d.Marzo.Id && n.Origin == NoveltyOrigin.Recurring).ToListAsync();
        recurrentes.Should().ContainSingle().Which.InstallmentNumber.Should().Be(1);
        recurrentes[0].InstallmentTotal.Should().BeNull();
    }

    [Fact]
    public async Task Desactivar_exige_motivo_y_anula_las_novedades_de_periodos_no_aprobados()
    {
        var d = new NominaTestData();
        var id = await Crear(d, d.Ana, "LIBRANZA", null, 50_000m, new DateTime(2026, 1, 1), null);
        (await Calculador(d).Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None)).IsSuccess.Should().BeTrue();
        var h = new DeactivateRecurringNoveltyCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker);

        new DeactivateRecurringNoveltyCommandValidator().Validate(new DeactivateRecurringNoveltyCommand(id, " ")).IsValid.Should().BeFalse();
        (await h.Handle(new DeactivateRecurringNoveltyCommand(id, ""), CancellationToken.None)).Error.Code.Should().Be("Payroll.ReasonRequired");

        var r = await h.Handle(new DeactivateRecurringNoveltyCommand(id, "El préstamo se canceló anticipadamente"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var rec = await d.Db.PayrollRecurringNovelties.SingleAsync(x => x.PublicId == id);
        rec.IsActive.Should().BeFalse();
        rec.DeactivationReason.Should().Be("El préstamo se canceló anticipadamente");
        var novedad = await d.Db.PayrollNovelties.SingleAsync(n => n.RecurringNoveltyId == rec.Id);
        novedad.Status.Should().Be(NoveltyStatus.Cancelled);
        novedad.StatusReason.Should().Contain("Recurrente desactivada");
        (await d.Db.PayrollRuns.SingleAsync(x => x.PayPeriodId == d.Marzo.Id)).Status.Should().Be(PayrollRunStatus.Stale);
        (await h.Handle(new DeactivateRecurringNoveltyCommand(id, "otra vez"), CancellationToken.None)).Error.Code.Should().Be("Payroll.NoveltyNotActive");

        // Y ya no vuelve a materializarse.
        (await Calculador(d).Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await d.Db.PayrollNovelties.CountAsync(n => n.RecurringNoveltyId == rec.Id && n.Status == NoveltyStatus.Active)).Should().Be(0);
    }

    [Fact]
    public async Task La_lista_trae_empleado_concepto_cuotas_y_estado()
    {
        var d = new NominaTestData();
        var bruno = d.Empleado("Bruno", 3_000_000m, new DateTime(2024, 2, 1));
        await Crear(d, d.Ana, "LIBRANZA", null, 50_000m, new DateTime(2026, 1, 1), 10);
        var idBruno = await Crear(d, bruno, "LIBRANZA", null, 80_000m, new DateTime(2026, 2, 1), null);
        await new DeactivateRecurringNoveltyCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker).Handle(new DeactivateRecurringNoveltyCommand(idBruno, "fin"), CancellationToken.None);

        var todas = await new ListRecurringNoveltiesQueryHandler(d.Db).Handle(new ListRecurringNoveltiesQuery(null, null), CancellationToken.None);
        var activas = await new ListRecurringNoveltiesQueryHandler(d.Db).Handle(new ListRecurringNoveltiesQuery(null, true), CancellationToken.None);
        var deBruno = await new ListRecurringNoveltiesQueryHandler(d.Db).Handle(new ListRecurringNoveltiesQuery(bruno.PublicId, null), CancellationToken.None);

        todas.Value.Should().HaveCount(2);
        activas.Value.Should().ContainSingle().Which.EmployeeName.Should().Be("Ana Prueba");
        activas.Value[0].ConceptName.Should().NotBe("LIBRANZA", "trae el nombre del concepto, no el código");
        activas.Value[0].TotalInstallments.Should().Be(10);
        deBruno.Value.Should().ContainSingle().Which.IsActive.Should().BeFalse();
    }
}
