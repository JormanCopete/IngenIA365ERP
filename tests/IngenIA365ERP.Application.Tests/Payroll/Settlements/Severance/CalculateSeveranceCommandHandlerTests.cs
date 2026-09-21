using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Settlements.Severance;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Payroll.Settlements.Severance;

/// <summary>
/// Feature 010 US2 (T048): la liquidación de cesantías e intereses del año por el comando —los
/// valores de los casos dorados 03, 04 y 05 al peso, el ingreso de septiembre proporcional, el
/// integral y el retirado con definitiva excluidos con su razón, la ventana de estabilidad explicada,
/// el duplicado del mismo año y el recálculo como versión nueva.
/// </summary>
public class CalculateSeveranceCommandHandlerTests
{
    private static async Task<(decimal Cesantias, decimal Intereses, string ExplicacionCesantias, int Dias)> LineasDeAsync(EscenarioDeCesantias e, Guid runId, int employeeId)
    {
        var run = await e.D.Db.PayrollRuns.AsNoTracking().SingleAsync(r => r.PublicId == runId);
        var fila = await e.D.Db.PayrollRunEmployees.AsNoTracking().Include(f => f.Lines).SingleAsync(f => f.PayrollRunId == run.Id && f.EmployeeId == employeeId);
        var cesantias = fila.Lines.Single(l => l.ConceptCode == WellKnownConceptCodes.Severance);
        var intereses = fila.Lines.Single(l => l.ConceptCode == WellKnownConceptCodes.SeveranceInterest);
        return (cesantias.Amount, intereses.Amount, cesantias.ExplanationJson, fila.DaysWorked);
    }

    [Fact]
    public async Task Liquida_2026_con_los_valores_de_los_casos_dorados_y_excluye_al_integral_y_al_retirado_con_definitiva()
    {
        var e = new EscenarioDeCesantias();

        var r = await e.CalcularAsync();

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var dto = r.Value;
        dto.Kind.Should().Be("Severance");
        dto.CutoffDate.Should().Be(new DateOnly(2026, 12, 31));
        dto.Version.Should().Be(1);
        dto.Employees.Should().Be(3, "A, F y G; C y E quedan fuera");
        dto.Blockers.Should().BeEmpty();

        dto.Excluded.Should().HaveCount(2);
        dto.Excluded.Should().ContainSingle(x => x.EmployeePublicId == e.C.PublicId).Which.ReasonCode.Should().Be("SalarioIntegral");
        dto.Excluded.Should().ContainSingle(x => x.EmployeePublicId == e.E.PublicId).Which.ReasonCode.Should().Be("RetiradoConDefinitiva");

        // A: estable → último salario + auxilio, 360 días (caso 03).
        var a = await LineasDeAsync(e, dto.RunPublicId, e.A.Id);
        a.Cesantias.Should().Be(2_749_095m);
        a.Intereses.Should().Be(329_891.40m);
        a.Dias.Should().Be(360);
        a.ExplicacionCesantias.Should().Contain("sin cambios", "la explicación dice que el salario no varió en la ventana de estabilidad")
            .And.Contain("CESANTIAS_VENTANA_ESTABILIDAD_MESES");

        // F: cambió en los últimos tres meses → promedio ponderado del año (caso 05).
        var f = await LineasDeAsync(e, dto.RunPublicId, e.F.Id);
        f.Cesantias.Should().Be(2_315_761.67m);
        f.Intereses.Should().Be(277_891.40m);
        f.ExplicacionCesantias.Should().Contain("cambió en los últimos", "la explicación dice por qué se promedia")
            .And.Contain("promedio ponderado");

        // G: ingresó el 01-09 → 120 días proporcionales (caso 04).
        var g = await LineasDeAsync(e, dto.RunPublicId, e.G.Id);
        g.Cesantias.Should().Be(666_666.67m);
        g.Intereses.Should().Be(26_666.67m);
        g.Dias.Should().Be(120);

        var run = await e.D.Db.PayrollRuns.AsNoTracking().SingleAsync(x => x.PublicId == dto.RunPublicId);
        run.Kind.Should().Be(PayrollRunKind.Severance);
        run.Year.Should().Be(2026);
        run.Semester.Should().BeNull();
        run.PayPeriodId.Should().BeNull();
        run.Status.Should().Be(PayrollRunStatus.Draft);
        run.EsCoherente.Should().BeTrue();
        dto.Totals.Earnings.Should().Be(a.Cesantias + a.Intereses + f.Cesantias + f.Intereses + g.Cesantias + g.Intereses);
    }

