using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.Payslips;
using IngenIA365ERP.Application.Payroll.Runs.ApprovePayrollRun;
using IngenIA365ERP.Application.Payroll.Runs.DiscardPayrollRun;
using IngenIA365ERP.Application.Payroll.Runs.Queries;
using IngenIA365ERP.Application.Payroll.Runs.ReversePayrollRun;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Application.Tests.Payroll.Settlements.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Runs;

/// <summary>
/// Feature 010 (T030, R2, contracts/api.md §2): las rutas de la nómina ordinaria frente a una
/// corrida especial. Aprobar, reversar y descartar la rechazan (si no, <c>Payroll.Runs.Approve</c>
/// aprobaría primas y definitivas y los permisos por tipo serían decorativos); el resumen, el
/// cuadre, el comparativo y el comprobante del empleado la atienden con lo suyo (tipo, corte,
/// provisión, versión anterior, etiqueta).
/// </summary>
public class CorridasEspecialesEnRutasOrdinariasTests
{
    private static readonly ICurrentUserService Contadora = NominaTestData.UsuarioDePrueba("contadora@demo", 9);

    private static PayrollAuditEmitter Auditor(NominaTestData d, ICurrentUserService quien) =>
        new(d.Audit, quien, d.Clock, NullLogger<PayrollAuditEmitter>.Instance);

    [Fact]
    public async Task Aprobar_reversar_y_descartar_de_la_ordinaria_rechazan_una_corrida_especial_con_su_ruta()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre();
        LiquidacionDePrueba.ConContabilidad(d);
        var run = await LiquidacionDePrueba.PrimaAsync(d);

        var aprobar = await new ApprovePayrollRunCommandHandler(d.Db, d.Contabilizador(Contadora), d.Policies, d.Permissions, d.Clock, Contadora, Auditor(d, Contadora), d.StaleMarker)
            .Handle(new ApprovePayrollRunCommand(run.PublicId, Confirm: true), CancellationToken.None);
        var reversar = await new ReversePayrollRunCommandHandler(d.Db, d.Contabilizador(Contadora), d.Clock, Contadora, Auditor(d, Contadora), d.StaleMarker)
            .Handle(new ReversePayrollRunCommand(run.PublicId, "x"), CancellationToken.None);
        var descartar = await new DiscardPayrollRunCommandHandler(d.Db, d.Lock, d.Clock, Contadora, Auditor(d, Contadora))
            .Handle(new DiscardPayrollRunCommand(run.PublicId, "x"), CancellationToken.None);

