using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Payroll.Novelties.RegisterNovelty;
using IngenIA365ERP.Application.Payroll.Runs;
using IngenIA365ERP.Application.Payroll.Runs.ApprovePayrollRun;
using IngenIA365ERP.Application.Payroll.Runs.CalculatePayrollRun;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Runs;

/// <summary>T070 — FR-019..FR-023: aprobar genera el asiento en la misma transacción, o no aprueba.</summary>
public class ApprovePayrollRunCommandHandlerTests
{
    private static readonly ICurrentUserService Contadora = NominaTestData.UsuarioDePrueba("contadora@demo", 9);

    private static async Task<Guid> Calcular(NominaTestData d)
    {
        var h = new CalculatePayrollRunCommandHandler(d.Db, d.Loader, d.Recurrentes, d.Lock, d.Clock, d.User, NullLogger<CalculatePayrollRunCommandHandler>.Instance);
        var r = await h.Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.Error.Message);
        return r.Value.RunPublicId;
    }

    private static ApprovePayrollRunCommandHandler Aprobador(NominaTestData d, ICurrentUserService? quien = null)
    {
        var usuario = quien ?? Contadora;
        var poster = d.Contabilizador(usuario);
        var audit = new PayrollAuditEmitter(d.Audit, usuario, d.Clock, NullLogger<PayrollAuditEmitter>.Instance);
        return new ApprovePayrollRunCommandHandler(d.Db, poster, d.Policies, d.Permissions, d.Clock, usuario, audit);
    }

    private static async Task RegistrarHoras(NominaTestData d)
    {
        var h = new RegisterNoveltyCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker, d.CarryOver);
        var r = await h.Handle(new RegisterNoveltyCommand { PeriodPublicId = d.Marzo.PublicId, EmployeePublicId = d.Ana.PublicId, ConceptCode = "HEX_NOCTURNA", Quantity = 6m }, CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.Error.Message);
    }

    [Fact]
    public async Task Aprobar_cierra_el_periodo_y_genera_el_comprobante_NM_cuadrado()
    {
        var d = new NominaTestData();
        d.ConfigurarContabilidad();
        await RegistrarHoras(d);
        var runId = await Calcular(d);

        var r = await Aprobador(d).Handle(new ApprovePayrollRunCommand(runId, Confirm: true), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.AccountingDocumentNumber.Should().Be("NM-1");

        var run = await d.Db.PayrollRuns.SingleAsync(x => x.PublicId == runId);
        run.Status.Should().Be(PayrollRunStatus.Approved);
        run.ApprovedBy.Should().Be("contadora@demo");
        run.ApprovedWithoutSegregation.Should().BeFalse();
        run.AccountingDocumentId.Should().NotBeNull();

        var periodo = await d.Db.PayPeriods.SingleAsync(p => p.Id == d.Marzo.Id);
        periodo.Status.Should().Be(PayPeriodStatus.Approved);
        periodo.ApprovedBy.Should().Be("contadora@demo");
        periodo.RunPublicId.Should().Be(runId);

        var doc = await d.Db.AccountingDocuments.Include(x => x.VoucherType).SingleAsync(x => x.Id == run.AccountingDocumentId);
        doc.VoucherType!.Code.Should().Be("NM");
        doc.TotalDebit.Should().Be(doc.TotalCredit).And.BeGreaterThan(0m);
        doc.OriginModule.Should().Be("NOM");
        doc.Number.Should().Be(1);
        doc.Status.Should().Be(Domain.Enums.Accounting.DocumentStatus.Posted);
        var asientos = await d.Db.JournalEntries.Where(j => j.DocumentId == doc.Id).ToListAsync();
        asientos.Sum(a => a.Debit).Should().Be(asientos.Sum(a => a.Credit));
        asientos.Should().OnlyContain(a => a.IsPosted && a.Date == new DateOnly(2026, 3, 31) && a.BranchId == d.Principal.Id);
        asientos.Should().HaveCountGreaterThan(2);
        (await d.Db.VoucherTypes.SingleAsync(v => v.Code == "NM")).NextNumber.Should().Be(2);

        // Devengos y deducciones menos las líneas sin asiento tienen que estar en el comprobante.
        // El asiento va por concepto y en valor absoluto: una línea negativa (el ajuste por
        // redondeo, que con horas a 220/mes aparece casi siempre) gira las cuentas en vez de
        // restar del débito, así que se compara contra la suma de |total por concepto|.
        var lineasContables = (await d.Db.PayrollRunLines.Where(l => l.AffectsAccounting).GroupBy(l => l.ConceptCode).Select(g => g.Sum(l => l.Amount)).ToListAsync()).Sum(Math.Abs);
        doc.TotalDebit.Should().Be(lineasContables);

        await d.Audit.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(a => a.Action == AuditEventTypes.PayrollRunApproved), Arg.Any<CancellationToken>());
        (await d.Db.PayrollNovelties.SingleAsync()).Status.Should().Be(NoveltyStatus.Active, "las novedades no se tocan, sólo quedan inmutables por el estado del período");
    }

    [Fact]
    public async Task Sin_confirmacion_explicita_no_aprueba()
    {
        var d = new NominaTestData();
        d.ConfigurarContabilidad();
        var runId = await Calcular(d);

        var r = await Aprobador(d).Handle(new ApprovePayrollRunCommand(runId, Confirm: false), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.ConfirmationRequired");
    }

    [Fact]
    public async Task Un_borrador_desactualizado_no_se_aprueba()
    {
        var d = new NominaTestData();
        d.ConfigurarContabilidad();
        var runId = await Calcular(d);
        await RegistrarHoras(d); // deja el borrador Stale

        var r = await Aprobador(d).Handle(new ApprovePayrollRunCommand(runId, Confirm: true), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.RunStale");
        (await d.Db.PayPeriods.SingleAsync(p => p.Id == d.Marzo.Id)).Status.Should().Be(PayPeriodStatus.Calculated);
    }

    [Fact]
    public async Task Un_bloqueo_sin_excepcion_impide_aprobar_y_nombra_al_empleado()
    {
        var d = new NominaTestData();
        d.ConfigurarContabilidad();
        d.Empleado("Carla", 20_000_000m, new DateTime(2020, 6, 1), EmployeeClass.IntegralSalary, procedimientoRetencion: 2);
        var runId = await Calcular(d);

        var r = await Aprobador(d).Handle(new ApprovePayrollRunCommand(runId, Confirm: true), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.ApprovalBlocked");
        r.Error.Message.Should().Contain("retención");
    }

    [Fact]
    public async Task Una_excepcion_exige_el_permiso_y_queda_registrada()
    {
        var d = new NominaTestData();
        d.ConfigurarContabilidad();
        var carla = d.Empleado("Carla", 20_000_000m, new DateTime(2020, 6, 1), EmployeeClass.IntegralSalary, procedimientoRetencion: 2);
        var runId = await Calcular(d);
        var excepciones = new List<ApprovalExceptionDto> { new(carla.PublicId, nameof(RunEmployeeFlag.WithholdingRateMissing), "Retención se declara manualmente este mes") };

        var sinPermiso = await Aprobador(d).Handle(new ApprovePayrollRunCommand(runId, true, excepciones), CancellationToken.None);
        sinPermiso.Error.Code.Should().Be("Payroll.ExceptionNotAuthorized");

        d.Permissions.HasPermissionAsync(ApprovePayrollRunCommandHandler.AuthorizeExceptionPermission, Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));
        var conPermiso = await Aprobador(d).Handle(new ApprovePayrollRunCommand(runId, true, excepciones), CancellationToken.None);

        conPermiso.IsSuccess.Should().BeTrue(conPermiso.Error.Message);
        var run = await d.Db.PayrollRuns.SingleAsync(x => x.PublicId == runId);
        run.ExceptionsJson.Should().Contain("Retención se declara manualmente").And.Contain("contadora@demo");
    }

    [Fact]
    public async Task Un_concepto_sin_cuentas_impide_aprobar_y_no_deja_comprobante()
    {
        var d = new NominaTestData();
        d.ConfigurarContabilidad();
        var cuentasSalario = await d.Db.PayrollConceptDefinitionAccounts.Where(a => a.ConceptCode == "SALARIO").ToListAsync();
        d.Db.PayrollConceptDefinitionAccounts.RemoveRange(cuentasSalario);
        d.Db.SaveChanges();
        var runId = await Calcular(d);

        var r = await Aprobador(d).Handle(new ApprovePayrollRunCommand(runId, Confirm: true), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.ConceptWithoutAccounts");
        r.Error.Message.Should().Contain("SALARIO");
        d.Db.AccountingDocuments.Should().BeEmpty();
        (await d.Db.PayrollRuns.SingleAsync(x => x.PublicId == runId)).Status.Should().Be(PayrollRunStatus.Draft);
    }

    [Fact]
    public async Task Con_el_periodo_contable_cerrado_no_hay_aprobacion()
    {
        var d = new NominaTestData();
        d.ConfigurarContabilidad(periodoContableAbierto: false);
        var runId = await Calcular(d);

        var r = await Aprobador(d).Handle(new ApprovePayrollRunCommand(runId, Confirm: true), CancellationToken.None);

        r.Error.Code.Should().Be("Accounting.Period.Closed");
        (await d.Db.PayPeriods.SingleAsync(p => p.Id == d.Marzo.Id)).Status.Should().Be(PayPeriodStatus.Calculated);
    }

    [Fact]
    public async Task Quien_calculo_o_registro_novedades_no_aprueba_salvo_que_la_cooperativa_lo_permita_con_doble_confirmacion()
    {
        var d = new NominaTestData();
        d.ConfigurarContabilidad();
        await RegistrarHoras(d);
        var runId = await Calcular(d);

        var mismaPersona = await Aprobador(d, d.User).Handle(new ApprovePayrollRunCommand(runId, Confirm: true), CancellationToken.None);
        mismaPersona.Error.Code.Should().Be("Payroll.SegregationOfDuties");

        d.PermitirAprobarMismoUsuario();
        var sinSegundaConfirmacion = await Aprobador(d, d.User).Handle(new ApprovePayrollRunCommand(runId, Confirm: true), CancellationToken.None);
        sinSegundaConfirmacion.Error.Code.Should().Be("Payroll.ConfirmationRequired");

        var conDoble = await Aprobador(d, d.User).Handle(new ApprovePayrollRunCommand(runId, Confirm: true, ConfirmWithoutSegregation: true), CancellationToken.None);
        conDoble.IsSuccess.Should().BeTrue(conDoble.Error.Message);
        conDoble.Value.ApprovedWithoutSegregation.Should().BeTrue();
        (await d.Db.PayrollRuns.SingleAsync(x => x.PublicId == runId)).ApprovedWithoutSegregation.Should().BeTrue();
    }

    [Fact]
    public async Task Despues_de_aprobar_no_se_calcula_ni_se_aprueba_de_nuevo()
    {
        var d = new NominaTestData();
        d.ConfigurarContabilidad();
        var runId = await Calcular(d);
        (await Aprobador(d).Handle(new ApprovePayrollRunCommand(runId, true), CancellationToken.None)).IsSuccess.Should().BeTrue();

        var otraVez = await Aprobador(d).Handle(new ApprovePayrollRunCommand(runId, true), CancellationToken.None);
        otraVez.Error.Code.Should().Be("Payroll.RunNotDraft");

        var calculo = await new CalculatePayrollRunCommandHandler(d.Db, d.Loader, d.Recurrentes, d.Lock, d.Clock, d.User, NullLogger<CalculatePayrollRunCommandHandler>.Instance)
            .Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);
        calculo.Error.Code.Should().Be("Payroll.PeriodApproved");
    }

    [Fact]
    public async Task Un_concepto_parametrizado_a_una_cuenta_de_agrupacion_impide_aprobar_nombrandola_y_no_deja_nada()
    {
        var d = new NominaTestData();
        d.ConfigurarContabilidad();
        var cuentasSalario = await d.Db.PayrollConceptDefinitionAccounts.SingleAsync(a => a.ConceptCode == "SALARIO");
        var gasto = await d.Db.ChartOfAccounts.SingleAsync(a => a.Id == cuentasSalario.DebitAccountId);
        gasto.IsMovement = false;
        gasto.Level = 4;
        await d.Db.SaveChangesAsync();
        var runId = await Calcular(d);

        var r = await Aprobador(d).Handle(new ApprovePayrollRunCommand(runId, Confirm: true), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Accounting.Line.AccountNotMovement");
        r.Error.Message.Should().Contain(gasto.Code);
        d.Db.AccountingDocuments.Should().BeEmpty();
        d.Db.JournalEntries.Should().BeEmpty();
        (await d.Db.PayrollRuns.SingleAsync(x => x.PublicId == runId)).Status.Should().Be(PayrollRunStatus.Draft);
        (await d.Db.VoucherTypes.SingleAsync(v => v.Code == "NM")).NextNumber.Should().Be(1);
    }
}
