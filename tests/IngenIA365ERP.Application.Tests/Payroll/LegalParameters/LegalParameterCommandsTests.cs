using FluentAssertions;
using IngenIA365ERP.Application.Payroll.EmployeeTax;
using IngenIA365ERP.Application.Payroll.LegalParameters;
using IngenIA365ERP.Application.Payroll.LegalParameters.Queries;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Payroll.LegalParameters;

/// <summary>T081 — FR-010, FR-011, FR-039: vigencias nuevas, tablas por rangos y retención por empleado.</summary>
public class LegalParameterCommandsTests
{
    [Fact]
    public async Task Una_vigencia_nueva_cierra_la_anterior_el_dia_antes_y_desactualiza_los_borradores()
    {
        var d = new NominaTestData();
        var run = d.Borrador(d.Marzo);
        var h = new AddLegalParameterVersionCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker);

        var r = await h.Handle(new AddLegalParameterVersionCommand(LegalParameterCodes.TransportAllowance, new DateTime(2027, 1, 1), 262_000m, null, "Decreto 2027"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var versiones = await d.Db.PayrollLegalParameters.Where(p => p.Code == LegalParameterCodes.TransportAllowance).OrderBy(p => p.ValidFrom).ToListAsync();
        versiones.Should().HaveCount(2);
        versiones[0].ValidTo.Should().Be(new DateTime(2026, 12, 31));
        versiones[1].Value.Should().Be(262_000m);
        versiones[1].Kind.Should().Be(LegalParameterKind.Amount, "el tipo se hereda");
        (await d.Db.PayrollRuns.SingleAsync(x => x.Id == run.Id)).Status.Should().Be(PayrollRunStatus.Stale);
    }

    [Fact]
    public async Task Una_vigencia_que_no_es_posterior_a_la_actual_se_rechaza()
    {
        var d = new NominaTestData();
        var h = new AddLegalParameterVersionCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker);

        var r = await h.Handle(new AddLegalParameterVersionCommand(LegalParameterCodes.Smmlv, new DateTime(2026, 1, 1), 1_800_000m, null, "x"), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.LegalParameterOverlap");
    }

    [Fact]
    public async Task Una_tabla_con_huecos_o_sin_tramo_abierto_se_rechaza()
    {
        var d = new NominaTestData();
        var h = new AddLegalParameterVersionCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker);

        var conHueco = await h.Handle(new AddLegalParameterVersionCommand(LegalParameterCodes.WithholdingTableUvt, new DateTime(2027, 1, 1), null,
            [new(0m, 95m, 0m, 0m), new(100m, 150m, 19m, 0m), new(150m, null, 28m, 10m)], "Ley 2027"), CancellationToken.None);
        conHueco.Error.Code.Should().Be("Payroll.RangeTableInvalid");
        conHueco.Error.Message.Should().Contain("hueco");

        var sinAbierto = await h.Handle(new AddLegalParameterVersionCommand(LegalParameterCodes.WithholdingTableUvt, new DateTime(2027, 1, 1), null,
            [new(0m, 95m, 0m, 0m), new(95m, 150m, 19m, 0m)], "Ley 2027"), CancellationToken.None);
        sinAbierto.Error.Code.Should().Be("Payroll.RangeTableInvalid");
        sinAbierto.Error.Message.Should().Contain("abierto");

        var ok = await h.Handle(new AddLegalParameterVersionCommand(LegalParameterCodes.WithholdingTableUvt, new DateTime(2027, 1, 1), null,
            [new(0m, 95m, 0m, 0m), new(95m, 150m, 19m, 0m), new(150m, null, 28m, 10m)], "Ley 2027"), CancellationToken.None);
        ok.IsSuccess.Should().BeTrue(ok.Error.Message);
        var nueva = await d.Db.PayrollLegalParameters.Include(p => p.Ranges).SingleAsync(p => p.PublicId == ok.Value);
        nueva.Ranges.Should().HaveCount(3);
        nueva.RangeUnitParameterCode.Should().Be(LegalParameterCodes.Uvt, "la unidad se hereda de la vigencia anterior");
        nueva.RangeIsMarginal.Should().BeTrue();
    }