    [Fact]
    public async Task Sin_lista_de_empleados_toma_a_todos_los_vigentes_en_el_anio_y_deja_fuera_al_retirado_antes_del_anio()
    {
        var e = new EscenarioDeCesantias();
        var viejo = e.D.Empleado("Hugo", 2_000_000m, new DateTime(2023, 1, 1), retiro: new DateTime(2025, 6, 30));

        var r = await e.Calculador().Handle(new CalculateSeveranceCommand(2026), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Employees.Should().Be(4, "Ana (la de NominaTestData), A, F y G");
        r.Value.Excluded.Should().HaveCount(2, "C integral y E retirada con definitiva");
        r.Value.Excluded.Select(x => x.EmployeePublicId).Should().NotContain(viejo.PublicId, "retirado en 2025: ni entra ni se explica, no es de este año");
    }

    [Fact]
    public async Task La_segunda_del_mismo_anio_es_Duplicate_y_recalcular_deja_version_2_con_la_anterior_Superseded()
    {
        var e = new EscenarioDeCesantias();
        var v1 = await e.CalcularAsync();
        v1.IsSuccess.Should().BeTrue(v1.Error.Message);

        var otra = await e.CalcularAsync();
        otra.IsFailure.Should().BeTrue();
        otra.Error.Code.Should().Be("Payroll.Settlement.Duplicate");
        otra.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { runPublicId = v1.Value.RunPublicId, status = "Draft" });

        var v2 = await e.Recalculador().Handle(new RecalculateSeveranceCommand(v1.Value.RunPublicId), CancellationToken.None);
        v2.IsSuccess.Should().BeTrue(v2.Error.Message);
        v2.Value.Version.Should().Be(2);
        v2.Value.RunPublicId.Should().NotBe(v1.Value.RunPublicId, "recalcular es una corrida nueva (Principio XI)");

        var corridas = await e.D.Db.PayrollRuns.AsNoTracking().Where(r => r.Kind == PayrollRunKind.Severance).OrderBy(r => r.Version).ToListAsync();
        corridas.Select(r => (r.Version, r.Status)).Should().Equal((1, PayrollRunStatus.Superseded), (2, PayrollRunStatus.Draft));
        corridas[1].InputsHash.Should().NotBe(corridas[0].InputsHash, "el recálculo toma a toda la población del año, Ana incluida");
    }

    [Fact]
    public async Task Recalcular_una_aprobada_o_una_corrida_de_otro_tipo_se_rechaza()
    {
        var e = new EscenarioDeCesantias(conContabilidad: true);
        var v1 = await e.CalcularAsync();
        (await e.AprobarAsync(v1.Value.RunPublicId)).IsSuccess.Should().BeTrue();

        var aprobada = await e.Recalculador().Handle(new RecalculateSeveranceCommand(v1.Value.RunPublicId), CancellationToken.None);
        aprobada.Error.Code.Should().Be("Payroll.Settlement.NotDraft");

        var ordinaria = e.D.Borrador(e.D.Marzo);
        var otroTipo = await e.Recalculador().Handle(new RecalculateSeveranceCommand(ordinaria.PublicId), CancellationToken.None);
        otroTipo.Error.Code.Should().Be("Payroll.Settlement.KindMismatch");

        var inexistente = await e.Recalculador().Handle(new RecalculateSeveranceCommand(Guid.NewGuid()), CancellationToken.None);
        inexistente.Error.Code.Should().Be("Payroll.Run.NotFound");
    }

    [Fact]
    public async Task Si_nadie_tiene_derecho_responde_NoEligibleEmployees_con_la_lista_de_excluidos()
    {
        var e = new EscenarioDeCesantias();

        var r = await e.CalcularAsync([e.C.PublicId, e.E.PublicId]);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Payroll.Settlement.NoEligibleEmployees");
        (await e.D.Db.PayrollRuns.AsNoTracking().CountAsync(x => x.Kind == PayrollRunKind.Severance)).Should().Be(0, "no se guarda una corrida vacía");
    }

    [Fact]
    public async Task Un_corte_fuera_del_anio_o_un_empleado_inexistente_se_rechazan_antes_de_calcular()
    {
        var e = new EscenarioDeCesantias();

        var corte = await e.Calculador().Handle(new CalculateSeveranceCommand(2026, new DateOnly(2027, 1, 31)), CancellationToken.None);
        corte.Error.Code.Should().Be("Payroll.Severance.CutoffOutsideYear");

        var fantasma = await e.CalcularAsync([e.A.PublicId, Guid.NewGuid()]);
        fantasma.Error.Code.Should().Be("Payroll.Employee.NotFound");

        new CalculateSeveranceCommandValidator().Validate(new CalculateSeveranceCommand(2026, new DateOnly(2025, 12, 31))).IsValid.Should().BeFalse();
        new CalculateSeveranceCommandValidator().Validate(new CalculateSeveranceCommand(2026)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Sin_saldo_inicial_e_ingreso_anterior_al_arranque_avisa_sin_bloquear()
    {
        var e = new EscenarioDeCesantias();
        e.D.Politica(Domain.Payroll.Policies.CompanyPolicyKeys.ArranqueNominaFecha, "2026-06-01", new DateOnly(2026, 1, 1));

        var r = await e.CalcularAsync([e.A.PublicId, e.G.PublicId]);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Blockers.Should().BeEmpty("el saldo inicial ausente es aviso, no bloqueo");
        var aviso = r.Value.Warnings.Should().ContainSingle(w => w.Code == "Payroll.Settlement.OpeningBalanceMissing").Subject;
        aviso.Message.Should().Contain("Alba").And.NotContain("Gema", "Gema ingresó después del arranque");
    }
}
