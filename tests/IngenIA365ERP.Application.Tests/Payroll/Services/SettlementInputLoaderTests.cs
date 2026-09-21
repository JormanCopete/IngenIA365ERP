using FluentAssertions;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Lending;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
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
    public async Task Un_empleado_sin_provision_acumulada_recibe_las_cuatro_provisiones_en_cero_y_el_motor_ajusta_toda_la_prima_al_gasto()
    {
        // Sin ninguna corrida aprobada ni saldo inicial: el saldo consultado es cero de verdad. Si el
        // lector no informara PROV_PRIMA, el motor omitiría el ajuste y el comprobante debitaría la
        // provisión por toda la prima (la e2e de la prima lo atrapó el 2026-09-21 con empleados
        // recién creados por otra prueba en la misma cooperativa).
        var d = new NominaTestData();

        var batch = await d.SettlementLoader.LoadAsync(SettlementLoadRequest.Prima(2026, 1), CancellationToken.None);

        var ana = batch.Employees.Should().ContainSingle(e => e.Employee.Id == d.Ana.Id).Subject;
        ana.Input.Provisions.Select(p => p.ProvisionConceptCode).Should().BeEquivalentTo(
            WellKnownConceptCodes.ServiceBonusProvision, WellKnownConceptCodes.SeveranceProvision,
            WellKnownConceptCodes.SeveranceInterestProvision, WellKnownConceptCodes.VacationProvision);
        ana.Input.Provisions.Should().OnlyContain(p => p.Accrued == 0m);

        var resultado = new SettlementCalculationEngine().Calculate(ana.Input);
        var prima = resultado.Lines.Single(l => l.Code == WellKnownConceptCodes.ServiceBonus);
        var ajuste = resultado.Lines.Should().ContainSingle(l => l.Code == WellKnownConceptCodes.ServiceBonusProvisionAdjustment).Subject;
        ajuste.Amount.Should().Be(prima.Amount, "sin provisión acumulada, toda la prima va al gasto y nada queda debitado en la provisión");
        resultado.Skips.Should().NotContain(k => k.ReasonCode == SettlementReasonCodes.SinProvisionInformada);
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
    public async Task El_ajuste_del_saldo_inicial_reemplaza_a_la_apertura_y_llega_como_un_solo_tramo_con_fecha_y_autor()
    {
        // La contadora digita el ajuste como el saldo COMPLETO corregido (así lo dicen el comando, la
        // pantalla y contracts/api.md §4): la apertura decía 2.000.000 de cesantías y eran 2.100.000.
        // Hasta el 2026-09-21 el cargador sumaba apertura más ajuste (4.100.000) y doblaba los días de
        // vacaciones; el motor y el lector de provisiones deben ver una sola fila, la vigente.
        var d = ConSeisMesesAprobados();
        var apertura = d.SaldoInicial(d.Ana, new DateOnly(2025, 12, 31), prima: 500_000m, cesantias: 2_000_000m, intereses: 240_000m, diasVacaciones: 10m, diasPrima: 80);
        d.Db.EmployeeBenefitOpeningBalances.Add(new EmployeeBenefitOpeningBalance
        {
            EmployeeId = d.Ana.Id, AsOfDate = new DateOnly(2025, 12, 31), Kind = OpeningBalanceKind.Adjustment, AdjustsBalanceId = apertura.Id,
            AccruedServiceBonus = 500_000m, AccruedSeverance = 2_100_000m, AccruedSeveranceInterest = 240_000m, PendingVacationDays = 10m,
            AdjustmentReason = "Faltó un mes", CreatedBy = "revisora@demo",
        });
        d.Politica(CompanyPolicyKeys.ArranqueNominaFecha, "2026-01-01", new DateOnly(2020, 1, 1));
        await d.Db.SaveChangesAsync();

        var batch = await d.SettlementLoader.LoadAsync(SettlementLoadRequest.Cesantias(2026, new DateOnly(2026, 6, 30)), CancellationToken.None);

        var input = batch.Employees.Single().Input;
        input.OpeningBalance.Should().NotBeNull();
        input.OpeningBalance!.AsOfDate.Should().Be(new DateTime(2025, 12, 31));
        input.OpeningBalance.AccruedSeverance.Should().Be(2_100_000m, "el ajuste es el saldo completo: reemplaza, no se suma");
        input.OpeningBalance.AccruedServiceBonus.Should().Be(500_000m);
        input.OpeningBalance.PendingVacationDays.Should().Be(10m, "los días tampoco se suman");
        input.OpeningBalance.ServiceBonusDaysAccrued.Should().Be(80, "el ajuste no trajo los días ya contados: se heredan de la apertura");
        input.OpeningBalance.EnteredBy.Should().Be("revisora@demo", "la fila vigente es la que se explica");
        input.Policies.PayrollStartDate.Should().Be(new DateTime(2026, 1, 1));
        // La provisión de cesantías incluye lo digitado en el saldo inicial (R3), una sola vez.
        input.Provisions.Single(p => p.ProvisionConceptCode == WellKnownConceptCodes.SeveranceProvision).Accrued.Should().Be(6 * 187_424m + 2_100_000m);
        input.Provisions.Single(p => p.ProvisionConceptCode == WellKnownConceptCodes.ServiceBonusProvision).Accrued.Should().Be(6 * 187_424m + 500_000m);
    }

    [Fact]
    public async Task Un_ajuste_con_corte_posterior_manda_sobre_la_apertura_y_los_movimientos_de_vacaciones_lo_ven_igual()
    {
        // Apertura al 31-12-2025 con 10 días; ajuste al 31-03-2026 que dice 12 días y 1.200.000 de
        // cesantías. La fila más reciente es el saldo, y el saldo de vacaciones (VacationBalanceCalculator)
        // usa la misma lectura que el motor.
        var d = ConSeisMesesAprobados();
        var apertura = d.SaldoInicial(d.Ana, new DateOnly(2025, 12, 31), cesantias: 1_000_000m, diasVacaciones: 10m);
        d.Db.EmployeeBenefitOpeningBalances.Add(new EmployeeBenefitOpeningBalance
        {
            EmployeeId = d.Ana.Id, AsOfDate = new DateOnly(2026, 3, 31), Kind = OpeningBalanceKind.Adjustment, AdjustsBalanceId = apertura.Id,
            AccruedSeverance = 1_200_000m, PendingVacationDays = 12m, AdjustmentReason = "Eran 12 días", CreatedBy = "contadora@demo",
        });
        await d.Db.SaveChangesAsync();

        var batch = await d.SettlementLoader.LoadAsync(SettlementLoadRequest.Cesantias(2026, new DateOnly(2026, 6, 30)), CancellationToken.None);
        var input = batch.Employees.Single().Input;
        input.OpeningBalance!.AsOfDate.Should().Be(new DateTime(2026, 3, 31));
        input.OpeningBalance.AccruedSeverance.Should().Be(1_200_000m);
        input.OpeningBalance.PendingVacationDays.Should().Be(12m);
        input.Provisions.Single(p => p.ProvisionConceptCode == WellKnownConceptCodes.SeveranceProvision).Accrued.Should().Be(6 * 187_424m + 1_200_000m);

        var saldos = await d.SaldosDeProvision.LeerAsync([d.Ana.Id], new DateOnly(2026, 6, 30), excludeRunId: null, CancellationToken.None);
        saldos[d.Ana.Id].Single(s => s.ProvisionCode == WellKnownConceptCodes.SeveranceProvision).OpeningBalance.Should().Be(1_200_000m);
        saldos[d.Ana.Id].Single(s => s.ProvisionCode == WellKnownConceptCodes.VacationProvision).OpeningBalance
            .Should().Be(12m * 2_000_000m / 30m, "los días vigentes al salario de la fecha del ajuste, no la suma de 10 + 12");
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
    public async Task Las_cuotas_causadas_de_una_libranza_siguen_la_regla_ApplyOn_de_la_recurrente_en_un_plan_quincenal()
    {
        // Plan quincenal; libranza mensual (LastOfMonth) de 12 cuotas desde enero; ocho corridas de fin
        // de mes la descontaron (InstallmentsIssued = 8). Retiro el 20/09: la novena cuota (septiembre)
        // es la única causada y no descontada. Hasta el 2026-09-21 se contaba cada quincena como una
        // cuota (18 → min 12) y se proponían cuatro: 600.000 de más al empleado.
        var d = new NominaTestData();
        var quincenal = new PayrollPlan { Code = "QUINC", Name = "Quincenal", Periodicity = PayrollPeriodicity.Biweekly, IsActive = true, CreatedBy = "test" };
        d.Db.PayrollPlans.Add(quincenal);
        await d.Db.SaveChangesAsync();
        for (var mes = 1; mes <= 9; mes++)
        {
            var fin = new DateTime(2026, mes, DateTime.DaysInMonth(2026, mes));
            var estado = mes < 9 ? PayPeriodStatus.Approved : PayPeriodStatus.Open;
            d.Periodo(new DateTime(2026, mes, 1), new DateTime(2026, mes, 15), estado, plan: quincenal);
            d.Periodo(new DateTime(2026, mes, 16), fin, estado, plan: quincenal);
        }
        var luis = d.Empleado("Luis", 2_000_000m, new DateTime(2025, 6, 1));
        luis.PayrollPlanId = quincenal.Id;
        var motivo = new TerminationReason { Code = "RENUNCIA", Name = "Renuncia", IsSeeded = true, CreatedBy = "test" };
        d.Db.TerminationReasons.Add(motivo);
        await d.Db.SaveChangesAsync();
        var retiro = new DateOnly(2026, 9, 20);
        var terminacion = new EmploymentTermination { EmployeeId = luis.Id, TerminationDate = retiro, TerminationReasonId = motivo.Id, CreatedBy = "test" };
        d.Db.EmploymentTerminations.Add(terminacion);
        d.Db.PayrollRecurringNovelties.AddRange(
            new PayrollRecurringNovelty
            {
                EmployeeId = luis.Id, ConceptCode = SettlementInputLoader.LibranzaCode, Amount = 200_000m, StartDate = new DateTime(2026, 1, 1),
                TotalInstallments = 12, InstallmentsIssued = 8, ApplyOn = RecurringApplyRule.LastOfMonth, Notes = "Mensual", CreatedBy = "test",
            },
            new PayrollRecurringNovelty
            {
                EmployeeId = luis.Id, ConceptCode = SettlementInputLoader.LibranzaCode, Amount = 50_000m, StartDate = new DateTime(2026, 7, 1),
                TotalInstallments = 24, InstallmentsIssued = 4, ApplyOn = RecurringApplyRule.EveryPeriod, Notes = "Quincenal", CreatedBy = "test",
            },
            new PayrollRecurringNovelty
            {
                EmployeeId = luis.Id, ConceptCode = SettlementInputLoader.LibranzaCode, Amount = 30_000m, StartDate = new DateTime(2026, 8, 1),
                TotalInstallments = 10, InstallmentsIssued = 1, ApplyOn = RecurringApplyRule.FirstOfMonth, Notes = "Primera quincena", CreatedBy = "test",
            });
        await d.Db.SaveChangesAsync();

        var batch = await d.SettlementLoader.LoadAsync(SettlementLoadRequest.Definitiva(luis.Id, terminacion.Id, retiro), CancellationToken.None);

        var deudas = batch.Employees.Single().Deudas;
        deudas.Single(x => x.Description.Contains("Mensual")).Proposed.Should().Be(200_000m,
            "nueve fines de mes (enero a septiembre) causan nueve cuotas y ocho se descontaron: queda la de septiembre");
        deudas.Single(x => x.Description.Contains("Quincenal")).Proposed.Should().Be(2 * 50_000m,
            "seis quincenas (julio a septiembre, incluida la 16–30/09 que contiene el retiro) menos cuatro descontadas");
        deudas.Single(x => x.Description.Contains("Primera quincena")).Proposed.Should().Be(30_000m,
            "dos primeras quincenas (agosto y septiembre) menos una descontada");
    }

    [Fact]
    public async Task En_modo_acumulado_el_cupo_anual_usado_suma_la_retencion_de_las_liquidaciones_especiales_aprobadas_del_anio()
    {
        // Prima de junio aprobada con RETEFTE_PRIMA que aplicó 5.000.000 de renta exenta; una
        // definitiva de otro año y una prima reversada no cuentan. La liquidación siguiente (cesantías
        // al 30/06, misma lectura para vacaciones y definitiva) debe partir de ese «usado» más el de la
        // ordinaria. Hasta el 2026-09-21 sólo se sumaba la ordinaria: el cupo de 790 UVT se excedía.
        var d = ConSeisMesesAprobados();
        d.Politica(CompanyPolicyKeys.RetefteTopesAnualesModo, CompanyPolicyKeys.RetefteTopesAnualesModoValores.Acumulado, new DateOnly(2020, 1, 1));
        // La ordinaria de marzo retuvo con 400.000 de exenta y 700.000 de depuración total.
        var marzo = await d.Db.PayrollRuns.Include(r => r.Employees).ThenInclude(re => re.Lines)
            .Where(r => r.PayPeriod!.StartDate == new DateTime(2026, 3, 1)).SingleAsync();
        var definiciones = await d.Db.PayrollConceptDefinitions.ToDictionaryAsync(c => c.Code);
        marzo.Employees.Single().Lines.Add(Retencion(definiciones[WellKnownConceptCodes.Withholding], 90_000m, exenta: 400_000m, total: 700_000m));
        EspecialAprobada(d, PayrollRunKind.ServiceBonus, new DateOnly(2026, 6, 30), d.Ana, Retencion(definiciones[WellKnownConceptCodes.WithholdingOnServiceBonus], 300_000m, exenta: 5_000_000m, total: 5_000_000m));
        EspecialAprobada(d, PayrollRunKind.ServiceBonus, new DateOnly(2025, 12, 31), d.Ana, Retencion(definiciones[WellKnownConceptCodes.WithholdingOnServiceBonus], 100_000m, exenta: 2_000_000m, total: 2_000_000m));
        EspecialAprobada(d, PayrollRunKind.Vacation, new DateOnly(2026, 4, 10), d.Ana, Retencion(definiciones[WellKnownConceptCodes.Withholding], 50_000m, exenta: 250_000m, total: 300_000m), status: PayrollRunStatus.Reversed);
        // La indemnización sólo resta la exenta (art. 401-3): consume del cupo global lo mismo.
        var sinTotal = Retencion(definiciones[WellKnownConceptCodes.WithholdingOnIndemnity], 800_000m, exenta: 1_000_000m, total: null);
        EspecialAprobada(d, PayrollRunKind.Settlement, new DateOnly(2026, 5, 20), d.Ana, sinTotal);
        await d.Db.SaveChangesAsync();

        var batch = await d.SettlementLoader.LoadAsync(SettlementLoadRequest.Cesantias(2026, new DateOnly(2026, 6, 30)), CancellationToken.None);

        var ytd = batch.Employees.Single().Input.WithholdingYearToDate;
        ytd.Should().NotBeNull();
        ytd!.RentaExentaUsada.Should().Be(400_000m + 5_000_000m + 1_000_000m, "ordinaria de marzo + prima de junio + indemnización de mayo; no la prima de 2025 ni la reversada");
        ytd.DeduccionesYExentasUsadas.Should().Be(700_000m + 5_000_000m + 1_000_000m);
    }

    private static PayrollRunLine Retencion(PayrollConceptDefinition def, decimal valor, decimal exenta, decimal? total)
    {
        var exp = new Explanation { Form = "Tabla por rangos" };
        exp.Steps.Add(new ExplanationStep("Ingreso gravado del período", 10_000_000m, null));
        exp.Steps.Add(new ExplanationStep("Renta exenta del 25,00 % sobre 9.000.000 (RETEFTE_EXENTA_PCT)", exenta, null));
        if (total is { } t) exp.Steps.Add(new ExplanationStep("Total de deducciones y rentas exentas", t, null));
        exp.Steps.Add(new ExplanationStep("Base de retención depurada", 4_000_000m, null));
        return new PayrollRunLine
        {
            ConceptDefinitionId = def.Id, ConceptCode = def.Code, ConceptName = def.Name, Nature = ConceptNature.Deduction, Amount = valor,
            ExplanationJson = System.Text.Json.JsonSerializer.Serialize(exp, IngenIA365ERP.Application.Payroll.Runs.RunJson.Options), CreatedBy = "test",
        };
    }

    private static void EspecialAprobada(NominaTestData d, PayrollRunKind kind, DateOnly corte, Employee e, PayrollRunLine linea, PayrollRunStatus status = PayrollRunStatus.Approved)
    {
        var run = new PayrollRun
        {
            Kind = kind, CutoffDate = corte, Version = 1, Status = status, EmployeeId = kind is PayrollRunKind.Vacation or PayrollRunKind.Settlement ? e.Id : null,
            CalculatedAt = corte.ToDateTime(TimeOnly.MinValue), CalculatedBy = "ana@demo", ApprovedAt = corte.ToDateTime(TimeOnly.MinValue), ApprovedBy = "contadora@demo",
            InputsHash = new string('c', 64), CreatedBy = "test",
        };
        var fila = new PayrollRunEmployee { EmployeeId = e.Id, PayrollPlanId = e.PayrollPlanId, DaysWorked = 30, EmployeeClass = e.EmployeeClass, CreatedBy = "test" };
        fila.Lines.Add(linea);
        run.Employees.Add(fila);
        d.Db.PayrollRuns.Add(run);
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

    /// <summary>
    /// D-30: una terminación registrada (definitiva en borrador) también saca al empleado de la prima
    /// semestral y de las cesantías anuales cuyo corte cae en o después del retiro: la definitiva paga
    /// esos rubros al retirarse. Hasta la revisión de N1 sólo contaba la Settled, y la corrida colectiva
    /// aprobada entre el registro y la aprobación de la definitiva pagaba la prima o las cesantías dos veces.
    /// </summary>
    [Fact]
    public async Task La_prima_y_las_cesantias_excluyen_al_retirado_con_definitiva_registrada_en_borrador()
    {
        var d = ConSeisMesesAprobados();
        var motivo = new TerminationReason { Code = "RENUNCIA", Name = "Renuncia", IsSeeded = true, CreatedBy = "test" };
        d.Db.TerminationReasons.Add(motivo);
        await d.Db.SaveChangesAsync();
        // Ana: retiro registrado el 25 de junio, definitiva en borrador; la ficha sigue abierta a propósito.
        d.Db.EmploymentTerminations.Add(new EmploymentTermination { EmployeeId = d.Ana.Id, TerminationDate = new DateOnly(2026, 6, 25), TerminationReasonId = motivo.Id, Status = TerminationStatus.Registered, CreatedBy = "test" });
        // Luis: retiro registrado en julio: la prima del primer semestre y las cesantías al 30 de junio lo incluyen completo.
        var luis = d.Empleado("Luis", 1_800_000m, new DateTime(2026, 2, 1));
        d.Db.EmploymentTerminations.Add(new EmploymentTermination { EmployeeId = luis.Id, TerminationDate = new DateOnly(2026, 7, 10), TerminationReasonId = motivo.Id, Status = TerminationStatus.Registered, CreatedBy = "test" });
        await d.Db.SaveChangesAsync();

        var prima = await d.SettlementLoader.LoadAsync(SettlementLoadRequest.Prima(2026, 1), CancellationToken.None);
        var cesantias = await d.SettlementLoader.LoadAsync(SettlementLoadRequest.Cesantias(2026, new DateOnly(2026, 6, 30)), CancellationToken.None);

        prima.Employees.Select(e => e.Employee.Id).Should().Equal(luis.Id);
        var sinPrima = prima.Excluded.Should().ContainSingle().Subject;
        sinPrima.EmployeePublicId.Should().Be(d.Ana.PublicId);
        sinPrima.ReasonCode.Should().Be(SettlementReasonCodes.YaPagadaEnDefinitiva);
        sinPrima.Reason.Should().Contain("borrador");
        prima.Employees.Single().Input.Employee.TerminationDate.Should().Be(new DateTime(2026, 7, 10), "el retiro registrado viaja al motor aunque caiga después del corte");

        cesantias.Employees.Select(e => e.Employee.Id).Should().Equal(luis.Id);
        var sinCesantias = cesantias.Excluded.Should().ContainSingle().Subject;
        sinCesantias.EmployeePublicId.Should().Be(d.Ana.PublicId);
        sinCesantias.ReasonCode.Should().Be("RetiradoConDefinitiva");
        sinCesantias.Reason.Should().Contain("borrador");
    }

    /// <summary>
    /// D-30, el otro sentido: la definitiva registrada después de aprobar la prima semestral o la corrida
    /// anual de cesantías del mismo período recibe lo ya pagado, con quién lo pagó, para omitir el rubro o
    /// liquidar sólo los días posteriores al corte. Una prima semestral no se informa a la propia corrida
    /// semestral: ahí sólo cuentan las definitivas (FR-009).
    /// </summary>
    [Fact]
    public async Task La_definitiva_recibe_la_prima_de_la_semestral_y_las_cesantias_de_la_anual_ya_aprobadas()
    {
        var d = ConSeisMesesAprobados();
        var conceptos = await d.Db.PayrollConceptDefinitions.ToDictionaryAsync(c => c.Code);
        var semestral = CorridaAprobada(d, PayrollRunKind.ServiceBonus, new DateOnly(2026, 6, 30), conceptos[WellKnownConceptCodes.ServiceBonus], 1_124_547.50m, 180m, year: 2026, semester: 1);
        var anual = CorridaAprobada(d, PayrollRunKind.Severance, new DateOnly(2026, 5, 31), conceptos[WellKnownConceptCodes.Severance], 937_123m, 150m, year: 2026);
        var motivo = new TerminationReason { Code = "RENUNCIA", Name = "Renuncia", IsSeeded = true, CreatedBy = "test" };
        d.Db.TerminationReasons.Add(motivo);
        await d.Db.SaveChangesAsync();
        var retiro = new DateOnly(2026, 6, 25);
        var terminacion = new EmploymentTermination { EmployeeId = d.Ana.Id, TerminationDate = retiro, TerminationReasonId = motivo.Id, CreatedBy = "test" };
        d.Db.EmploymentTerminations.Add(terminacion);
        await d.Db.SaveChangesAsync();

        var definitiva = await d.SettlementLoader.LoadAsync(SettlementLoadRequest.Definitiva(d.Ana.Id, terminacion.Id, retiro), CancellationToken.None);
        var prima = await d.SettlementLoader.LoadAsync(SettlementLoadRequest.Prima(2026, 1, [d.Ana.Id]), CancellationToken.None);

        var input = definitiva.Employees.Single().Input;
        var primaPagada = input.ServiceBonusPaidInSettlements.Should().ContainSingle().Subject;
        primaPagada.RunPublicId.Should().Be(semestral.PublicId);
        primaPagada.PaidBy.Should().Be(SettlementKind.ServiceBonus);
        primaPagada.PaidThrough.Should().Be(new DateTime(2026, 6, 30));
        primaPagada.Amount.Should().Be(1_124_547.50m);
        primaPagada.Days.Should().Be(180);
        var cesantiasPagadas = input.SeverancePaidInRuns.Should().ContainSingle().Subject;
        cesantiasPagadas.RunPublicId.Should().Be(anual.PublicId);
        cesantiasPagadas.PaidThrough.Should().Be(new DateTime(2026, 5, 31));
        cesantiasPagadas.Amount.Should().Be(937_123m);

        // Recalcular la semestral con la terminación ya registrada: Ana queda fuera (su definitiva paga), y la
        // prima semestral aprobada no se informa a la propia semestral: ahí sólo cuentan las definitivas (FR-009).
        prima.Employees.Should().BeEmpty();
        prima.Excluded.Should().ContainSingle(x => x.EmployeePublicId == d.Ana.PublicId && x.ReasonCode == SettlementReasonCodes.YaPagadaEnDefinitiva);
    }

    /// <summary>
    /// D-30: la definitiva trae las novedades activas del empleado en el período abierto donde cae el
    /// retiro —devengadas y deducciones con su concepto— porque la nómina ordinaria de ese período ya no
    /// lo incluye. Quedan fuera las informativas (ya viajan como ausencias), la cuota de una libranza
    /// recurrente (va como deuda propuesta, FR-018a) y lo anulado.
    /// </summary>
    [Fact]
    public async Task La_definitiva_trae_las_novedades_activas_del_periodo_pendiente_con_su_id_interno()
    {
        var d = ConSeisMesesAprobados();
        var julio = d.Periodo(new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), PayPeriodStatus.Open);
        var conceptos = await d.Db.PayrollConceptDefinitions.ToDictionaryAsync(c => c.Code);
        var junio = await d.Db.PayPeriods.SingleAsync(p => p.StartDate == new DateTime(2026, 6, 1));
        var recurrente = new PayrollRecurringNovelty { EmployeeId = d.Ana.Id, ConceptCode = SettlementInputLoader.LibranzaCode, Amount = 50_000m, StartDate = new DateTime(2026, 6, 1), TotalInstallments = 12, InstallmentsIssued = 1, CreatedBy = "test" };
        d.Db.PayrollRecurringNovelties.Add(recurrente);
        await d.Db.SaveChangesAsync();
        var extras = new PayrollNovelty { PayPeriodId = julio.Id, EmployeeId = d.Ana.Id, ConceptDefinitionId = conceptos["HEX_DIURNA"].Id, ConceptCode = "HEX_DIURNA", Quantity = 10m, Notes = "Cierre de mes", CreatedBy = "test" };
        var embargo = new PayrollNovelty { PayPeriodId = julio.Id, EmployeeId = d.Ana.Id, ConceptDefinitionId = conceptos["EMBARGO"].Id, ConceptCode = "EMBARGO", Amount = 150_000m, CreatedBy = "test" };
        d.Db.PayrollNovelties.AddRange(
            extras, embargo,
            new PayrollNovelty { PayPeriodId = julio.Id, EmployeeId = d.Ana.Id, ConceptDefinitionId = conceptos["LIC_NO_REMUNERADA"].Id, ConceptCode = "LIC_NO_REMUNERADA", StartDate = new DateTime(2026, 7, 6), EndDate = new DateTime(2026, 7, 8), CreatedBy = "test" },
            new PayrollNovelty { PayPeriodId = julio.Id, EmployeeId = d.Ana.Id, ConceptDefinitionId = conceptos["LIBRANZA"].Id, ConceptCode = "LIBRANZA", Amount = 50_000m, RecurringNoveltyId = recurrente.Id, Origin = NoveltyOrigin.Recurring, CreatedBy = "test" },
            new PayrollNovelty { PayPeriodId = julio.Id, EmployeeId = d.Ana.Id, ConceptDefinitionId = conceptos["HEX_NOCTURNA"].Id, ConceptCode = "HEX_NOCTURNA", Quantity = 4m, Status = NoveltyStatus.Cancelled, CreatedBy = "test" },
            new PayrollNovelty { PayPeriodId = junio.Id, EmployeeId = d.Ana.Id, ConceptDefinitionId = conceptos["HEX_DIURNA"].Id, ConceptCode = "HEX_DIURNA", Quantity = 3m, CreatedBy = "test" });
        var motivo = new TerminationReason { Code = "RENUNCIA", Name = "Renuncia", IsSeeded = true, CreatedBy = "test" };
        d.Db.TerminationReasons.Add(motivo);
        await d.Db.SaveChangesAsync();
        var retiro = new DateOnly(2026, 7, 15);
        var terminacion = new EmploymentTermination { EmployeeId = d.Ana.Id, TerminationDate = retiro, TerminationReasonId = motivo.Id, CreatedBy = "test" };
        d.Db.EmploymentTerminations.Add(terminacion);
        await d.Db.SaveChangesAsync();

        var batch = await d.SettlementLoader.LoadAsync(SettlementLoadRequest.Definitiva(d.Ana.Id, terminacion.Id, retiro), CancellationToken.None);

        var input = batch.Employees.Single().Input;
        input.PendingSalary.Should().Be(new PendingSalaryInput(julio.StartDate, julio.EndDate));
        input.PendingNovelties.Select(n => n.ConceptCode).Should().BeEquivalentTo(["HEX_DIURNA", "EMBARGO"],
            "la informativa va como ausencia, la cuota de libranza como deuda, la anulada no existe y la de junio ya la pagó la ordinaria");
        input.PendingNovelties.Single(n => n.ConceptCode == "HEX_DIURNA").Should().BeEquivalentTo(new { extras.PublicId, Quantity = 10m, Description = "Hora extra diurna: Cierre de mes" });
        input.PendingNovelties.Single(n => n.ConceptCode == "EMBARGO").Amount.Should().Be(150_000m);
        input.Absences.Should().ContainSingle(a => a.Description == "LIC_NO_REMUNERADA");
        batch.NoveltyIds.Should().BeEquivalentTo(new Dictionary<Guid, int> { [extras.PublicId] = extras.Id, [embargo.PublicId] = embargo.Id },
            "la línea de la corrida se enlaza a la novedad por su id interno");
    }

    private static Domain.Entities.Payroll.Transactions.PayrollRun CorridaAprobada(NominaTestData d, PayrollRunKind kind, DateOnly corte, PayrollConceptDefinition concepto,
        decimal valor, decimal dias, int year, int? semester = null)
    {
        var run = new Domain.Entities.Payroll.Transactions.PayrollRun
        {
            Kind = kind, CutoffDate = corte, Year = (short)year, Semester = (byte?)semester, Version = 1, Status = PayrollRunStatus.Approved,
            CalculatedBy = "ana@demo", CalculatedAt = NominaTestData.Ahora, InputsHash = new string('c', 64), CreatedBy = "test",
        };
        var fila = new Domain.Entities.Payroll.Transactions.PayrollRunEmployee { EmployeeId = d.Ana.Id, PayrollPlanId = d.Plan.Id, CreatedBy = "test" };
        fila.Lines.Add(new Domain.Entities.Payroll.Transactions.PayrollRunLine { ConceptDefinitionId = concepto.Id, ConceptCode = concepto.Code, ConceptName = concepto.Name, Nature = ConceptNature.Earning, Amount = valor, Quantity = dias, ExplanationJson = "{}", CreatedBy = "test" });
        run.Employees.Add(fila);
        d.Db.PayrollRuns.Add(run);
        return run;
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
