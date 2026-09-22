using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Application.Payroll.Terminations;
using IngenIA365ERP.Application.Tests.Payroll.Settlements.Settlement;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Policies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Terminations;

/// <summary>
/// Feature 010, US3 (T065): registrar la terminación crea la terminación y la definitiva en borrador
/// en una acción, propone los descuentos desde Cartera y las libranzas según la política, respeta
/// FR-021 (fecha en período aprobado), no admite dos terminaciones vivas, avisa si Cartera no
/// responde y deja la ficha vigente.
/// </summary>
public class RegisterTerminationCommandHandlerTests
{
    [Fact]
    public async Task Registrar_crea_la_terminacion_y_la_definitiva_en_borrador_con_los_descuentos_propuestos_y_la_ficha_sigue_vigente()
    {
        var p = new DefinitivaDePrueba();

        var t = await p.RegistrarAnaAsync();

        var terminacion = await p.D.Db.EmploymentTerminations.Include(x => x.Deductions).SingleAsync(x => x.PublicId == t.TerminationPublicId);
        terminacion.Status.Should().Be(TerminationStatus.Registered);
        terminacion.TerminationDate.Should().Be(DefinitivaDePrueba.Retiro);
        terminacion.ContractTypeAtTermination.Should().Be(DianContractType.FixedTerm);

        var run = await p.D.Db.PayrollRuns.Include(r => r.Employees).ThenInclude(e => e.Lines).SingleAsync(r => r.PublicId == t.RunPublicId);
        run.Kind.Should().Be(PayrollRunKind.Settlement);
        run.Status.Should().Be(PayrollRunStatus.Draft);
        run.TerminationId.Should().Be(terminacion.Id);
        run.EmployeeId.Should().Be(p.D.Ana.Id);
        run.CutoffDate.Should().Be(DefinitivaDePrueba.Retiro);
        run.PayPeriodId.Should().BeNull();
        var lineas = run.Employees.Single().Lines;
        lineas.Should().Contain(l => l.ConceptCode == WellKnownConceptCodes.PendingSalary && l.Amount == 1_000_000m, "del 1 al 15 de septiembre sobre 2.000.000");
        lineas.Should().Contain(l => l.ConceptCode == WellKnownConceptCodes.Indemnity && l.Amount > 0m, "despido sin justa causa a término fijo: el tiempo faltante");
        lineas.Should().Contain(l => l.ConceptCode == WellKnownConceptCodes.ServiceBonus).And.Contain(l => l.ConceptCode == WellKnownConceptCodes.Severance);

        // Descuentos: saldo total de cada crédito vivo (política SaldoTotal) y sólo las cuotas causadas sin descontar de la libranza.
        terminacion.Deductions.Should().HaveCount(3);
        p.Descuento(t, 1001).Proposed.Should().Be(1_500_000m);
        p.Descuento(t, 1001).Applied.Should().Be(1_500_000m);
        // En Cartera el saldo del crédito es capital (el recaudo sólo le resta el capital pagado); el desglose lo dice.
        p.Descuento(t, 1001).CapitalBalance.Should().Be(1_500_000m);
        p.Descuento(t, 1001).InterestBalance.Should().Be(0m);
        p.Descuento(t, 1001).RemainingAfter.Should().Be(0m, "aplicado el saldo total no queda nada en Cartera");
        p.Descuento(t, 1002).Proposed.Should().Be(300_000m);
        p.LibranzaDe(t).Proposed.Should().Be(100_000m, "cuatro cuotas causadas de junio a septiembre menos dos descontadas");
        t.Deductions.TotalProposed.Should().Be(1_900_000m);
        t.Deductions.NetAfterDeductions.Should().Be(t.Deductions.Net - 1_900_000m);
        t.Deductions.DeductionOverNet.Should().BeFalse();

        // Cada línea de descuento quedó enlazada a su fila (SettlementDeductionId) con el valor aplicado.
        var descuentos = lineas.Where(l => l.SettlementDeductionId != null).ToList();
        descuentos.Should().HaveCount(3);
        descuentos.Select(l => l.SettlementDeductionId).Should().BeEquivalentTo(terminacion.Deductions.Select(d => (int?)d.Id));
        descuentos.Where(l => l.ConceptCode == WellKnownConceptCodes.LoanDeduction).Should().OnlyContain(l => !l.AffectsAccounting, "Cartera contabiliza el recaudo (D-08)");
        descuentos.Sum(l => l.Amount).Should().Be(1_900_000m);
        run.TotalNet.Should().Be(t.Totals.Net);

        // La ficha no se toca al registrar: se cierra al aprobar (FR-020).
        var ficha = await p.D.Db.Employees.SingleAsync(e => e.Id == p.D.Ana.Id);
        ficha.Status.Should().Be(1);
        ficha.TerminationDate.Should().Be(DateTime.MaxValue.Date);
    }

