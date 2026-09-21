using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Application.Payroll.Settlements.ServiceBonus;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Policies;
using IngenIA365ERP.Domain.Payroll.Settlements;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Settlements.ServiceBonus;

/// <summary>
/// T041 (feature 010, US1; FR-005, FR-007, FR-008, FR-009): la prima 2026-II del escenario de
/// <see cref="PrimaDePrueba"/> reproduce al peso los casos dorados 01 y 02 (SC-001), deja fuera con razón
/// a quien no tiene derecho, rechaza una segunda del mismo semestre, avisa —sin bloquear— el saldo
/// inicial ausente, y recalcular deja versión nueva con la anterior reemplazada.
/// </summary>
public class CalculateServiceBonusCommandHandlerTests
{
    private static readonly CalculateServiceBonusCommand Segundo2026 = new(2026, 2);

    [Fact]
    public async Task Dos_empleados_dan_los_valores_de_los_casos_dorados_y_los_sin_derecho_quedan_excluidos_con_razon()
    {
        var s = PrimaDePrueba.SegundoSemestre2026();

        var r = await PrimaDePrueba.Calcular(s.D).Handle(Segundo2026, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var v = r.Value;
        v.Kind.Should().Be("ServiceBonus");
        v.Version.Should().Be(1);
        v.CutoffDate.Should().Be(new DateOnly(2026, 12, 31));
        v.Blockers.Should().BeEmpty();

        (await PrimaDePrueba.LineaAsync(s.D, v.RunPublicId, s.A, "PRIMA"))!.Amount.Should().Be(1_124_547.50m, "caso dorado 01: (2.000.000 + 249.095) × 180 / 360");
        (await PrimaDePrueba.LineaAsync(s.D, v.RunPublicId, s.B, "PRIMA"))!.Amount.Should().Be(630_404.72m, "caso dorado 02: 46 días a 2.000.000 + 60 días a 2.249.095");
        (await PrimaDePrueba.LineaAsync(s.D, v.RunPublicId, s.A, "RETEFTE_PRIMA"))!.Amount.Should().Be(0m, "P1: la prima sola queda bajo el primer tramo");

        var excluidos = v.Excluded.ToDictionary(x => x.EmployeePublicId, x => x.ReasonCode);
        excluidos.Should().Contain(s.C.PublicId, SettlementReasonCodes.SalarioIntegral);
        excluidos.Should().Contain(s.Aprendiz.PublicId, SettlementReasonCodes.AprendizLectiva);
        excluidos.Should().Contain(s.Pasante.PublicId, SettlementReasonCodes.Pasante);
        excluidos.Should().Contain(s.E.PublicId, SettlementReasonCodes.YaPagadaEnDefinitiva);
        excluidos.Should().NotContainKey(s.A.PublicId).And.NotContainKey(s.B.PublicId);
        v.Excluded.Should().OnlyContain(x => !string.IsNullOrWhiteSpace(x.Reason), "cada exclusión trae su texto para la contadora");

        var run = await s.D.Db.PayrollRuns.SingleAsync(x => x.PublicId == v.RunPublicId);
        run.Kind.Should().Be(PayrollRunKind.ServiceBonus);
        run.PayPeriodId.Should().BeNull();
        (run.Year, run.Semester).Should().Be(((short)2026, (byte)2));
        run.Status.Should().Be(PayrollRunStatus.Draft);
        run.EmployeeCount.Should().Be(v.Employees);
        var filas = await s.D.Db.PayrollRunEmployees.Where(x => x.PayrollRunId == run.Id).Select(x => x.EmployeeId).ToListAsync();
        filas.Should().Contain([s.A.Id, s.B.Id, s.D.Ana.Id]).And.NotContain([s.C.Id, s.Aprendiz.Id, s.Pasante.Id, s.E.Id]);

        await s.D.Audit.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(a => a.Action == AuditEventTypes.PayrollSettlementCalculated && a.EntityPublicId == run.PublicId.ToString()), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task La_pestana_de_excluidos_deriva_la_misma_lista_de_una_corrida_ya_calculada()
    {
        var s = PrimaDePrueba.SegundoSemestre2026();
        var calculo = await PrimaDePrueba.Calcular(s.D).Handle(Segundo2026, CancellationToken.None);

        var r = await PrimaDePrueba.Excluidos(s.D).Handle(new GetServiceBonusExclusionsQuery(calculo.Value.RunPublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Select(x => (x.EmployeePublicId, x.ReasonCode)).Should().BeEquivalentTo(calculo.Value.Excluded.Select(x => (x.EmployeePublicId, x.ReasonCode)));
    }

    [Fact]
    public async Task Una_segunda_prima_del_mismo_semestre_es_Duplicate_mientras_la_anterior_este_en_borrador()
    {
        var s = PrimaDePrueba.SegundoSemestre2026();
        var primera = await PrimaDePrueba.Calcular(s.D).Handle(Segundo2026, CancellationToken.None);

        var segunda = await PrimaDePrueba.Calcular(s.D).Handle(Segundo2026, CancellationToken.None);

        segunda.Error.Code.Should().Be("Payroll.Settlement.Duplicate");
        segunda.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { runPublicId = primera.Value.RunPublicId, status = "Draft" });
        (await s.D.Db.PayrollRuns.CountAsync(r => r.Kind == PayrollRunKind.ServiceBonus)).Should().Be(1, "no se creó nada");

        // Otro semestre sí: la llave es año + semestre (D-02).
        var otro = await PrimaDePrueba.Calcular(s.D).Handle(new CalculateServiceBonusCommand(2026, 1), CancellationToken.None);
        otro.IsSuccess.Should().BeTrue(otro.Error.Message);
        otro.Value.CutoffDate.Should().Be(new DateOnly(2026, 6, 30));
    }

    [Fact]
    public async Task Recalcular_deja_version_2_y_la_1_Superseded_con_el_mismo_hash_si_nada_cambio()
    {
        var s = PrimaDePrueba.SegundoSemestre2026();
        var v1 = await PrimaDePrueba.Calcular(s.D).Handle(Segundo2026, CancellationToken.None);

        var v2 = await PrimaDePrueba.Recalcular(s.D).Handle(new RecalculateServiceBonusCommand(v1.Value.RunPublicId), CancellationToken.None);

        v2.IsSuccess.Should().BeTrue(v2.Error.Message);
        v2.Value.Version.Should().Be(2);
        var corridas = await s.D.Db.PayrollRuns.Where(r => r.Kind == PayrollRunKind.ServiceBonus).OrderBy(r => r.Version).ToListAsync();
        corridas.Select(r => (r.Version, r.Status)).Should().Equal((1, PayrollRunStatus.Superseded), (2, PayrollRunStatus.Draft));
        corridas[1].InputsHash.Should().Be(corridas[0].InputsHash, "misma entrada, mismo resultado (FR-014)");
        corridas[1].TotalNet.Should().Be(corridas[0].TotalNet);

        var deOtroTipo = await PrimaDePrueba.Recalcular(s.D).Handle(new RecalculateServiceBonusCommand(s.DefinitivaDeE.PublicId), CancellationToken.None);
        deOtroTipo.Error.Code.Should().Be("Payroll.Settlement.KindMismatch");
    }

    [Fact]
    public async Task Ingreso_anterior_al_arranque_sin_saldo_inicial_avisa_con_su_codigo_y_no_bloquea()
    {
        var s = PrimaDePrueba.SegundoSemestre2026();
        // La nómina arrancó aquí el 01-07-2026: A y Ana traen historia de antes; B ingresó después.
        s.D.Politica(CompanyPolicyKeys.ArranqueNominaFecha, "2026-07-01", new DateOnly(2026, 1, 1));
        s.D.SaldoInicial(s.D.Ana, new DateOnly(2026, 6, 30), prima: 0m, diasPrima: 0);

        var r = await PrimaDePrueba.Calcular(s.D).Handle(Segundo2026, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Blockers.Should().BeEmpty("el saldo inicial ausente es aviso, no bloqueo");
        var aviso = r.Value.Warnings.Should().ContainSingle(w => w.Code == SettlementErrors.OpeningBalanceMissingCode).Subject;
        aviso.Message.Should().Contain("Alba").And.NotContain("Bruno").And.NotContain("Ana ");
        aviso.Data.Should().BeEquivalentTo(new { employeePublicIds = new[] { s.A.PublicId } });

        var fila = await s.D.Db.PayrollRunEmployees.SingleAsync(x => x.EmployeeId == s.A.Id);
        fila.Flags.Should().Be(RunEmployeeFlag.OpeningBalanceMissing);
        SettlementRunPersister.SinAvisos(fila.Flags).Should().Be(RunEmployeeFlag.None);
    }

    [Fact]
    public async Task Sin_nadie_con_derecho_responde_NoEligibleEmployees_con_la_lista()
    {
        var s = PrimaDePrueba.SegundoSemestre2026();

        var r = await PrimaDePrueba.Calcular(s.D).Handle(new CalculateServiceBonusCommand(2026, 2, [s.C.PublicId, s.Aprendiz.PublicId]), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.Settlement.NoEligibleEmployees");
        r.Error.Should().BeOfType<ErrorConDatos>();
        (await s.D.Db.PayrollRuns.CountAsync(x => x.Kind == PayrollRunKind.ServiceBonus)).Should().Be(0);

        var desconocido = await PrimaDePrueba.Calcular(s.D).Handle(new CalculateServiceBonusCommand(2026, 2, [Guid.NewGuid()]), CancellationToken.None);
        desconocido.Error.Code.Should().Be("Payroll.Employee.NotFound");
    }

    [Fact]
    public async Task Falta_un_parametro_legal_al_corte_y_se_niega_nombrandolo_sin_tocar_a_nadie()
    {
        var s = PrimaDePrueba.SegundoSemestre2026();
        var dias = await s.D.Db.PayrollLegalParameters.SingleAsync(p => p.Code == "PRIMA_DIAS_ANIO");
        dias.ValidTo = new DateTime(2026, 11, 30);
        await s.D.Db.SaveChangesAsync();

        var r = await PrimaDePrueba.Calcular(s.D).Handle(Segundo2026, CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.Settlement.ParametersMissing");
        r.Error.Message.Should().Contain("PRIMA_DIAS_ANIO").And.Contain("31/12/2026");
        (await s.D.Db.PayrollRuns.CountAsync(x => x.Kind == PayrollRunKind.ServiceBonus)).Should().Be(0);
    }

    [Fact]
    public void El_validador_exige_semestre_1_o_2_y_un_anio_razonable()
    {
        var v = new CalculateServiceBonusCommandValidator();
        v.Validate(new CalculateServiceBonusCommand(2026, 3)).IsValid.Should().BeFalse();
        v.Validate(new CalculateServiceBonusCommand(0, 1)).IsValid.Should().BeFalse();
        v.Validate(new CalculateServiceBonusCommand(2026, 2, [Guid.Empty])).IsValid.Should().BeFalse();
        v.Validate(Segundo2026).IsValid.Should().BeTrue();
        new ListServiceBonusRunsQueryValidator().Validate(new ListServiceBonusRunsQuery(2026, "Aprobada")).IsValid.Should().BeFalse();
        new ListServiceBonusRunsQueryValidator().Validate(new ListServiceBonusRunsQuery(2026, "approved")).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task La_lista_trae_cada_version_con_su_estado_pagos_y_comprobante()
    {
        var s = PrimaDePrueba.SegundoSemestre2026();
        var v1 = await PrimaDePrueba.Calcular(s.D).Handle(Segundo2026, CancellationToken.None);
        await PrimaDePrueba.Recalcular(s.D).Handle(new RecalculateServiceBonusCommand(v1.Value.RunPublicId), CancellationToken.None);
        await PrimaDePrueba.Calcular(s.D).Handle(new CalculateServiceBonusCommand(2026, 1), CancellationToken.None);

        var todas = await PrimaDePrueba.Listar(s.D).Handle(new ListServiceBonusRunsQuery(), CancellationToken.None);
        var del2027 = await PrimaDePrueba.Listar(s.D).Handle(new ListServiceBonusRunsQuery(2027), CancellationToken.None);
        var borradores = await PrimaDePrueba.Listar(s.D).Handle(new ListServiceBonusRunsQuery(2026, "Draft"), CancellationToken.None);

        todas.Value.Should().HaveCount(3);
        todas.Value.Select(x => (x.Year, x.Semester, x.Version, x.Status)).Should().Equal((2026, 2, 2, "Draft"), (2026, 2, 1, "Superseded"), (2026, 1, 1, "Draft"));
        todas.Value.Should().OnlyContain(x => x.PaidCount == 0 && x.PostedDocumentPublicId == null && x.CalculatedBy == "ana@demo");
        todas.Value.First(x => x.Semester == 1).CutoffDate.Should().Be(new DateOnly(2026, 6, 30));
        del2027.Value.Should().BeEmpty();
        borradores.Value.Select(x => (x.Semester, x.Version)).Should().BeEquivalentTo([(2, 2), (1, 1)]);
    }
}
