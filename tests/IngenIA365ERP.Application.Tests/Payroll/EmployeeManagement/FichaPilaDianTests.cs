using FluentAssertions;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Commands.RegisterEmployee;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Commands.UpdateEmployee;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Contracts;
using IngenIA365ERP.Application.Payroll.EmployeeManagement.Queries;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Payroll.EmployeeManagement;

/// <summary>
/// T036 (feature 010, contracts/api.md §12): la ficha guarda lo que la PILA y la DIAN necesitan,
/// un bloque que no viene no borra lo que había, el aprendiz exige su etapa, y el GET devuelve
/// los bloques, el banco de dispersión, el saldo inicial, el porcentaje P2 vigente y la
/// terminación viva.
/// </summary>
public class FichaPilaDianTests
{
    private readonly NominaTestData _d = new();

    private UpdateEmployeeCommand Basico() => new()
    {
        EmployeePublicId = _d.Ana.PublicId, BaseSalary = _d.Ana.Salary, ContractType = 1,
    };

    private Task<IngenIA365ERP.Application.Common.Models.Result> ActualizarAsync(UpdateEmployeeCommand c) =>
        new UpdateEmployeeCommandHandler(_d.Db, _d.Clock, _d.User).Handle(c, CancellationToken.None);

    private Bank Banco()
    {
        var b = new Bank { Name = "AV Villas", TransferCode = "52", LegacyCode = "AVV", CreatedBy = "seed" };
        _d.Db.Banks.Add(b);
        _d.Db.SaveChanges();
        return b;
    }

    [Fact]
    public async Task Los_bloques_PILA_y_DIAN_se_guardan_y_el_DIVIPOLA_se_junta_en_el_codigo_DANE()
    {
        var banco = Banco();

        var r = await ActualizarAsync(Basico() with
        {
            Pila = new PilaEmployeeInput("01", "00", "76", "001", "9499", "CT01", "V", false, false, PensionTransitionRegime.Yes, true),
            Dian = new DianEmployeeInput(ContractTypeDian: DianContractType.Indefinite, PaymentMethodCode: "47", WorkAddress: "Cra 5 # 10-20"),
            DisbursementBankPublicId = banco.PublicId,
        });

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var e = await _d.Db.Employees.AsNoTracking().SingleAsync(x => x.Id == _d.Ana.Id);
        e.PilaContributorType.Should().Be("01");
        e.PilaContributorSubType.Should().Be("00");
        e.WorkMunicipalityDaneCode.Should().Be("76001");
        e.EconomicActivityCode.Should().Be("9499");
        e.WorkCenterCode.Should().Be("CT01");
        e.SalaryType.Should().Be(1, "V = variable");
        e.PensionTransitionRegime.Should().Be(PensionTransitionRegime.Yes);
        e.HighRiskPension.Should().BeTrue();
        e.DianContractType.Should().Be(DianContractType.Indefinite);
        e.DianPaymentMethodCode.Should().Be("47");
        e.WorkAddress.Should().Be("Cra 5 # 10-20");
        e.DisbursementBankId.Should().Be(banco.Id);
    }

    [Fact]
    public async Task Un_PUT_sin_los_bloques_no_borra_lo_que_habia()
    {
        var banco = Banco();
        await ActualizarAsync(Basico() with
        {
            Pila = new PilaEmployeeInput("01", "00", "76", "001", PensionTransitionRegime: PensionTransitionRegime.No),
            Dian = new DianEmployeeInput(ContractTypeDian: DianContractType.FixedTerm),
            DisbursementBankPublicId = banco.PublicId,
        });

        var r = await ActualizarAsync(Basico() with { BaseSalary = 2_100_000m });

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var e = await _d.Db.Employees.AsNoTracking().SingleAsync(x => x.Id == _d.Ana.Id);
        e.Salary.Should().Be(2_100_000m);
        e.WorkMunicipalityDaneCode.Should().Be("76001", "la pantalla vieja manda sólo lo laboral");
        e.PensionTransitionRegime.Should().Be(PensionTransitionRegime.No);
        e.DianContractType.Should().Be(DianContractType.FixedTerm);
        e.DisbursementBankId.Should().Be(banco.Id);

        var quitar = await ActualizarAsync(Basico() with { ClearDisbursementBank = true });
        quitar.IsSuccess.Should().BeTrue(quitar.Error.Message);
        (await _d.Db.Employees.AsNoTracking().SingleAsync(x => x.Id == _d.Ana.Id)).DisbursementBankId.Should().BeNull();
    }

