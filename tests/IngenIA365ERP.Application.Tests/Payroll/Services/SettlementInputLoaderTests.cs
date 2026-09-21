using FluentAssertions;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Lending;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using IngenIA365ERP.Domain.Payroll.Policies;
using IngenIA365ERP.Domain.Payroll.Settlements;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Payroll.Services;

/// <summary>
/// Feature 010 (T026): el cargador arma la entrada del motor de liquidaciones desde la ficha, las
/// corridas aprobadas por imputación, las provisiones, el saldo inicial, los movimientos de
/// vacaciones, la terminación y Cartera. No calcula: lo que se prueba es que cada dato llegue
/// de donde debe y como el motor lo espera.
/// </summary>
public class SettlementInputLoaderTests
{
    private static NominaTestData ConSeisMesesAprobados()
    {
        var d = new NominaTestData();
        // Enero a junio de 2026: salario, auxilio, provisiones y horas extras en dos meses.
        for (var mes = 1; mes <= 6; mes++)
            d.MesAprobado(2026, mes, d.Ana, 2_000_000m, 249_095m, provPrima: 187_424m, provCesantias: 187_424m, provIntereses: 22_491m, provVacaciones: 83_333m,
                variables: mes is 2 or 5 ? 120_000m : 0m);
        return d;
    }

    [Fact]
    public async Task La_prima_recibe_bases_por_mes_provisiones_acumuladas_y_el_historial_de_salario()
    {
        var d = ConSeisMesesAprobados();
        d.CambioDeSalario(d.Ana, new DateTime(2026, 4, 1), 2_400_000m);

        var batch = await d.SettlementLoader.LoadAsync(SettlementLoadRequest.Prima(2026, 1), CancellationToken.None);

        batch.Kind.Should().Be(SettlementKind.ServiceBonus);
        batch.MissingRequiredParameters.Should().BeEmpty("la semilla 2026 trae todo lo que la liquidación exige");
        var ana = batch.Employees.Should().ContainSingle(e => e.Employee.Id == d.Ana.Id).Subject;
        var input = ana.Input;
        input.CutoffDate.Should().Be(new DateTime(2026, 6, 30));
        input.PeriodStart.Should().Be(new DateTime(2026, 1, 1));

        // Bases por mes de imputación: seis meses; las variables sólo en febrero y mayo; el ingreso laboral suma todo.
        input.MonthlyBases.Should().HaveCount(6);
        input.MonthlyBases.Single(m => m.Month == 2).VariableBenefitsEarnings.Should().Be(120_000m);
        input.MonthlyBases.Single(m => m.Month == 3).VariableBenefitsEarnings.Should().Be(0m, "un mes con corrida y sin variables cuenta en cero");
        input.MonthlyBases.Single(m => m.Month == 2).VariableVacationEarnings.Should().Be(0m, "las horas extras no entran a la base de vacaciones (CST art. 192)");
        input.MonthlyBases.Single(m => m.Month == 2).LaborIncome.Should().Be(2_000_000m + 249_095m + 120_000m);

        // Provisión acumulada por concepto: la suma de las corridas aprobadas hasta el corte.
        input.Provisions.Single(p => p.ProvisionConceptCode == WellKnownConceptCodes.ServiceBonusProvision).Accrued.Should().Be(6 * 187_424m);
        input.Provisions.Single(p => p.ProvisionConceptCode == WellKnownConceptCodes.SeveranceInterestProvision).Accrued.Should().Be(6 * 22_491m);

        // Historial de salario con la línea base del ingreso y el cambio de abril.
        input.Employee.SalaryHistory.Select(h => (h.EffectiveDate, h.MonthlySalary)).Should().Equal(
            (new DateTime(2025, 1, 15), 2_000_000m), (new DateTime(2026, 4, 1), 2_400_000m));
        input.Employee.TerminationDate.Should().BeNull("sigue vinculada");
        input.OpeningBalance.Should().BeNull();
        input.Policies.PayrollStartDate.Should().BeNull();
    }