    [Fact]
    public async Task Con_la_politica_SoloCuotasCausadas_el_credito_propone_la_cuota_y_la_mora_no_el_saldo()
    {
        var p = new DefinitivaDePrueba();
        p.D.Politica(CompanyPolicyKeys.DeduccionAlRetiroModo, CompanyPolicyKeys.DeduccionAlRetiroModoValores.SoloCuotasCausadas, new DateOnly(2026, 1, 1));

        var t = await p.RegistrarAnaAsync();

        p.Descuento(t, 1001).Proposed.Should().Be(250_000m, "la cuota de 250.000 sin mora");
        p.Descuento(t, 1002).Proposed.Should().Be(100_000m);
        p.LibranzaDe(t).Proposed.Should().Be(100_000m, "la libranza no depende de la política del crédito");
    }

    [Fact]
    public async Task FR021_una_fecha_dentro_de_un_periodo_aprobado_se_rechaza_y_dice_cual_es_el_abierto()
    {
        var p = new DefinitivaDePrueba();
        var agosto = await p.D.Db.PayPeriods.SingleAsync(x => x.StartDate == new DateTime(2026, 8, 1));

        var r = await p.Registrar().Handle(new RegisterTerminationCommand(p.D.Ana.PublicId, new DateOnly(2026, 8, 20), "RENUNCIA"), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Payroll.Termination.PeriodApproved");
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { periodPublicId = agosto.PublicId, periodName = agosto.Description, openPeriodPublicId = p.Septiembre.PublicId });
        (await p.D.Db.EmploymentTerminations.CountAsync()).Should().Be(0, "no dejó rastro");
        (await p.D.Db.PayrollRuns.CountAsync(x => x.Kind == PayrollRunKind.Settlement)).Should().Be(0);
    }

    [Fact]
    public async Task Fechas_anterior_al_ingreso_o_futura_se_rechazan()
    {
        var p = new DefinitivaDePrueba();

        var antes = await p.Registrar().Handle(new RegisterTerminationCommand(p.D.Ana.PublicId, new DateOnly(2024, 12, 31), "RENUNCIA"), CancellationToken.None);
        antes.Error.Code.Should().Be("Payroll.Termination.DateBeforeHire");

        var futura = await p.Registrar().Handle(new RegisterTerminationCommand(p.D.Ana.PublicId, new DateOnly(2026, 9, 25), "RENUNCIA"), CancellationToken.None);
        futura.Error.Code.Should().Be("Payroll.Termination.DateInFuture");
    }

    [Fact]
    public async Task Motivo_inexistente_o_inactivo_y_termino_fijo_sin_fecha_de_fin_se_rechazan()
    {
        var p = new DefinitivaDePrueba();

        var sinMotivo = await p.Registrar().Handle(new RegisterTerminationCommand(p.D.Ana.PublicId, DefinitivaDePrueba.Retiro, "NO_EXISTE"), CancellationToken.None);
        sinMotivo.Error.Code.Should().Be("Payroll.Termination.ReasonNotFound");

        var sinFin = await p.Registrar().Handle(new RegisterTerminationCommand(p.D.Ana.PublicId, DefinitivaDePrueba.Retiro, "DESP_SINJC", DianContractType.FixedTerm), CancellationToken.None);
        sinFin.Error.Code.Should().Be("Payroll.Termination.ContractEndDateRequired");

        var indefinido = await p.Registrar().Handle(new RegisterTerminationCommand(p.D.Ana.PublicId, DefinitivaDePrueba.Retiro, "desp_sinjc", DianContractType.Indefinite), CancellationToken.None);
        indefinido.IsSuccess.Should().BeTrue("a término indefinido no hace falta la fecha de fin y el código entra en cualquier caja: " + indefinido.Error.Message);
    }

    [Fact]
    public async Task Una_segunda_terminacion_con_la_definitiva_en_borrador_responde_PendingSettlement_con_la_corrida()
    {
        var p = new DefinitivaDePrueba();
        var primera = await p.RegistrarAnaAsync();

        var r = await p.Registrar().Handle(new RegisterTerminationCommand(p.D.Ana.PublicId, DefinitivaDePrueba.Retiro, "RENUNCIA"), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.Termination.PendingSettlement");
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { runPublicId = primera.RunPublicId });
    }

