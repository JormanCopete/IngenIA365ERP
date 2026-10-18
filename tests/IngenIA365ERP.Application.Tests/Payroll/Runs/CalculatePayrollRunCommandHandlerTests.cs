using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Caching;
using IngenIA365ERP.Application.Payroll.Novelties.RegisterNovelty;
using IngenIA365ERP.Application.Payroll.Runs.CalculatePayrollRun;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Payroll.Services;
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
        new(d.Db, d.Loader, d.Recurrentes, d.CuotasDeCartera, d.CuotasDeCuentas, d.Lock, d.Clock, d.User, NullLogger<CalculatePayrollRunCommandHandler>.Instance);

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

    /// <summary>Una cuota de Cartera del período de marzo para Ana (feature 013, T0110).</summary>
    private static CuotaDeNominaDto Cuota(NominaTestData d, Guid obligacion, decimal valor, string numero = "0000123") =>
        new(d.Db.People.Single(p => p.Id == d.Ana.PersonId).PublicId, obligacion, numero, "CONSUMO", 7, new DateOnly(2026, 3, 31), valor, 0m, 1);

    [Fact]
    public async Task La_cuota_de_Cartera_entra_como_novedad_LoanDeduction_con_asiento_y_deja_su_fila_Calculated()
    {
        var d = new NominaTestData();
        var obligacion = Guid.NewGuid();
        d.Obligaciones.AgregarCuota(Cuota(d, obligacion, 150_000m));

        var r = await Handler(d).Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var consulta = d.Obligaciones.ConsultasDeCuotas.Should().ContainSingle().Subject;
        consulta.PeriodStart.Should().Be(new DateOnly(2026, 3, 1));
        consulta.PeriodEnd.Should().Be(new DateOnly(2026, 3, 31));

        // Una novedad por empleado, obligación y período, con la obligación (C55).
        var novedad = await d.Db.PayrollNovelties.SingleAsync(n => n.Origin == NoveltyOrigin.LoanDeduction);
        novedad.ObligationPublicId.Should().Be(obligacion);
        novedad.ObligationNumber.Should().Be("0000123");
        novedad.ConceptCode.Should().Be(WellKnownConceptCodes.LoanDeduction);
        novedad.Amount.Should().Be(150_000m);
        novedad.InstallmentNumber.Should().Be(7);

        // Enmienda D-08 (T53): la línea DESC_CARTERA ahora contabiliza (contra el puente «recaudos por nómina para Cartera»).
        var linea = await d.Db.PayrollRunLines.SingleAsync(l => l.ConceptCode == WellKnownConceptCodes.LoanDeduction);
        linea.Amount.Should().Be(150_000m);
        linea.AffectsAccounting.Should().BeTrue("enmienda D-08: DESC_CARTERA contabiliza contra la cuenta del concepto");
        r.Value.Totals.Net.Should().Be(2_089_095m - 150_000m);

        var fila = await d.Db.PayrollLoanDeductions.SingleAsync();
        fila.Status.Should().Be(PayrollLoanDeductionStatus.Calculated);
        fila.ObligationPublicId.Should().Be(obligacion);
        fila.ObligationNumber.Should().Be("0000123");
        fila.ExpectedAmount.Should().Be(150_000m);
        fila.DeductedAmount.Should().Be(150_000m);
        fila.NotDeductedAmount.Should().Be(0m);
        fila.NotDeductedReason.Should().BeNull();
        fila.PayrollRunLineId.Should().Be(linea.Id);
        (await d.Db.PayrollRuns.SingleAsync(x => x.PublicId == r.Value.RunPublicId)).Id.Should().Be(fila.PayrollRunId);
    }

    [Fact]
    public async Task Lo_que_no_cabe_en_el_tope_queda_no_descontado_con_motivo_y_no_se_difiere()
    {
        var d = new NominaTestData();
        d.Obligaciones.AgregarCuota(Cuota(d, Guid.NewGuid(), 2_500_000m));

        var r = await Handler(d).Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var fila = await d.Db.PayrollLoanDeductions.SingleAsync();
        fila.ExpectedAmount.Should().Be(2_500_000m);
        fila.DeductedAmount.Should().Be(1_044_548m, "50 % de (2.249.095 − 160.000), al peso");
        fila.NotDeductedAmount.Should().Be(1_455_452m);
        fila.NotDeductedReason.Should().Be("DeductionCap");
        r.Value.Blockers.Should().BeEmpty("lo no descontado de Cartera no bloquea ni deja neto negativo");
    }

    [Fact]
    public async Task Recalcular_reemplaza_la_novedad_si_la_cuota_cambio_y_anula_la_que_Cartera_ya_no_informa()
    {
        var d = new NominaTestData();
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        d.Obligaciones.AgregarCuota(Cuota(d, a, 150_000m, "A-1"));
        d.Obligaciones.AgregarCuota(Cuota(d, b, 90_000m, "B-1"));
        (await Handler(d).Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None)).IsSuccess.Should().BeTrue();

        d.Obligaciones.LimpiarCuotas();
        d.Obligaciones.AgregarCuota(Cuota(d, a, 175_000m, "A-1"));
        var v2 = await Handler(d).Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);

        v2.IsSuccess.Should().BeTrue(v2.Error.Message);
        var novedades = await d.Db.PayrollNovelties.Where(n => n.Origin == NoveltyOrigin.LoanDeduction).ToListAsync();
        novedades.Should().HaveCount(3, "nada se borra: la de A se reemplazó y la de B se anuló");
        var vigente = novedades.Single(n => n.Status == NoveltyStatus.Active);
        vigente.ObligationPublicId.Should().Be(a);
        vigente.Amount.Should().Be(175_000m);
        vigente.SupersedesNoveltyId.Should().Be(novedades.Single(n => n.Status == NoveltyStatus.Superseded).Id);
        novedades.Single(n => n.ObligationPublicId == b).Status.Should().Be(NoveltyStatus.Cancelled);
        var filasV2 = await d.Db.PayrollLoanDeductions.Where(f => f.PayrollRun!.PublicId == v2.Value.RunPublicId).ToListAsync();
        filasV2.Should().ContainSingle().Which.ExpectedAmount.Should().Be(175_000m);
    }

    [Fact]
    public async Task Con_personas_migradas_sin_activar_avisa_y_no_descuenta()
    {
        var d = new NominaTestData();
        d.Obligaciones.MarcarMigrada(d.Db.People.Single(p => p.Id == d.Ana.PersonId).PublicId);

        var r = await Handler(d).Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Warnings.Should().Contain(CuotasDeCarteraEnNomina.AvisoCarteraPorActivar);
        (await d.Db.PayrollLoanDeductions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Si_Cartera_no_responde_el_calculo_sigue_y_avisa()
    {
        var d = new NominaTestData();
        d.Obligaciones.Caida = new TimeoutException("Cartera no responde (simulado).");

        var r = await Handler(d).Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Warnings.Should().Contain(CuotasDeCarteraEnNomina.AvisoCarteraNoResponde);
    }

    [Fact]
    public async Task Sin_Cartera_activada_la_nomina_calcula_igual_que_siempre()
    {
        var d = new NominaTestData();

        var r = await Handler(d).Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        (await d.Db.PayrollNovelties.CountAsync(n => n.Origin == NoveltyOrigin.LoanDeduction)).Should().Be(0);
        (await d.Db.PayrollLoanDeductions.CountAsync()).Should().Be(0);
        r.Value.Totals.Net.Should().Be(2_089_095m);
    }
}