    [Fact]
    public async Task El_bloque_DIAN_solo_llena_tipo_y_subtipo_cuando_PILA_no_los_trae()
    {
        var r = await ActualizarAsync(Basico() with
        {
            Pila = new PilaEmployeeInput(ContributorType: "51"),
            Dian = new DianEmployeeInput(WorkerType: "01", WorkerSubtype: "01"),
        });

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var e = await _d.Db.Employees.AsNoTracking().SingleAsync(x => x.Id == _d.Ana.Id);
        e.PilaContributorType.Should().Be("51", "D-05: una sola columna; manda PILA");
        e.PilaContributorSubType.Should().Be("01", "PILA no lo trajo: entra el de DIAN");
    }

    [Fact]
    public async Task Un_aprendiz_sin_etapa_se_rechaza_y_con_etapa_se_guarda()
    {
        var aprendiz = _d.Empleado("Aprendiz", 1_000_000m, new DateTime(2026, 1, 15), EmployeeClass.Apprentice);
        var comando = new UpdateEmployeeCommand { EmployeePublicId = aprendiz.PublicId, BaseSalary = 1_000_000m, ContractType = 5 };

        var sinEtapa = await ActualizarAsync(comando);
        sinEtapa.Error.Code.Should().Be("Payroll.Employee.ApprenticeStageRequired");

        var conEtapa = await ActualizarAsync(comando with { ApprenticeStage = ApprenticeStage.Lective });
        conEtapa.IsSuccess.Should().BeTrue(conEtapa.Error.Message);
        (await _d.Db.Employees.AsNoTracking().SingleAsync(x => x.Id == aprendiz.Id)).ApprenticeStage.Should().Be(ApprenticeStage.Lective);
    }

    [Fact]
    public async Task La_etapa_se_quita_con_ClearApprenticeStage_y_no_con_el_nulo_a_secas()
    {
        // D-29 (revisión de N1, pantallas #7): «un bloque que no viene no toca lo que había» vale también
        // para la etapa, así que la ficha necesita una orden explícita para dejarla en blanco cuando la
        // persona dejó de ser aprendiz. Hasta entonces la X del desplegable notificaba éxito sin borrar nada.
        var exAprendiz = _d.Empleado("ExAprendiz", 1_000_000m, new DateTime(2026, 1, 15), EmployeeClass.Apprentice);
        var comando = new UpdateEmployeeCommand { EmployeePublicId = exAprendiz.PublicId, BaseSalary = 1_000_000m, ContractType = 5 };
        (await ActualizarAsync(comando with { ApprenticeStage = ApprenticeStage.Lective })).IsSuccess.Should().BeTrue();

        // Sigue siendo aprendiz: quitarla se rechaza igual que faltar.
        var todaviaAprendiz = await ActualizarAsync(comando with { ClearApprenticeStage = true });
        todaviaAprendiz.Error.Code.Should().Be("Payroll.Employee.ApprenticeStageRequired");
        (await _d.Db.Employees.AsNoTracking().SingleAsync(x => x.Id == exAprendiz.Id)).ApprenticeStage.Should().Be(ApprenticeStage.Lective);

        // Pasó a estándar («Retención y plan» cambia la clase sin tocar la etapa).
        var e = await _d.Db.Employees.SingleAsync(x => x.Id == exAprendiz.Id);
        e.EmployeeClass = EmployeeClass.Standard;
        await _d.Db.SaveChangesAsync();

        var nuloASecas = await ActualizarAsync(comando);
        nuloASecas.IsSuccess.Should().BeTrue(nuloASecas.Error.Message);
        (await _d.Db.Employees.AsNoTracking().SingleAsync(x => x.Id == exAprendiz.Id)).ApprenticeStage
            .Should().Be(ApprenticeStage.Lective, "el nulo no toca lo que había (contracts/api.md §12)");

        var quitar = await ActualizarAsync(comando with { ClearApprenticeStage = true });
        quitar.IsSuccess.Should().BeTrue(quitar.Error.Message);
        (await _d.Db.Employees.AsNoTracking().SingleAsync(x => x.Id == exAprendiz.Id)).ApprenticeStage.Should().BeNull();
    }

