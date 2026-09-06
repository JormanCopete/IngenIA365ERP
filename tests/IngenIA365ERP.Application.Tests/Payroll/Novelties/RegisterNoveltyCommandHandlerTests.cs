using FluentAssertions;
using IngenIA365ERP.Application.Payroll.Novelties.RegisterNovelty;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Payroll.Novelties;

/// <summary>T055 — FR-001..FR-003, FR-016: registrar novedades.</summary>
public class RegisterNoveltyCommandHandlerTests
{
    private static RegisterNoveltyCommandHandler Handler(NominaTestData d) =>
        new(d.Db, d.Clock, d.User, d.StaleMarker, d.CarryOver);

    private static RegisterNoveltyCommand Horas(NominaTestData d, decimal horas = 6m) => new()
    {
        PeriodPublicId = d.Marzo.PublicId,
        EmployeePublicId = d.Ana.PublicId,
        ConceptCode = "HEX_NOCTURNA",
        Quantity = horas,
        Notes = "Turno del 12",
    };

    [Fact]
    public async Task Registra_horas_extra_con_usuario_y_fecha()
    {
        var d = new NominaTestData();

        var r = await Handler(d).Handle(Horas(d), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var n = await d.Db.PayrollNovelties.SingleAsync();
        n.PublicId.Should().Be(r.Value);
        n.Status.Should().Be(NoveltyStatus.Active);
        n.Origin.Should().Be(NoveltyOrigin.Manual);
        n.Quantity.Should().Be(6m);
        n.DaysInPeriod.Should().Be(0);
        n.CreatedBy.Should().Be("ana@demo");
        n.CreatedAt.Should().Be(NominaTestData.Ahora);
        n.ConceptCode.Should().Be("HEX_NOCTURNA");
    }

    [Fact]
    public async Task Periodo_aprobado_se_niega_y_ofrece_el_siguiente_abierto()
    {
        var d = new NominaTestData();
        d.Marzo.Status = PayPeriodStatus.Approved;
        var abril = d.Periodo(new DateTime(2026, 4, 1), new DateTime(2026, 4, 30), PayPeriodStatus.Open);

        var r = await Handler(d).Handle(Horas(d), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Payroll.PeriodApproved");
        r.Error.Message.Should().Contain(abril.PublicId.ToString());
        d.Db.PayrollNovelties.Should().BeEmpty();
    }

    [Fact]
    public async Task Empleado_retirado_antes_del_periodo_no_es_vigente()
    {
        var d = new NominaTestData();
        var retirado = d.Empleado("Beto", 1_750_905m, new DateTime(2024, 1, 1), retiro: new DateTime(2026, 2, 10));

        var r = await Handler(d).Handle(Horas(d) with { EmployeePublicId = retirado.PublicId }, CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.EmployeeNotActiveInPeriod");
    }

    [Fact]
    public async Task Concepto_automatico_no_se_registra_como_novedad()
    {
        var d = new NominaTestData();

        var r = await Handler(d).Handle(Horas(d) with { ConceptCode = "SALUD_EMP", Quantity = null, Amount = 1000m }, CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.ConceptNotApplicable");
    }

    [Fact]
    public async Task Concepto_que_no_aplica_a_la_clase_del_empleado()
    {
        var d = new NominaTestData();
        var soloIntegrales = new Domain.Entities.Payroll.PayrollConceptDefinition
        {
            Code = "BONO_INTEGRAL", Name = "Bono integrales", Nature = ConceptNature.Earning, CalculationKind = CalculationKind.FixedAmount,
            RequiresAmount = true, ApplicableClasses = 1 << (int)EmployeeClass.IntegralSalary, ValidFrom = new DateTime(2026, 1, 1), IsActive = true,
        };
        d.Db.PayrollConceptDefinitions.Add(soloIntegrales);
        d.Db.SaveChanges();

        var r = await Handler(d).Handle(Horas(d) with { ConceptCode = "BONO_INTEGRAL", Quantity = null, Amount = 50_000m }, CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.ConceptNotApplicable");
    }

    [Fact]
    public async Task Concepto_que_exige_valor_lo_dice()
    {
        var d = new NominaTestData();

        var r = await Handler(d).Handle(Horas(d) with { ConceptCode = "COMISION", Quantity = 3m, Amount = null }, CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.ConceptRequiresAmount");
    }

    [Fact]
    public async Task Sin_repeticion_la_segunda_se_rechaza_y_muestra_la_existente()
    {
        var d = new NominaTestData();
        var maternidad = Horas(d) with { ConceptCode = "LIC_MATERNIDAD", Quantity = null, StartDate = new DateTime(2026, 3, 2), EndDate = new DateTime(2026, 3, 10) };

        var primera = await Handler(d).Handle(maternidad, CancellationToken.None);
        var segunda = await Handler(d).Handle(maternidad with { StartDate = new DateTime(2026, 3, 15), EndDate = new DateTime(2026, 3, 20) }, CancellationToken.None);

        primera.IsSuccess.Should().BeTrue(primera.Error.Message);
        segunda.Error.Code.Should().Be("Payroll.NoveltyDuplicate");
        segunda.Error.Message.Should().Contain(primera.Value.ToString());
    }

    [Fact]
    public async Task Incapacidad_que_cruza_el_periodo_calcula_dias_y_traslada_al_siguiente()
    {
        var d = new NominaTestData();
        var abril = d.Periodo(new DateTime(2026, 4, 1), new DateTime(2026, 4, 30), PayPeriodStatus.Open);
        var incapacidad = Horas(d) with { ConceptCode = "INCAP_GENERAL", Quantity = null, StartDate = new DateTime(2026, 3, 28), EndDate = new DateTime(2026, 4, 3) };

        var r = await Handler(d).Handle(incapacidad, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var origen = await d.Db.PayrollNovelties.SingleAsync(n => n.PublicId == r.Value);
        origen.DaysInPeriod.Should().Be(3, "28, 29 y 30 comerciales");
        origen.CarryOverDays.Should().Be(3, "1, 2 y 3 de abril");

        var traslado = await d.Db.PayrollNovelties.SingleAsync(n => n.CarriedFromNoveltyId == origen.Id);
        traslado.PayPeriodId.Should().Be(abril.Id);
        traslado.Origin.Should().Be(NoveltyOrigin.CarryOver);
        traslado.StartDate.Should().Be(new DateTime(2026, 4, 1));
        traslado.EndDate.Should().Be(new DateTime(2026, 4, 3));
        traslado.DaysInPeriod.Should().Be(3);
        traslado.CarryOverDays.Should().Be(0);
        traslado.ConceptCode.Should().Be("INCAP_GENERAL");
    }

    [Fact]
    public async Task Sin_periodo_siguiente_el_traslado_queda_pendiente_y_se_materializa_al_abrirlo()
    {
        var d = new NominaTestData();
        var incapacidad = Horas(d) with { ConceptCode = "INCAP_GENERAL", Quantity = null, StartDate = new DateTime(2026, 3, 28), EndDate = new DateTime(2026, 4, 3) };
        (await Handler(d).Handle(incapacidad, CancellationToken.None)).IsSuccess.Should().BeTrue();
        d.Db.PayrollNovelties.Count().Should().Be(1);

        var abril = d.Periodo(new DateTime(2026, 4, 1), new DateTime(2026, 4, 30), PayPeriodStatus.Open);
        var creadas = await d.CarryOver.MaterializePendingAsync(abril, CancellationToken.None);
        await d.Db.SaveChangesAsync();

        creadas.Should().Be(1);
        d.Db.PayrollNovelties.Count(n => n.PayPeriodId == abril.Id && n.Origin == NoveltyOrigin.CarryOver).Should().Be(1);
    }

    [Fact]
    public async Task Una_novedad_nueva_deja_el_borrador_desactualizado()
    {
        var d = new NominaTestData();
        var run = d.Borrador(d.Marzo);

        var r = await Handler(d).Handle(Horas(d), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        (await d.Db.PayrollRuns.SingleAsync(x => x.Id == run.Id)).Status.Should().Be(PayrollRunStatus.Stale);
    }

    [Fact]
    public void El_validador_exige_cantidad_valor_o_fechas()
    {
        var v = new RegisterNoveltyCommandValidator();
        var sinNada = new RegisterNoveltyCommand { PeriodPublicId = Guid.NewGuid(), EmployeePublicId = Guid.NewGuid(), ConceptCode = "X" };

        v.Validate(sinNada).IsValid.Should().BeFalse();
        v.Validate(sinNada with { Quantity = 1m }).IsValid.Should().BeTrue();
    }
}