        foreach (var r in new[] { aprobar.Error, reversar.Error, descartar.Error })
        {
            r.Code.Should().Be("Payroll.Settlement.UseSettlementRoute");
            r.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { kind = "ServiceBonus", route = "/api/payroll/settlements/service-bonus/{runId}" });
        }
        (await d.Db.PayrollRuns.SingleAsync(x => x.Id == run.Id)).Status.Should().Be(PayrollRunStatus.Draft, "nada la tocó");
    }

    [Fact]
    public async Task El_resumen_de_una_corrida_especial_trae_tipo_corte_anio_semestre_y_periodo_nulo()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre();
        d.Politica(Domain.Payroll.Policies.CompanyPolicyKeys.ArranqueNominaFecha, "2026-01-01", new DateOnly(2020, 1, 1));
        var run = await LiquidacionDePrueba.PrimaAsync(d);

        var r = await new GetRunSummaryQueryHandler(d.Db, new RunSummaryBuilder(d.Db)).Handle(new GetRunSummaryQuery(run.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.PeriodPublicId.Should().BeNull();
        r.Value.Kind.Should().Be("ServiceBonus");
        r.Value.CutoffDate.Should().Be(new DateOnly(2026, 6, 30));
        (r.Value.Year, r.Value.Semester).Should().Be((2026, 1));
        r.Value.Warnings.Should().ContainSingle(w => w.Code == SettlementErrors.OpeningBalanceMissingCode,
            "Ana ingresó en 2025, antes del arranque, y no tiene saldo inicial: aviso, no bloqueo");
        r.Value.Blockers.Should().BeEmpty("el saldo inicial ausente no bloquea la aprobación");
    }

    [Fact]
    public async Task El_cuadre_de_una_corrida_especial_agrega_la_provision_por_rubro()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre(provPrimaMensual: 150_000m);
        var run = await LiquidacionDePrueba.PrimaAsync(d);
        var prima = await d.Db.PayrollRunLines.Where(l => l.RunEmployee!.PayrollRunId == run.Id && l.ConceptCode == "PRIMA").SumAsync(l => l.Amount);

        var r = await new GetRunBalanceCheckQueryHandler(d.Db, d.SaldosDeProvision).Handle(new GetRunBalanceCheckQuery(run.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var provision = r.Value.Provision.Should().ContainSingle(p => p.ProvisionCode == "PROV_PRIMA").Subject;
        provision.Accrued.Should().Be(6 * 150_000m);
        provision.Consumed.Should().Be(prima);
        provision.Difference.Should().Be(prima - 6 * 150_000m);
        provision.Released.Should().Be(0m);
        r.Value.Details.Should().Contain(x => x.Contains("Provisión PROV_PRIMA"));
    }

    [Fact]
    public async Task El_comparativo_de_una_especial_es_contra_su_version_anterior_y_sin_ella_previous_es_nulo()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre();
        var v1 = await LiquidacionDePrueba.PrimaAsync(d);
        var handler = new GetRunComparisonQueryHandler(d.Db, d.Policies);

        var sinAnterior = await handler.Handle(new GetRunComparisonQuery(v1.PublicId), CancellationToken.None);
        sinAnterior.IsSuccess.Should().BeTrue(sinAnterior.Error.Message);
        sinAnterior.Value.PreviousRunPublicId.Should().BeNull();
        sinAnterior.Value.PreviousPeriodPublicId.Should().BeNull();
        sinAnterior.Value.Rows.Should().ContainSingle().Which.PreviousNet.Should().BeNull();

        d.CambioDeSalario(d.Ana, new DateTime(2026, 4, 1), 3_000_000m);
        var v2 = await LiquidacionDePrueba.PrimaAsync(d, recalculo: true);
        var conAnterior = await handler.Handle(new GetRunComparisonQuery(v2.PublicId), CancellationToken.None);

        conAnterior.Value.PreviousRunPublicId.Should().Be(v1.PublicId);
        conAnterior.Value.PreviousPeriodLabel.Should().Contain("Versión 1");
        var fila = conAnterior.Value.Rows.Should().ContainSingle().Subject;
        fila.PreviousNet.Should().Be(v1.TotalNet);
        fila.CurrentNet.Should().Be(v2.TotalNet).And.BeGreaterThan(v1.TotalNet);
        fila.VariationPercent.Should().BePositive();
    }

    [Fact]
    public async Task El_comprobante_del_empleado_lleva_la_etiqueta_del_tipo()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre();
        LiquidacionDePrueba.ConContabilidad(d);
        var run = await LiquidacionDePrueba.PrimaAsync(d);
        (await d.Flujo(Contadora).ApproveAsync(new SettlementApprovalRequest(run.PublicId, PayrollRunKind.ServiceBonus, true), null, CancellationToken.None)).IsSuccess.Should().BeTrue();
        var tenant = Substitute.For<ICurrentTenantService>();
        tenant.TenantName.Returns("COOFLOPAL");

        var r = await new PayslipModelBuilder(d.Db, tenant, d.Clock).BuildAsync(run.PublicId, null, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var modelo = r.Value.Single().Model;
        modelo.PeriodLabel.Should().Be("Prima de servicios 2026-I");
        modelo.PeriodStart.Should().Be(new DateTime(2026, 1, 1));
        modelo.PeriodEnd.Should().Be(new DateTime(2026, 6, 30));
        modelo.Earnings.Should().Contain(l => l.Code == "PRIMA");
        modelo.PlanName.Should().Be("prima de servicios");
    }

    /// <summary>
    /// D-29 (spec US3 escenario 1, FR-020): el último tramo lo paga la definitiva como SALARIO_PENDIENTE,
    /// así que el empleado con definitiva aprobada dentro del período (o antes) no entra a la ordinaria de
    /// ese período. Hasta la revisión de N1 el cargador lo conservaba «por los días hasta el retiro» y el
    /// salario, el auxilio y las deducciones de ley de esos días salían dos veces. Si la definitiva no trajo
    /// ese tramo (no había período abierto al aprobarla), la ordinaria sí lo liquida por días.
    /// </summary>
    [Fact]
    public async Task La_nomina_ordinaria_excluye_al_retirado_con_definitiva_aprobada_antes_o_dentro_del_periodo_salvo_que_no_le_pagara_el_tramo()
    {
        var d = new NominaTestData();
        var motivo = new TerminationReason { Code = "RENUNCIA", Name = "Renuncia", IsSeeded = true, CreatedBy = "test" };
        d.Db.TerminationReasons.Add(motivo);
        await d.Db.SaveChangesAsync();
        // Luis: definitiva aprobada en febrero, la ficha aún no cerrada (Status vigente): FR-005 lo saca de marzo.
        var luis = d.Empleado("Luis", 1_800_000m, new DateTime(2025, 6, 1));
        Liquidado(d, luis.Id, new DateOnly(2026, 2, 20), motivo, conTramo: true);
        // Marta: se retira el 10 de marzo con definitiva aprobada que pagó del 1 al 10: no entra.
        var marta = d.Empleado("Marta", 1_800_000m, new DateTime(2025, 6, 1), retiro: new DateTime(2026, 3, 10));
        Liquidado(d, marta.Id, new DateOnly(2026, 3, 10), motivo, conTramo: true);
        // Pedro: se retira el 31 de marzo (último día) con definitiva aprobada que pagó el mes: tampoco entra, aunque la ficha no recorte días.
        var pedro = d.Empleado("Pedro", 1_800_000m, new DateTime(2025, 6, 1), retiro: new DateTime(2026, 3, 31));
        Liquidado(d, pedro.Id, new DateOnly(2026, 3, 31), motivo, conTramo: true);
        // Julia: definitiva aprobada el 12 de marzo cuando marzo no existía como período (sin SALARIO_PENDIENTE): entra por los días hasta el retiro.
        var julia = d.Empleado("Julia", 1_800_000m, new DateTime(2025, 6, 1), retiro: new DateTime(2026, 3, 12));
        Liquidado(d, julia.Id, new DateOnly(2026, 3, 12), motivo, conTramo: false);
        // Rosa: ficha cerrada el 20 de marzo por el camino anterior, sin definitiva: se liquida por los días hasta el retiro.
        var rosa = d.Empleado("Rosa", 1_800_000m, new DateTime(2025, 6, 1), retiro: new DateTime(2026, 3, 20));
        // Elena: definitiva aprobada con retiro en abril: marzo la liquida completa.
        var elena = d.Empleado("Elena", 1_800_000m, new DateTime(2025, 6, 1));
        Liquidado(d, elena.Id, new DateOnly(2026, 4, 5), motivo, conTramo: true);
        await d.Db.SaveChangesAsync();

        var batch = await d.Loader.LoadAsync(d.Marzo, CancellationToken.None);

        batch.Employees.Select(e => e.Employee.Id).Should().BeEquivalentTo([d.Ana.Id, julia.Id, rosa.Id, elena.Id],
            "Luis, Marta y Pedro tienen definitiva aprobada hasta el fin de marzo que ya pagó su último tramo (D-29)");
        batch.Employees.Single(e => e.Employee.Id == julia.Id).Input.TerminationDate.Should().Be(new DateTime(2026, 3, 12), "su definitiva no pagó marzo: la ordinaria liquida hasta el retiro");
        batch.Employees.Single(e => e.Employee.Id == rosa.Id).Input.TerminationDate.Should().Be(new DateTime(2026, 3, 20));
        batch.Employees.Single(e => e.Employee.Id == elena.Id).Input.TerminationDate.Should().BeNull();
        batch.Employees.Single(e => e.Employee.Id == d.Ana.Id).Input.TerminationDate.Should().BeNull();
    }

    /// <summary>Terminación Settled con su definitiva aprobada; con <paramref name="conTramo"/> la corrida trae la línea SALARIO_PENDIENTE.</summary>
    private static void Liquidado(NominaTestData d, int employeeId, DateOnly retiro, TerminationReason motivo, bool conTramo)
    {
        d.Db.EmploymentTerminations.Add(new EmploymentTermination { EmployeeId = employeeId, TerminationDate = retiro, TerminationReasonId = motivo.Id, Status = TerminationStatus.Settled, CreatedBy = "test" });
        var run = new Domain.Entities.Payroll.Transactions.PayrollRun
        {
            Kind = PayrollRunKind.Settlement, CutoffDate = retiro, EmployeeId = employeeId, Version = 1, Status = PayrollRunStatus.Approved,
            CalculatedBy = "contadora@demo", CalculatedAt = NominaTestData.Ahora, InputsHash = new string('d', 64), CreatedBy = "test",
        };
        var fila = new Domain.Entities.Payroll.Transactions.PayrollRunEmployee { EmployeeId = employeeId, PayrollPlanId = d.Plan.Id, CreatedBy = "test" };
        var pendiente = d.Db.PayrollConceptDefinitions.Single(c => c.Code == Domain.Payroll.Calculation.WellKnownConceptCodes.PendingSalary);
        if (conTramo)
            fila.Lines.Add(new Domain.Entities.Payroll.Transactions.PayrollRunLine { ConceptDefinitionId = pendiente.Id, ConceptCode = pendiente.Code, ConceptName = pendiente.Name, Nature = ConceptNature.Earning, Amount = 600_000m, Quantity = 10m, ExplanationJson = "{}", CreatedBy = "test" });
        run.Employees.Add(fila);
        d.Db.PayrollRuns.Add(run);
    }
}