    [Fact]
    public void El_validador_exige_el_formato_de_cada_codigo_y_el_DIVIPOLA_completo()
    {
        var v = new UpdateEmployeeCommandValidator();
        v.Validate(Basico() with { Pila = new PilaEmployeeInput(ContributorType: "1") }).IsValid.Should().BeFalse("dos dígitos");
        v.Validate(Basico() with { Pila = new PilaEmployeeInput(DivipolaDepartment: "76") }).IsValid.Should().BeFalse("departamento sin municipio");
        v.Validate(Basico() with { Pila = new PilaEmployeeInput(SalaryTypeCode: "Z") }).IsValid.Should().BeFalse("F, V o X");
        v.Validate(Basico() with { Dian = new DianEmployeeInput(ContractTypeDian: (DianContractType)9) }).IsValid.Should().BeFalse("1..5");
        v.Validate(Basico() with { Pila = new PilaEmployeeInput("01", "00", "76", "001", "9499", "CT01", "F"), Dian = new DianEmployeeInput(ContractTypeDian: DianContractType.WorkOrLabor, PaymentMethodCode: "47") }).IsValid.Should().BeTrue();
        new EmployeeInputValidator().Validate(new EmployeeInput { BaseSalary = 1m, ContractType = 1, HireDate = new DateTime(2026, 1, 1), Pila = new PilaEmployeeInput(EconomicActivityCode: "ABC") }).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task El_alta_guarda_los_bloques_y_sin_banco_de_dispersion_toma_el_de_la_cuenta_de_nomina()
    {
        var banco = Banco();
        var persona = new Person { FirstName = "Nuevo", LastName = "Empleado", TaxId = "900", CreatedBy = "test" };
        _d.Db.People.Add(persona);
        await _d.Db.SaveChangesAsync();

        var r = await new RegisterEmployeeCommandHandler(_d.Db, new Application.Payroll.EmployeeManagement.Services.EmployeeRegistrar(_d.Db, _d.Clock, _d.User))
            .Handle(new RegisterEmployeeCommand
            {
                PersonPublicId = persona.PublicId, BaseSalary = 1_500_000m, ContractType = 1, HireDate = new DateTime(2026, 3, 1),
                PayrollBankPublicId = banco.PublicId, PayrollBankAccountNumber = "123", PayrollBankAccountType = 1,
                Pila = new PilaEmployeeInput("01", "00", "05", "001", SalaryTypeCode: "F", PensionTransitionRegime: PensionTransitionRegime.No),
                Dian = new DianEmployeeInput(ContractTypeDian: DianContractType.Indefinite),
            }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var e = await _d.Db.Employees.AsNoTracking().SingleAsync(x => x.PublicId == r.Value);
        e.WorkMunicipalityDaneCode.Should().Be("05001");
        e.DianContractType.Should().Be(DianContractType.Indefinite);
        e.DisbursementBankId.Should().Be(banco.Id, "sin banco de dispersión explícito, el de la banca de nómina");
    }

    [Fact]
    public async Task El_GET_devuelve_los_bloques_el_banco_el_saldo_inicial_el_porcentaje_vigente_y_la_terminacion()
    {
        var banco = Banco();
        await ActualizarAsync(Basico() with
        {
            Pila = new PilaEmployeeInput("01", "00", "76", "001", "9499", "CT01", "F", PensionTransitionRegime: PensionTransitionRegime.Yes),
            Dian = new DianEmployeeInput(ContractTypeDian: DianContractType.Indefinite, PaymentMethodCode: "47"),
            DisbursementBankPublicId = banco.PublicId,
        });
        _d.Db.EmployeeBenefitOpeningBalances.Add(new EmployeeBenefitOpeningBalance
        {
            EmployeeId = _d.Ana.Id, AsOfDate = new DateOnly(2025, 12, 31), PendingVacationDays = 7.5m, AccruedSeverance = 1_000_000m, CreatedBy = "contadora",
        });
        _d.Db.EmployeeWithholdingRates.Add(new EmployeeWithholdingRate
        {
            EmployeeId = _d.Ana.Id, RatePercent = 3.5m, ValidFrom = new DateTime(2026, 1, 1), ValidTo = new DateTime(2026, 6, 30), Origin = WithholdingRateOrigin.Calculated, CreatedBy = "test",
        });
        var motivo = new TerminationReason { Code = "RENUNCIA", Name = "Renuncia voluntaria", GeneratesSeverancePay = false, IsSeeded = true, CreatedBy = "seed" };
        _d.Db.TerminationReasons.Add(motivo);
        await _d.Db.SaveChangesAsync();
        _d.Db.EmploymentTerminations.Add(new EmploymentTermination
        {
            EmployeeId = _d.Ana.Id, TerminationDate = new DateOnly(2026, 4, 30), TerminationReasonId = motivo.Id, Status = TerminationStatus.Registered, CreatedBy = "test",
        });
        await _d.Db.SaveChangesAsync();

        var r = await new GetEmployeeByIdQueryHandler(_d.Db, _d.Clock).Handle(new GetEmployeeByIdQuery(_d.Ana.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var dto = r.Value;
        dto.EmployeeClass.Should().Be("Standard");
        dto.Pila!.DivipolaDepartment.Should().Be("76");
        dto.Pila.DivipolaMunicipality.Should().Be("001");
        dto.Pila.SalaryTypeCode.Should().Be("F");
        dto.Pila.PensionTransitionRegime.Should().Be(PensionTransitionRegime.Yes);
        dto.Dian!.WorkerType.Should().Be("01", "D-05: el tipo de trabajador DIAN es el cotizante PILA");
        dto.Dian.ContractTypeDian.Should().Be(DianContractType.Indefinite);
        dto.DisbursementBankPublicId.Should().Be(banco.PublicId);
        dto.DisbursementBankTransferCode.Should().Be("52");
        dto.OpeningBalance!.PendingVacationDays.Should().Be(7.5m);
        dto.OpeningBalance.EnteredBy.Should().Be("contadora");
        dto.CurrentWithholdingRate!.RatePercent.Should().Be(3.5m);
        dto.CurrentWithholdingRate.Origin.Should().Be(WithholdingRateOrigin.Calculated);
        dto.Termination!.ReasonName.Should().Be("Renuncia voluntaria");
        dto.Termination.Status.Should().Be(TerminationStatus.Registered);
        // US4: saldo derivado al día de hoy (20-03-2026): 7,5 del saldo inicial al 31-12-2025 + 80 días comerciales de 2026 × 15 / 360.
        dto.VacationBalance.Should().NotBeNull();
        dto.VacationBalance!.AsOf.Should().Be(new DateOnly(2026, 3, 20));
        dto.VacationBalance.PendingDays.Should().BeApproximately(7.5m + 80m * 15m / 360m, 0.0001m);
    }
}
