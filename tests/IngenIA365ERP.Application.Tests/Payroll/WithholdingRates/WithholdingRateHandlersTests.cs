using FluentAssertions;
using IngenIA365ERP.Application.Payroll.EmployeeTax;
using IngenIA365ERP.Application.Payroll.WithholdingRates;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Policies;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.WithholdingRates;

/// <summary>El escenario de la US7: un empleado en procedimiento 2 con doce meses aprobados, prima en junio y cesantías en febrero.</summary>
public sealed class RetencionDePrueba
{
    public NominaTestData D { get; } = new();
    public Employee J { get; }

    public RetencionDePrueba(int meses = 12, byte procedimiento = 2)
    {
        J = D.Empleado("Juan", 12_000_000m, meses == 12 ? new DateTime(2020, 1, 1) : new DateTime(2026, 4, 1), procedimientoRetencion: procedimiento);
        var inicio = new DateTime(2025, 12, 1).AddMonths(12 - meses);
        for (var i = 0; i < meses; i++)
        {
            var mes = inicio.AddMonths(i);
            D.CorridaAprobada(mes.Year, mes.Month, J,
                new("SALARIO", ConceptNature.Earning, 12_000_000m, 30m),
                new("SALUD_EMP", ConceptNature.Deduction, 480_000m), new("PENSION_EMP", ConceptNature.Deduction, 480_000m), new("FSP", ConceptNature.Deduction, 120_000m));
        }
        // Prima de junio de 2026 (entra) y cesantías consignadas en febrero (no entran): corridas especiales aprobadas con corte en el mes.
        Especial(PayrollRunKind.ServiceBonus, new DateOnly(2026, 6, 30), "PRIMA", 6_000_000m);
        Especial(PayrollRunKind.Severance, new DateOnly(2026, 2, 14), "CESANTIAS", 12_000_000m);
    }

    private void Especial(PayrollRunKind kind, DateOnly corte, string concepto, decimal monto)
    {
        var def = D.Db.PayrollConceptDefinitions.Single(c => c.Code == concepto);
        var run = new PayrollRun
        {
            Kind = kind, CutoffDate = corte, PayDate = corte, Year = (short)corte.Year, Version = 1, Status = PayrollRunStatus.Approved,
            CalculatedAt = corte.ToDateTime(TimeOnly.MinValue), CalculatedBy = "ana@demo", ApprovedAt = corte.ToDateTime(TimeOnly.MinValue), ApprovedBy = "contadora@demo",
            InputsHash = new string('d', 64), CreatedBy = "test",
        };
        var fila = new PayrollRunEmployee { EmployeeId = J.Id, PayrollPlanId = D.Plan.Id, DaysWorked = 180, TotalEarnings = monto, NetPay = monto, CreatedBy = "test" };
        fila.Lines.Add(new PayrollRunLine { ConceptDefinitionId = def.Id, ConceptCode = def.Code, ConceptName = def.Name, Nature = ConceptNature.Earning, Amount = monto, Order = 1, ExplanationJson = "{}", CreatedBy = "test" });
        run.Employees.Add(fila);
        D.Db.PayrollRuns.Add(run);
        D.Db.SaveChanges();
    }

    public WithholdingRateInputLoader Loader => new(D.Db, D.Policies);
    public CalculateWithholdingRatesCommandHandler Calculador() => new(D.Db, Loader, D.Clock, D.User, D.AuditEmitter);
    public ApproveWithholdingRateCommandHandler Aprobador() => new(D.Db, D.Clock, D.User, D.StaleMarker, D.AuditEmitter);
}