    [Fact]
    public async Task Un_codigo_nuevo_exige_nombre_y_tipo()
    {
        var d = new NominaTestData();
        var h = new AddLegalParameterVersionCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker);

        var sinTipo = await h.Handle(new AddLegalParameterVersionCommand("BONO_ALIMENTACION_TOPE", new DateTime(2026, 1, 1), 100_000m, null, "Convención"), CancellationToken.None);
        sinTipo.Error.Code.Should().Be("Payroll.LegalParameterNew");

        var ok = await h.Handle(new AddLegalParameterVersionCommand("BONO_ALIMENTACION_TOPE", new DateTime(2026, 1, 1), 100_000m, null, "Convención", "Tope del bono de alimentación", LegalParameterKind.Amount), CancellationToken.None);
        ok.IsSuccess.Should().BeTrue(ok.Error.Message);
    }

    [Fact]
    public async Task La_lista_senala_los_requeridos_sin_vigencia_para_el_ano_siguiente()
    {
        var d = new NominaTestData();
        var q = new ListLegalParametersQueryHandler(d.Db, d.Clock);

        var r = await q.Handle(new ListLegalParametersQuery(), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        r.Value.MissingThisYear.Should().BeEmpty("la semilla 2026 no tiene fecha de fin");
        r.Value.MissingNextYear.Should().BeEmpty("sin ValidTo, la vigencia 2026 sigue rigiendo en 2027 hasta que se registre la nueva");
        r.Value.Items.Should().Contain(i => i.Code == LegalParameterCodes.Smmlv && i.IsRequired);
        r.Value.Items.Single(i => i.Code == LegalParameterCodes.WithholdingTableUvt).Ranges.Should().HaveCount(7);
    }

    [Fact]
    public async Task La_retencion_del_empleado_no_admite_porcentajes_solapados_y_deja_rastro()
    {
        var d = new NominaTestData();
        var h = new SetEmployeeWithholdingCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker, d.AuditEmitter);

        var solapadas = await h.Handle(new SetEmployeeWithholdingCommand(d.Ana.PublicId, 2,
            [new(5m, new DateTime(2026, 1, 1), new DateTime(2026, 6, 30)), new(6m, new DateTime(2026, 6, 1), null)], []), CancellationToken.None);
        solapadas.Error.Code.Should().Be("Payroll.WithholdingRateOverlap");

        var ok = await h.Handle(new SetEmployeeWithholdingCommand(d.Ana.PublicId, 2,
            [new(5m, new DateTime(2026, 1, 1), new DateTime(2026, 6, 30)), new(6m, new DateTime(2026, 7, 1), null)],
            [new(TaxDeductionKind.HousingInterest, 800_000m, null, new DateTime(2026, 1, 1), null)]), CancellationToken.None);
        ok.IsSuccess.Should().BeTrue(ok.Error.Message);

        (await d.Db.Employees.SingleAsync(e => e.Id == d.Ana.Id)).WithholdingProcedure.Should().Be(2);
        d.Db.EmployeeWithholdingRates.Count(r => r.EmployeeId == d.Ana.Id).Should().Be(2);
        d.Db.EmployeeTaxDeductions.Count(x => x.EmployeeId == d.Ana.Id).Should().Be(1);

        var consulta = await new GetEmployeeWithholdingQueryHandler(d.Db).Handle(new GetEmployeeWithholdingQuery(d.Ana.PublicId), CancellationToken.None);
        consulta.Value.Procedure.Should().Be(2);
        consulta.Value.Rates.Should().HaveCount(2);
        consulta.Value.Deductions.Should().ContainSingle(x => x.Kind == TaxDeductionKind.HousingInterest);
    }
}