    [Fact]
    public async Task Un_empleado_ya_retirado_responde_EmployeeAlreadyTerminated()
    {
        var p = new DefinitivaDePrueba();
        p.D.Ana.Status = -1;
        p.D.Ana.TerminationDate = new DateTime(2026, 8, 31);
        await p.D.Db.SaveChangesAsync();

        var r = await p.Registrar().Handle(new RegisterTerminationCommand(p.D.Ana.PublicId, DefinitivaDePrueba.Retiro, "RENUNCIA"), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.Termination.EmployeeAlreadyTerminated");
    }

    [Fact]
    public async Task Sin_deudas_no_hay_descuentos_y_con_renuncia_la_indemnizacion_se_omite_con_su_razon()
    {
        var p = new DefinitivaDePrueba(conCartera: false);

        var t = await p.RegistrarAnaAsync("RENUNCIA", DianContractType.Indefinite);

        t.Deductions.Items.Should().BeEmpty();
        t.Lines.Should().NotContain(l => l.ConceptCode == WellKnownConceptCodes.Indemnity);
        t.Skips.Should().Contain(s => s.Contains("indemniza", StringComparison.OrdinalIgnoreCase));
        t.Lines.Should().NotContain(l => l.ConceptCode == WellKnownConceptCodes.LoanDeduction);
    }

    [Fact]
    public async Task Si_Cartera_no_responde_la_propuesta_de_creditos_sale_vacia_con_aviso_la_libranza_sigue_y_no_bloquea()
    {
        var p = new DefinitivaDePrueba();
        var caida = CarteraCaida(p);
        var loader = new SettlementInputLoader(caida, p.D.Policies, new ProvisionBalanceReader(caida), NullLogger<SettlementInputLoader>.Instance);
        var handler = new RegisterTerminationCommandHandler(caida, loader, new SettlementRunPersister(caida, p.D.Clock, p.D.User), p.D.Clock, p.D.User, p.D.AuditEmitter);

        var r = await handler.Handle(new RegisterTerminationCommand(p.D.Ana.PublicId, DefinitivaDePrueba.Retiro, "DESP_SINJC", DianContractType.FixedTerm, new DateOnly(2026, 12, 31)), CancellationToken.None);

        r.IsSuccess.Should().BeTrue("Cartera caída avisa, no bloquea: " + r.Error.Message);
        r.Value.Warnings.Should().ContainSingle(w => w.Code == "Payroll.Settlement.PortfolioUnavailable");
        r.Value.Deductions.Items.Should().ContainSingle(i => i.Kind == SettlementDeductionKind.ThirdPartyLibranza, "la libranza no depende de Cartera");
        r.Value.Deductions.Items.Should().NotContain(i => i.Kind == SettlementDeductionKind.CooperativeLoan);
        (await p.D.Db.PayrollRuns.CountAsync(x => x.Kind == PayrollRunKind.Settlement && x.Status == PayrollRunStatus.Draft)).Should().Be(1);
    }

    /// <summary>El mismo contexto de pruebas, salvo que la tabla de Cartera no responde.</summary>
    private static IApplicationDbContext CarteraCaida(DefinitivaDePrueba p)
    {
        var real = p.D.Db;
        var db = Substitute.For<IApplicationDbContext>();
        db.Employees.Returns(real.Employees);
        db.People.Returns(real.People);
        db.EmploymentTerminations.Returns(real.EmploymentTerminations);
        db.TerminationReasons.Returns(real.TerminationReasons);
        db.SettlementDeductions.Returns(real.SettlementDeductions);
        db.PayPeriods.Returns(real.PayPeriods);
        db.PayrollPlans.Returns(real.PayrollPlans);
        db.PayrollRuns.Returns(real.PayrollRuns);
        db.PayrollRunEmployees.Returns(real.PayrollRunEmployees);
        db.PayrollRunLines.Returns(real.PayrollRunLines);
        db.WorkRiskRates.Returns(real.WorkRiskRates);
        db.SalaryChanges.Returns(real.SalaryChanges);
        db.PayrollConceptDefinitions.Returns(real.PayrollConceptDefinitions);
        db.PayrollLegalParameters.Returns(real.PayrollLegalParameters);
        db.EmployeeWithholdingRates.Returns(real.EmployeeWithholdingRates);
        db.EmployeeTaxDeductions.Returns(real.EmployeeTaxDeductions);
        db.PayrollNovelties.Returns(real.PayrollNovelties);
        db.PayrollRecurringNovelties.Returns(real.PayrollRecurringNovelties);
        db.EmployeeBenefitOpeningBalances.Returns(real.EmployeeBenefitOpeningBalances);
        db.VacationMovements.Returns(real.VacationMovements);
        db.LoanPortfolios.Returns(_ => throw new InvalidOperationException("Cartera no disponible (simulado)."));
        db.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(ci => real.SaveChangesAsync(ci.Arg<CancellationToken>()));
        return db;
    }
}