    [Fact]
    public async Task El_saldo_inicial_y_sus_ajustes_llegan_como_un_solo_tramo_con_fecha_y_autor()
    {
        var d = ConSeisMesesAprobados();
        var apertura = d.SaldoInicial(d.Ana, new DateOnly(2025, 12, 31), prima: 500_000m, cesantias: 2_000_000m, intereses: 240_000m, diasVacaciones: 10m, diasPrima: 80);
        d.Db.EmployeeBenefitOpeningBalances.Add(new EmployeeBenefitOpeningBalance
        {
            EmployeeId = d.Ana.Id, AsOfDate = new DateOnly(2025, 12, 31), Kind = OpeningBalanceKind.Adjustment, AdjustsBalanceId = apertura.Id,
            AccruedSeverance = 100_000m, AdjustmentReason = "Faltó un mes", CreatedBy = "contadora@demo",
        });
        d.Politica(CompanyPolicyKeys.ArranqueNominaFecha, "2026-01-01", new DateOnly(2020, 1, 1));
        await d.Db.SaveChangesAsync();

        var batch = await d.SettlementLoader.LoadAsync(SettlementLoadRequest.Cesantias(2026, new DateOnly(2026, 6, 30)), CancellationToken.None);

        var input = batch.Employees.Single().Input;
        input.OpeningBalance.Should().NotBeNull();
        input.OpeningBalance!.AsOfDate.Should().Be(new DateTime(2025, 12, 31));
        input.OpeningBalance.AccruedSeverance.Should().Be(2_100_000m, "apertura más ajuste");
        input.OpeningBalance.AccruedServiceBonus.Should().Be(500_000m);
        input.OpeningBalance.ServiceBonusDaysAccrued.Should().Be(80);
        input.OpeningBalance.EnteredBy.Should().Be("contadora@demo");
        input.Policies.PayrollStartDate.Should().Be(new DateTime(2026, 1, 1));
        // La provisión de cesantías incluye lo digitado en el saldo inicial (R3).
        input.Provisions.Single(p => p.ProvisionConceptCode == WellKnownConceptCodes.SeveranceProvision).Accrued.Should().Be(6 * 187_424m + 2_100_000m);
    }

    [Fact]
    public async Task Las_ausencias_salen_de_las_novedades_con_fechas_y_solo_las_informativas_son_suspension()
    {
        var d = ConSeisMesesAprobados();
        var febrero = await d.Db.PayPeriods.SingleAsync(p => p.StartDate == new DateTime(2026, 2, 1));
        var conceptos = await d.Db.PayrollConceptDefinitions.ToDictionaryAsync(c => c.Code);
        d.Db.PayrollNovelties.AddRange(
            new PayrollNovelty { PayPeriodId = febrero.Id, EmployeeId = d.Ana.Id, ConceptDefinitionId = conceptos["SUSPENSION"].Id, ConceptCode = "SUSPENSION", StartDate = new DateTime(2026, 2, 10), EndDate = new DateTime(2026, 2, 14), CreatedBy = "test" },
            new PayrollNovelty { PayPeriodId = febrero.Id, EmployeeId = d.Ana.Id, ConceptDefinitionId = conceptos["INCAP_GENERAL"].Id, ConceptCode = "INCAP_GENERAL", StartDate = new DateTime(2026, 2, 20), EndDate = new DateTime(2026, 2, 22), CreatedBy = "test" },
            new PayrollNovelty { PayPeriodId = febrero.Id, EmployeeId = d.Ana.Id, ConceptDefinitionId = conceptos["HEX_DIURNA"].Id, ConceptCode = "HEX_DIURNA", Quantity = 2m, CreatedBy = "test" });
        await d.Db.SaveChangesAsync();

        var batch = await d.SettlementLoader.LoadAsync(SettlementLoadRequest.Prima(2026, 1), CancellationToken.None);

        var ausencias = batch.Employees.Single().Input.Absences;
        ausencias.Should().HaveCount(2, "las horas extras no tienen fechas ni reducen días");
        ausencias.Single(a => a.Description == "SUSPENSION").IsSuspension.Should().BeTrue();
        ausencias.Single(a => a.Description == "INCAP_GENERAL").IsSuspension.Should().BeFalse("la incapacidad cuenta para prestaciones (art. 53 sólo descuenta suspensiones)");
    }

