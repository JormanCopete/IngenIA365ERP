using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Caching;
using IngenIA365ERP.Application.Payroll.Novelties.RegisterNovelty;
using IngenIA365ERP.Application.Payroll.Runs.CalculatePayrollRun;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Lending;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Runs;

/// <summary>T069 — FR-007, FR-011, FR-014, FR-015: calcular el período en borrador.</summary>
public class CalculatePayrollRunCommandHandlerTests
{
    private static CalculatePayrollRunCommandHandler Handler(NominaTestData d) =>
        new(d.Db, d.Loader, d.Lock, d.Clock, d.User, NullLogger<CalculatePayrollRunCommandHandler>.Instance);

    private static async Task RegistrarHoras(NominaTestData d, decimal horas = 6m)
    {
        var h = new RegisterNoveltyCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker, d.CarryOver);
        var r = await h.Handle(new RegisterNoveltyCommand { PeriodPublicId = d.Marzo.PublicId, EmployeePublicId = d.Ana.PublicId, ConceptCode = "HEX_NOCTURNA", Quantity = horas }, CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.Error.Message);
    }

    [Fact]
    public async Task Calcula_a_todos_los_empleados_del_plan_y_deja_el_periodo_calculado()
    {
        var d = new NominaTestData();
        var bruno = d.Empleado("Bruno", 1_750_905m, new DateTime(2026, 3, 10));
        await RegistrarHoras(d);

        var r = await Handler(d).Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Version.Should().Be(1);
        r.Value.EmployeeCount.Should().Be(2);
        r.Value.Blockers.Should().BeEmpty();
        r.Value.Totals.Net.Should().BeGreaterThan(0m);

        var run = await d.Db.PayrollRuns.Include(x => x.Employees).ThenInclude(e => e.Lines).SingleAsync();
        run.Status.Should().Be(PayrollRunStatus.Draft);
        run.InputsHash.Should().HaveLength(64);
        run.CalculatedBy.Should().Be("ana@demo");
        run.Employees.Should().HaveCount(2);

        var deBruno = run.Employees.Single(e => e.EmployeeId == bruno.Id);
        deBruno.DaysWorked.Should().Be(21, "ingresó el 10: 21 días comerciales");
        deBruno.Lines.Should().Contain(l => l.ConceptCode == WellKnownConceptCodes.BasicSalary && l.Amount == 1_225_634m);
        deBruno.Lines.Should().OnlyContain(l => l.ExplanationJson.Length > 2);

        var deAna = run.Employees.Single(e => e.EmployeeId == d.Ana.Id);
        deAna.Lines.Should().Contain(l => l.ConceptCode == "HEX_NOCTURNA" && l.NoveltyId != null);
        deAna.ChangedFromPreviousRun.Should().BeTrue("no había borrador anterior");

        var periodo = await d.Db.PayPeriods.SingleAsync(p => p.Id == d.Marzo.Id);
        periodo.Status.Should().Be(PayPeriodStatus.Calculated);
        periodo.RunPublicId.Should().Be(run.PublicId);
    }

    [Fact]
    public async Task Con_el_lock_ocupado_responde_en_curso_y_no_persiste_nada()
    {
        var d = new NominaTestData();
        d.Lock.TryAcquireAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IDistributedLockHandle?>(null));

        var r = await Handler(d).Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.RunInProgress");
        d.Db.PayrollRuns.Should().BeEmpty();
        (await d.Db.PayPeriods.SingleAsync(p => p.Id == d.Marzo.Id)).Status.Should().Be(PayPeriodStatus.Open);
    }

    [Fact]
    public async Task Sin_un_parametro_legal_vigente_se_niega_nombrandolo_y_no_persiste_nada()
    {
        var d = new NominaTestData();
        var uvt = await d.Db.PayrollLegalParameters.SingleAsync(p => p.Code == LegalParameterCodes.Uvt);
        uvt.ValidTo = new DateTime(2026, 2, 28);
        d.Db.SaveChanges();

        var r = await Handler(d).Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.LegalParameterMissing");
        r.Error.Message.Should().Contain("UVT");
        d.Db.PayrollRuns.Should().BeEmpty();
    }

    [Fact]
    public async Task Recalcular_reemplaza_el_borrador_anterior_y_senala_quien_cambio()
    {
        var d = new NominaTestData();
        d.Empleado("Bruno", 1_750_905m, new DateTime(2026, 3, 10));
        var h = Handler(d);

        var v1 = await h.Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);
        var v2 = await h.Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);
        v2.Value.ChangedEmployees.Should().Be(0, "nada cambió entre v1 y v2");
        v2.Value.Version.Should().Be(2);

        await RegistrarHoras(d);
        var v3 = await h.Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);

        v3.IsSuccess.Should().BeTrue(v3.Error.Message);
        v3.Value.Version.Should().Be(3);
        v3.Value.ChangedEmployees.Should().Be(1, "sólo Ana tiene la novedad nueva");

        var estados = await d.Db.PayrollRuns.OrderBy(r => r.Version).Select(r => r.Status).ToListAsync();
        estados.Should().Equal(PayrollRunStatus.Superseded, PayrollRunStatus.Superseded, PayrollRunStatus.Draft);
        v1.Value.RunPublicId.Should().NotBe(v3.Value.RunPublicId);

        // Principio XI: las líneas de las corridas reemplazadas siguen ahí.
        var lineasV1 = await d.Db.PayrollRunLines.CountAsync(l => d.Db.PayrollRunEmployees.Any(e => e.Id == l.PayrollRunEmployeeId && e.Run!.Version == 1));
        lineasV1.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task El_mismo_insumo_produce_el_mismo_hash()
    {
        var d = new NominaTestData();
        var h = Handler(d);
        await h.Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);
        await h.Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);

        var hashes = await d.Db.PayrollRuns.Select(r => r.InputsHash).Distinct().ToListAsync();
        hashes.Should().ContainSingle();
    }

    [Fact]
    public async Task Un_periodo_aprobado_no_se_recalcula()
    {
        var d = new NominaTestData();
        d.Marzo.Status = PayPeriodStatus.Approved;
        d.Db.SaveChanges();

        var r = await Handler(d).Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.PeriodApproved");
    }

    [Fact]
    public async Task Procedimiento_2_sin_porcentaje_deja_el_bloqueo_y_no_calcula_la_retencion()
    {
        var d = new NominaTestData();
        d.Empleado("Carla", 20_000_000m, new DateTime(2020, 6, 1), EmployeeClass.IntegralSalary, procedimientoRetencion: 2);

        var r = await Handler(d).Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Blockers.Should().ContainSingle(b => b.Flag == nameof(RunEmployeeFlag.WithholdingRateMissing));
        var carla = await d.Db.PayrollRunEmployees.Include(e => e.Lines).SingleAsync(e => e.Flags != RunEmployeeFlag.None);
        carla.Lines.Should().NotContain(l => l.ConceptCode == WellKnownConceptCodes.Withholding);
        carla.NotesJson.Should().Contain("Procedimiento 2");
    }

    [Fact]
    public async Task Los_descuentos_de_cartera_entran_como_lineas_sin_asiento()
    {
        var d = new NominaTestData();
        var persona = await d.Db.People.SingleAsync(p => p.Id == d.Ana.PersonId);
        d.Db.PayrollDeductionEntries.Add(new PayrollDeductionEntry
        {
            Period = 202603, CompanyCode = "01", PersonCode = persona.TaxId, EntryType = "DN", ConceptCode = "CR",
            StartDate = "20260301", EndDate = "20260331", Amount = 150_000m, TotalAmount = 150_000m, CreatedBy = "cartera",
        });
        d.Db.SaveChanges();

        var r = await Handler(d).Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var linea = await d.Db.PayrollRunLines.SingleAsync(l => l.ConceptCode == WellKnownConceptCodes.LoanDeduction);
        linea.Amount.Should().Be(150_000m);
        linea.AffectsAccounting.Should().BeFalse("Cartera ya contabilizó (D-08)");
        r.Value.Totals.Net.Should().Be(2_089_095m - 150_000m);
    }
}