public class CalculateWithholdingRatesCommandHandlerTests
{
    [Fact]
    public async Task Doce_meses_con_prima_y_sin_cesantias_dan_el_porcentaje_del_caso_dorado()
    {
        var f = new RetencionDePrueba();
        var r = await f.Calculador().Handle(new CalculateWithholdingRatesCommand(2026, 2), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var item = r.Value.Items.Should().ContainSingle().Subject;
        item.MonthsUsed.Should().Be(12);
        item.Divisor.Should().Be(13m);
        item.Percentage.Should().Be(6.80m, "caso dorado 04: 150.000.000 de sumatoria con la prima y sin las cesantías");
        item.ValidFrom.Should().Be(new DateOnly(2027, 1, 1));
        item.ValidTo.Should().Be(new DateOnly(2027, 6, 30));
        r.Value.Skipped.Should().BeEmpty();
        r.Value.Warnings.Should().BeEmpty("noviembre de 2026, el último mes de la ventana, está aprobado");
        var calc = await f.D.Db.WithholdingRateCalculations.Include(c => c.Months).SingleAsync();
        calc.Months.Should().HaveCount(12);
        calc.Months.Single(m => m.Month == 6).GrossIncome.Should().Be(18_000_000m, "junio suma la prima");
        calc.Months.Single(m => m.Month == 6).IncludedSpecialRuns.Should().BeTrue();
        calc.Months.Single(m => m.Month == 2).GrossIncome.Should().Be(12_000_000m, "las cesantías de febrero no entran (art. 386)");
        calc.TotalGrossIncome.Should().Be(150_000_000m);
        calc.TotalMandatoryContributions.Should().Be(12_960_000m);
    }

    [Fact]
    public async Task Con_menos_de_doce_meses_el_divisor_son_los_meses_y_la_explicacion_lo_dice()
    {
        var f = new RetencionDePrueba(meses: 8);
        var r = await f.Calculador().Handle(new CalculateWithholdingRatesCommand(2026, 2), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var item = r.Value.Items.Single();
        item.MonthsUsed.Should().Be(8);
        item.Divisor.Should().Be(8m);
        var detalle = await new GetWithholdingRateCalculationQueryHandler(f.D.Db).Handle(new GetWithholdingRateCalculationQuery(item.CalculationPublicId), CancellationToken.None);
        detalle.Value.DivisorSource.Should().Be("MesesDeVinculacion");
        detalle.Value.Steps.Should().Contain(s => s.Label.Contains("8 meses de vinculación"));
    }

    [Fact]
    public async Task Sin_historia_queda_en_skipped_con_NoHistory()
    {
        var f = new RetencionDePrueba();
        var nuevo = f.D.Empleado("Nuevo", 3_000_000m, new DateTime(2026, 12, 1), procedimientoRetencion: 2);
        var r = await f.Calculador().Handle(new CalculateWithholdingRatesCommand(2026, 2), CancellationToken.None);
        r.IsSuccess.Should().BeTrue();
        r.Value.Skipped.Should().ContainSingle(s => s.EmployeePublicId == nuevo.PublicId && s.Reason == "Payroll.WithholdingRate.NoHistory");
        r.Value.Items.Should().ContainSingle(i => i.EmployeePublicId == f.J.PublicId);
    }

    [Fact]
    public async Task Recalcular_crea_version_nueva_y_deja_la_anterior_Superseded()
    {
        var f = new RetencionDePrueba();
        var v1 = await f.Calculador().Handle(new CalculateWithholdingRatesCommand(2026, 2), CancellationToken.None);
        var v2 = await f.Calculador().Handle(new CalculateWithholdingRatesCommand(2026, 2), CancellationToken.None);
        v2.Value.Items.Single().Version.Should().Be(2);
        (await f.D.Db.WithholdingRateCalculations.SingleAsync(c => c.PublicId == v1.Value.Items.Single().CalculationPublicId)).Status.Should().Be(WithholdingRateCalculationStatus.Superseded);
    }

    [Fact]
    public async Task Sin_empleados_en_procedimiento_2_responde_NoProcedure2Employees()
    {
        var f = new RetencionDePrueba(procedimiento: 1);
        var r = await f.Calculador().Handle(new CalculateWithholdingRatesCommand(2026, 2), CancellationToken.None);
        r.Error.Code.Should().Be("Payroll.WithholdingRate.NoProcedure2Employees");
    }

    [Fact]
    public async Task La_politica_DividirLuegoDepurar_cambia_la_secuencia()
    {
        var f = new RetencionDePrueba();
        f.D.Politica(CompanyPolicyKeys.P2SecuenciaDepuracion, CompanyPolicyKeys.P2SecuenciaDepuracionValores.DividirLuegoDepurar, new DateOnly(2026, 1, 1));
        await f.D.Db.SaveChangesAsync();
        var r = await f.Calculador().Handle(new CalculateWithholdingRatesCommand(2026, 2), CancellationToken.None);
        r.Value.Items.Single().Sequence.Should().Be("DivideThenDepurate");
    }
}

public class ApproveWithholdingRateCommandHandlerTests
{
    [Fact]
    public async Task Aprobar_cierra_la_vigencia_anterior_y_abre_la_nueva_calculada_y_la_nomina_siguiente_la_lee()
    {
        var f = new RetencionDePrueba();
        f.D.Db.EmployeeWithholdingRates.Add(new EmployeeWithholdingRate { EmployeeId = f.J.Id, RatePercent = 5.10m, ValidFrom = new DateTime(2026, 7, 1), ValidTo = null, Origin = WithholdingRateOrigin.Manual, CreatedBy = "test" });
        await f.D.Db.SaveChangesAsync();
        var calc = (await f.Calculador().Handle(new CalculateWithholdingRatesCommand(2026, 2), CancellationToken.None)).Value.Items.Single();

        var r = await f.Aprobador().Handle(new ApproveWithholdingRateCommand(calc.CalculationPublicId), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.ValidFrom.Should().Be(new DateOnly(2027, 1, 1));
        r.Value.ValidTo.Should().Be(new DateOnly(2027, 6, 30));
        r.Value.PreviousClosedAt.Should().Be(new DateOnly(2026, 12, 31));

        var tasas = await f.D.Db.EmployeeWithholdingRates.Where(t => t.EmployeeId == f.J.Id).OrderBy(t => t.ValidFrom).ToListAsync();
        tasas.Should().HaveCount(2, "la anterior se cierra, no se borra (R8)");
        tasas[0].ValidTo.Should().Be(new DateTime(2026, 12, 31));
        tasas[0].Origin.Should().Be(WithholdingRateOrigin.Manual);
        tasas[1].Origin.Should().Be(WithholdingRateOrigin.Calculated);
        tasas[1].RatePercent.Should().Be(6.80m);
        tasas[1].SourceCalculationId.Should().NotBeNull();
        var fila = await f.D.Db.WithholdingRateCalculations.SingleAsync();
        fila.Status.Should().Be(WithholdingRateCalculationStatus.Approved);
        fila.ResultingRateId.Should().Be(tasas[1].Id);

        // La quincena de enero de 2027 lee la vigencia nueva por el cargador ordinario.
        var enero = f.D.Periodo(new DateTime(2027, 1, 1), new DateTime(2027, 1, 15), PayPeriodStatus.Open);
        var entrada = await f.D.Loader.LoadAsync(enero, CancellationToken.None);
        entrada.Employees.Single(e => e.Employee.Id == f.J.Id).Input.WithholdingRatePercent.Should().Be(6.80m);

        var otraVez = await f.Aprobador().Handle(new ApproveWithholdingRateCommand(calc.CalculationPublicId), CancellationToken.None);
        otraVez.Error.Code.Should().Be("Payroll.WithholdingRate.AlreadyApproved");
    }

    [Fact]
    public async Task El_lote_aprueba_por_item_y_reporta_cada_resultado()
    {
        var f = new RetencionDePrueba();
        var calc = (await f.Calculador().Handle(new CalculateWithholdingRatesCommand(2026, 2), CancellationToken.None)).Value.Items.Single();
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<ApproveWithholdingRateCommand>(), Arg.Any<CancellationToken>()).Returns(ci => f.Aprobador().Handle(ci.Arg<ApproveWithholdingRateCommand>(), CancellationToken.None));
        var r = await new ApproveWithholdingRatesBatchCommandHandler(sender).Handle(new ApproveWithholdingRatesBatchCommand([calc.CalculationPublicId, Guid.NewGuid()]), CancellationToken.None);
        r.Value.Should().HaveCount(2);
        r.Value[0].Approved.Should().BeTrue();
        r.Value[1].Approved.Should().BeFalse();
        r.Value[1].ErrorCode.Should().Be("Payroll.WithholdingRate.CalculationNotFound");
    }
}

/// <summary>R8: la ficha de retención cierra vigencias en vez de reemplazarlas; la aprobada por cálculo conserva su origen.</summary>
public class SetEmployeeWithholdingCierraVigenciaTests
{
    [Fact]
    public async Task Una_tasa_nueva_cierra_la_abierta_la_vispera_y_la_que_sigue_igual_se_conserva()
    {
        var d = new NominaTestData();
        var e = d.Empleado("Lucía", 9_000_000m, new DateTime(2020, 1, 1), procedimientoRetencion: 2);
        d.Db.EmployeeWithholdingRates.Add(new EmployeeWithholdingRate { EmployeeId = e.Id, RatePercent = 4.5m, ValidFrom = new DateTime(2026, 1, 1), Origin = WithholdingRateOrigin.Calculated, SourceCalculationId = 77, CreatedBy = "test" });
        await d.Db.SaveChangesAsync();
        var handler = new SetEmployeeWithholdingCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker, d.AuditEmitter);
        var r = await handler.Handle(new SetEmployeeWithholdingCommand(e.PublicId, 2,
            [new WithholdingRateInput(4.5m, new DateTime(2026, 1, 1), null), new WithholdingRateInput(6.2m, new DateTime(2026, 7, 1), null)], []), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var tasas = await d.Db.EmployeeWithholdingRates.Where(t => t.EmployeeId == e.Id).OrderBy(t => t.ValidFrom).ToListAsync();
        tasas.Should().HaveCount(2);
        tasas[0].Origin.Should().Be(WithholdingRateOrigin.Calculated, "la que sigue igual se conserva con su origen");
        tasas[0].SourceCalculationId.Should().Be(77);
        tasas[0].ValidTo.Should().Be(new DateTime(2026, 6, 30), "la nueva la cierra la víspera");
        tasas[1].RatePercent.Should().Be(6.2m);
        tasas[1].Origin.Should().Be(WithholdingRateOrigin.Manual);
    }
}