    [Fact]
    public async Task La_definitiva_trae_terminacion_salario_pendiente_prima_ya_pagada_y_deudas_de_cartera_y_libranzas()
    {
        var d = ConSeisMesesAprobados();
        // Julio y agosto aprobados; septiembre abierto: allí cae el retiro.
        d.MesAprobado(2026, 7, d.Ana, 2_000_000m, 249_095m, 187_424m, 187_424m, 22_491m, 83_333m);
        d.MesAprobado(2026, 8, d.Ana, 2_000_000m, 249_095m, 187_424m, 187_424m, 22_491m, 83_333m);
        var septiembre = d.Periodo(new DateTime(2026, 9, 1), new DateTime(2026, 9, 30), PayPeriodStatus.Open);
        var retiro = new DateOnly(2026, 9, 15);

        var motivo = new TerminationReason { Code = "DESP_SINJC", Name = "Despido sin justa causa", GeneratesSeverancePay = true, IsSeeded = true, CreatedBy = "test" };
        d.Db.TerminationReasons.Add(motivo);
        await d.Db.SaveChangesAsync();
        var terminacion = new EmploymentTermination
        {
            EmployeeId = d.Ana.Id, TerminationDate = retiro, TerminationReasonId = motivo.Id,
            ContractTypeAtTermination = DianContractType.FixedTerm, ContractEndDate = new DateOnly(2026, 12, 31), CreatedBy = "test",
        };
        d.Db.EmploymentTerminations.Add(terminacion);

        // Cartera: dos créditos vivos y uno cerrado, por persona.
        d.Db.LoanPortfolios.AddRange(
            new LoanPortfolio { PersonId = d.Ana.PersonId, PortfolioNumber = 1001, CurrentBalance = 1_500_000m, CapitalBalanceCurrent = 1_400_000m, InterestBalanceCurrent = 100_000m, InstallmentAmount = 250_000m, PendingInstallmentCount = 6, DisbursementDate = new DateOnly(2026, 1, 10), CreatedBy = "test" },
            new LoanPortfolio { PersonId = d.Ana.PersonId, PortfolioNumber = 1002, CurrentBalance = 300_000m, CapitalBalanceCurrent = 300_000m, InstallmentAmount = 100_000m, PendingInstallmentCount = 3, DisbursementDate = new DateOnly(2026, 5, 10), CreatedBy = "test" },
            new LoanPortfolio { PersonId = d.Ana.PersonId, PortfolioNumber = 1003, CurrentBalance = 0m, ClosingDate = new DateOnly(2026, 3, 1), DisbursementDate = new DateOnly(2025, 1, 10), CreatedBy = "test" });
        // Libranza con un tercero: cuota de 50.000 desde junio, 4 cuotas causadas (jun–sep) y 2 descontadas.
        d.Db.PayrollRecurringNovelties.Add(new PayrollRecurringNovelty
        {
            EmployeeId = d.Ana.Id, ConceptCode = SettlementInputLoader.LibranzaCode, Amount = 50_000m, StartDate = new DateTime(2026, 6, 1), TotalInstallments = 12, InstallmentsIssued = 2, Notes = "Coopcentral", CreatedBy = "test",
        });
        await d.Db.SaveChangesAsync();

        var batch = await d.SettlementLoader.LoadAsync(SettlementLoadRequest.Definitiva(d.Ana.Id, terminacion.Id, retiro), CancellationToken.None);

        var cargado = batch.Employees.Single();
        var input = cargado.Input;
        input.Kind.Should().Be(SettlementKind.Settlement);
        input.Employee.TerminationDate.Should().Be(new DateTime(2026, 9, 15));
        input.Employee.ContractType.Should().Be(DianContractType.FixedTerm);
        input.Employee.ContractEndDate.Should().Be(new DateTime(2026, 12, 31));
        input.Termination.Should().NotBeNull();
        input.Termination!.ReasonCode.Should().Be("DESP_SINJC");
        input.Termination.GeneratesSeverancePay.Should().BeTrue();
        input.PendingSalary.Should().Be(new PendingSalaryInput(septiembre.StartDate, septiembre.EndDate));
        input.MonthlyBases.Should().HaveCount(8, "enero a agosto aprobados; septiembre está abierto y no cuenta");

        // Deudas: saldo total de los dos créditos vivos (política por defecto) y las cuotas causadas sin descontar de la libranza.
        cargado.Deudas.Should().HaveCount(3);
        var credito = cargado.Deudas.Single(x => x.LoanPortfolioId != null && x.Description.Contains("1001"));
        credito.Kind.Should().Be(SettlementDeductionKind.CooperativeLoan);
        credito.Proposed.Should().Be(1_500_000m);
        credito.Applied.Should().Be(1_500_000m);
        credito.ConceptCode.Should().Be(WellKnownConceptCodes.LoanDeduction);
        credito.AccountedByOtherModule.Should().BeTrue("Cartera contabiliza el recaudo (D-08)");
        credito.BreakdownJson.Should().Contain("\"capital\":1400000").And.Contain("\"intereses\":100000");
        cargado.Deudas.Should().NotContain(x => x.Description.Contains("1003"), "el crédito cerrado no se propone");
        var libranza = cargado.Deudas.Single(x => x.RecurringNoveltyId != null);
        libranza.Kind.Should().Be(SettlementDeductionKind.ThirdPartyLibranza);
        libranza.Proposed.Should().Be(2 * 50_000m, "cuatro cuotas causadas (junio a septiembre) menos dos descontadas");
        libranza.AccountedByOtherModule.Should().BeFalse();
        input.ProposedDeductions.Should().HaveCount(3);
        batch.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task La_politica_de_deduccion_al_retiro_cambia_la_propuesta_y_puede_apagarla()
    {
        var d = ConSeisMesesAprobados();
        var motivo = new TerminationReason { Code = "RENUNCIA", Name = "Renuncia", IsSeeded = true, CreatedBy = "test" };
        d.Db.TerminationReasons.Add(motivo);
        await d.Db.SaveChangesAsync();
        var terminacion = new EmploymentTermination { EmployeeId = d.Ana.Id, TerminationDate = new DateOnly(2026, 3, 20), TerminationReasonId = motivo.Id, CreatedBy = "test" };
        d.Db.EmploymentTerminations.Add(terminacion);
        d.Db.LoanPortfolios.Add(new LoanPortfolio { PersonId = d.Ana.PersonId, PortfolioNumber = 7, CurrentBalance = 1_000_000m, InstallmentAmount = 120_000m, DefaultBalanceCurrent = 30_000m, DisbursementDate = new DateOnly(2026, 1, 1), CreatedBy = "test" });
        await d.Db.SaveChangesAsync();

        d.Politica(CompanyPolicyKeys.DeduccionAlRetiroModo, CompanyPolicyKeys.DeduccionAlRetiroModoValores.SoloCuotasCausadas, new DateOnly(2020, 1, 1));
        var cuotas = await d.SettlementLoader.LoadAsync(SettlementLoadRequest.Definitiva(d.Ana.Id, terminacion.Id, new DateOnly(2026, 3, 20)), CancellationToken.None);
        cuotas.Employees.Single().Deudas.Single().Proposed.Should().Be(150_000m, "la cuota más la mora, no el saldo");

        var politica = await d.Db.CompanyPolicies.SingleAsync();
        politica.Value = CompanyPolicyKeys.DeduccionAlRetiroModoValores.NoProponer;
        await d.Db.SaveChangesAsync();
        var nada = await d.SettlementLoader.LoadAsync(SettlementLoadRequest.Definitiva(d.Ana.Id, terminacion.Id, new DateOnly(2026, 3, 20)), CancellationToken.None);
        nada.Employees.Single().Deudas.Should().BeEmpty();
    }

    [Fact]
    public async Task Un_descuento_ya_ajustado_conserva_lo_aplicado_si_el_saldo_no_cambio()
    {
        var d = ConSeisMesesAprobados();
        var motivo = new TerminationReason { Code = "RENUNCIA", Name = "Renuncia", IsSeeded = true, CreatedBy = "test" };
        d.Db.TerminationReasons.Add(motivo);
        var credito = new LoanPortfolio { PersonId = d.Ana.PersonId, PortfolioNumber = 9, CurrentBalance = 800_000m, DisbursementDate = new DateOnly(2026, 1, 1), CreatedBy = "test" };
        d.Db.LoanPortfolios.Add(credito);
        await d.Db.SaveChangesAsync();
        var terminacion = new EmploymentTermination { EmployeeId = d.Ana.Id, TerminationDate = new DateOnly(2026, 3, 20), TerminationReasonId = motivo.Id, CreatedBy = "test" };
        terminacion.Deductions.Add(new SettlementDeduction
        {
            Kind = SettlementDeductionKind.CooperativeLoan, LoanPortfolioId = credito.Id, Description = "Crédito 9", ProposedAmount = 800_000m, AppliedAmount = 300_000m,
            AdjustmentReason = "Acuerdo de pago por el resto", Status = SettlementDeductionStatus.Adjusted, CreatedBy = "test",
        });
        d.Db.EmploymentTerminations.Add(terminacion);
        await d.Db.SaveChangesAsync();

        var igual = await d.SettlementLoader.LoadAsync(SettlementLoadRequest.Definitiva(d.Ana.Id, terminacion.Id, new DateOnly(2026, 3, 20)), CancellationToken.None);
        var deuda = igual.Employees.Single().Deudas.Single();
        deuda.Applied.Should().Be(300_000m, "lo propuesto no cambió: el ajuste de la responsable se respeta");
        deuda.AdjustmentReason.Should().Be("Acuerdo de pago por el resto");
        igual.Warnings.Should().BeEmpty();

        credito.CurrentBalance = 750_000m;
        await d.Db.SaveChangesAsync();
        var cambio = await d.SettlementLoader.LoadAsync(SettlementLoadRequest.Definitiva(d.Ana.Id, terminacion.Id, new DateOnly(2026, 3, 20)), CancellationToken.None);
        cambio.Employees.Single().Deudas.Single().Applied.Should().Be(750_000m, "el saldo cambió: vuelve a proponerse y se avisa");
        cambio.Warnings.Should().ContainSingle(w => w.Code == "Payroll.Settlement.DeductionReproposed");
    }

    [Fact]
    public async Task La_prima_ya_pagada_en_una_definitiva_aprobada_del_semestre_se_informa()
    {
        var d = ConSeisMesesAprobados();
        var definitiva = new Domain.Entities.Payroll.Transactions.PayrollRun
        {
            Kind = PayrollRunKind.Settlement, CutoffDate = new DateOnly(2026, 4, 30), EmployeeId = d.Ana.Id, Version = 1, Status = PayrollRunStatus.Approved,
            CalculatedBy = "ana@demo", CalculatedAt = NominaTestData.Ahora, InputsHash = new string('c', 64), CreatedBy = "test",
        };
        var prima = await d.Db.PayrollConceptDefinitions.SingleAsync(c => c.Code == WellKnownConceptCodes.ServiceBonus);
        var fila = new Domain.Entities.Payroll.Transactions.PayrollRunEmployee { EmployeeId = d.Ana.Id, PayrollPlanId = d.Plan.Id, CreatedBy = "test" };
        fila.Lines.Add(new Domain.Entities.Payroll.Transactions.PayrollRunLine { ConceptDefinitionId = prima.Id, ConceptCode = prima.Code, ConceptName = prima.Name, Nature = ConceptNature.Earning, Amount = 749_698m, Quantity = 120m, ExplanationJson = "{}", CreatedBy = "test" });
        definitiva.Employees.Add(fila);
        d.Db.PayrollRuns.Add(definitiva);
        await d.Db.SaveChangesAsync();

        var batch = await d.SettlementLoader.LoadAsync(SettlementLoadRequest.Prima(2026, 1, [d.Ana.Id]), CancellationToken.None);

        var pagada = batch.Employees.Single().Input.ServiceBonusPaidInSettlements.Should().ContainSingle().Subject;
        pagada.RunPublicId.Should().Be(definitiva.PublicId);
        pagada.PaidThrough.Should().Be(new DateTime(2026, 4, 30));
        pagada.Amount.Should().Be(749_698m);
        pagada.Days.Should().Be(120);
        // La provisión de prima ya la consumió esa definitiva: el acumulado baja en lo pagado.
        batch.Employees.Single().Input.Provisions.Single(p => p.ProvisionConceptCode == WellKnownConceptCodes.ServiceBonusProvision).Accrued
            .Should().Be(6 * 187_424m - 749_698m);
    }

    [Fact]
    public async Task Las_cesantias_excluyen_al_retirado_con_definitiva_aprobada_en_el_anio()
    {
        var d = ConSeisMesesAprobados();
        var motivo = new TerminationReason { Code = "RENUNCIA", Name = "Renuncia", IsSeeded = true, CreatedBy = "test" };
        d.Db.TerminationReasons.Add(motivo);
        await d.Db.SaveChangesAsync();
        d.Db.EmploymentTerminations.Add(new EmploymentTermination { EmployeeId = d.Ana.Id, TerminationDate = new DateOnly(2026, 5, 31), TerminationReasonId = motivo.Id, Status = TerminationStatus.Settled, CreatedBy = "test" });
        var luis = d.Empleado("Luis", 1_800_000m, new DateTime(2026, 2, 1));
        await d.Db.SaveChangesAsync();

        var batch = await d.SettlementLoader.LoadAsync(SettlementLoadRequest.Cesantias(2026), CancellationToken.None);

        batch.Employees.Select(e => e.Employee.Id).Should().Equal(luis.Id);
        var excluida = batch.Excluded.Should().ContainSingle().Subject;
        excluida.EmployeePublicId.Should().Be(d.Ana.PublicId);
        excluida.ReasonCode.Should().Be("RetiradoConDefinitiva");
    }

    [Fact]
    public async Task Las_bases_por_mes_separan_variables_prestacionales_de_las_de_vacaciones()
    {
        var d = new NominaTestData();
        var defs = await d.Db.PayrollConceptDefinitions.ToDictionaryAsync(c => c.Id);
        var salario = defs.Values.Single(c => c.Code == "SALARIO");
        var comision = defs.Values.Single(c => c.Code == "COMISION");
        var extra = defs.Values.Single(c => c.Code == "HEX_DIURNA");
        var auxilio = defs.Values.Single(c => c.Code == "AUX_TRANSPORTE");

        var bases = SettlementInputLoader.BasesPorMes(
        [
            (2026, 1, salario.Id, salario.Code, ConceptNature.Earning, 2_000_000m),
            (2026, 1, auxilio.Id, auxilio.Code, ConceptNature.Earning, 249_095m),
            (2026, 1, comision.Id, comision.Code, ConceptNature.Earning, 300_000m),
            (2026, 1, extra.Id, extra.Code, ConceptNature.Earning, 80_000m),
            (2026, 1, salario.Id, "SALUD_EMP", ConceptNature.Deduction, 80_000m),
        ], defs);

        var enero = bases.Should().ContainSingle().Subject;
        enero.VariableBenefitsEarnings.Should().Be(380_000m, "comisión y extras son prestacionales; salario y auxilio no son variables");
        enero.VariableVacationEarnings.Should().Be(300_000m, "la comisión entra a la base de vacaciones; las extras no (art. 192)");
        enero.LaborIncome.Should().Be(2_629_095m, "todos los devengos, sin las deducciones");
    }
}
