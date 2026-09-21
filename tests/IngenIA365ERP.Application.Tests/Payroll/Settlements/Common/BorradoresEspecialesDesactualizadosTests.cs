using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Payroll.Novelties.RegisterSalaryChange;
using IngenIA365ERP.Application.Payroll.OpeningBalances;
using IngenIA365ERP.Application.Payroll.Runs.ApprovePayrollRun;
using IngenIA365ERP.Application.Payroll.Runs.CalculatePayrollRun;
using IngenIA365ERP.Application.Payroll.Runs.ReversePayrollRun;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.Common;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Tests.Payroll.Settlements.Common;

/// <summary>
/// Feature 010, revisión N1 (2026-09-21): un borrador de prima, cesantías, vacaciones o definitiva no tiene
/// período, así que ningún marcador por período lo alcanzaba, y aprobar contabiliza las líneas guardadas
/// tal cual: si entre el cálculo y la aprobación se aprobaba o reversaba una ordinaria de su rango,
/// cambiaba un salario o un saldo inicial del empleado, el ajuste de provisión iba viejo y la provisión
/// no volvía a cero. Ahora esos cuatro disparadores dejan el borrador <c>Stale</c> y la aprobación lo
/// rechaza (<c>NotDraft</c>) hasta recalcular.
/// </summary>
public class BorradoresEspecialesDesactualizadosTests
{
    private static readonly ICurrentUserService Contadora = NominaTestData.UsuarioDePrueba("contadora@demo", 9);

    private static async Task<PayrollRunStatus> EstadoAsync(NominaTestData d, PayrollRun run) =>
        (await d.Db.PayrollRuns.AsNoTracking().SingleAsync(r => r.Id == run.Id)).Status;

    /// <summary>Enero, febrero, abril y mayo aprobados; marzo (el de la semilla) queda abierto para calcularlo y aprobarlo por los comandos.</summary>
    private static NominaTestData ConSemestreMenosMarzo()
    {
        var d = new NominaTestData();
        foreach (var mes in new[] { 1, 2, 4, 5 })
            d.MesAprobado(2026, mes, d.Ana, 2_000_000m, 249_095m, provPrima: 187_424m, provCesantias: 187_424m, provIntereses: 22_491m, provVacaciones: 83_333m);
        d.ConfigurarContabilidad();
        d.PeriodoContable(2026, 6);
        d.PeriodoContable(2026, 7);
        d.HoyEs(new DateTime(2026, 7, 10, 12, 0, 0));
        return d;
    }

    private static async Task<Guid> CalcularMarzoAsync(NominaTestData d)
    {
        var r = await new CalculatePayrollRunCommandHandler(d.Db, d.Loader, d.Recurrentes, d.Lock, d.Clock, d.User, NullLogger<CalculatePayrollRunCommandHandler>.Instance)
            .Handle(new CalculatePayrollRunCommand(d.Marzo.PublicId), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.Error.Message);
        return r.Value.RunPublicId;
    }

    private static Task<Application.Common.Models.Result<ApproveRunResultDto>> AprobarOrdinariaAsync(NominaTestData d, Guid runId) =>
        new ApprovePayrollRunCommandHandler(d.Db, d.Contabilizador(Contadora), d.Policies, d.Permissions, d.Clock, Contadora,
                new PayrollAuditEmitter(d.Audit, Contadora, d.Clock, NullLogger<PayrollAuditEmitter>.Instance), d.StaleMarker)
            .Handle(new ApprovePayrollRunCommand(runId, Confirm: true), CancellationToken.None);

    [Fact]
    public async Task Aprobar_y_reversar_una_ordinaria_del_rango_dejan_Stale_los_borradores_de_prima_y_cesantias_del_empleado()
    {
        var d = ConSemestreMenosMarzo();
        var prima = await LiquidacionDePrueba.PrimaAsync(d);
        var cesantias = await LiquidacionDePrueba.CesantiasAsync(d);
        var marzo = await CalcularMarzoAsync(d);

        var aprobada = await AprobarOrdinariaAsync(d, marzo);

        aprobada.IsSuccess.Should().BeTrue(aprobada.Error.Message);
        (await EstadoAsync(d, prima)).Should().Be(PayrollRunStatus.Stale, "marzo aporta provisión y base a la prima 2026-I calculada antes");
        (await EstadoAsync(d, cesantias)).Should().Be(PayrollRunStatus.Stale);
        // Y el ciclo común ya no la aprueba tal cual: hay que recalcular.
        var aprobarVieja = await d.Flujo(Contadora).ApproveAsync(new SettlementApprovalRequest(prima.PublicId, PayrollRunKind.ServiceBonus, Confirm: true), null, CancellationToken.None);
        aprobarVieja.Error.Code.Should().Be("Payroll.Settlement.NotDraft");

        // Recalculada con marzo adentro, vuelve a ser borrador; reversar marzo la vuelve a desactualizar.
        var primaNueva = await LiquidacionDePrueba.PrimaAsync(d, recalculo: true);
        (await EstadoAsync(d, primaNueva)).Should().Be(PayrollRunStatus.Draft);
        var reversada = await new ReversePayrollRunCommandHandler(d.Db, d.Contabilizador(Contadora), d.Clock, Contadora,
                new PayrollAuditEmitter(d.Audit, Contadora, d.Clock, NullLogger<PayrollAuditEmitter>.Instance), d.StaleMarker)
            .Handle(new ReversePayrollRunCommand(marzo, "Se digitó mal una novedad"), CancellationToken.None);
        reversada.IsSuccess.Should().BeTrue(reversada.Error.Message);
        (await EstadoAsync(d, primaNueva)).Should().Be(PayrollRunStatus.Stale);
    }

    [Fact]
    public async Task Un_cambio_de_salario_con_efecto_dentro_del_rango_deja_Stale_el_borrador_y_uno_posterior_al_corte_no()
    {
        // Enero a mayo aprobados; junio abierto (un cambio de salario sólo entra en período abierto, FR-004).
        var d = new NominaTestData();
        for (var mes = 1; mes <= 5; mes++)
            d.MesAprobado(2026, mes, d.Ana, 2_000_000m, 249_095m, provPrima: 187_424m, provCesantias: 187_424m, provIntereses: 22_491m, provVacaciones: 83_333m);
        d.Periodo(new DateTime(2026, 6, 1), new DateTime(2026, 6, 30), PayPeriodStatus.Open);
        d.Periodo(new DateTime(2026, 7, 1), new DateTime(2026, 7, 31), PayPeriodStatus.Open);
        d.HoyEs(new DateTime(2026, 7, 10, 12, 0, 0));
        var prima = await LiquidacionDePrueba.PrimaAsync(d);
        var handler = new RegisterSalaryChangeCommandHandler(d.Db, d.Clock, d.User, d.StaleMarker, d.AuditEmitter);

        var despues = await handler.Handle(new RegisterSalaryChangeCommand(d.Ana.PublicId, 2_100_000m, new DateTime(2026, 7, 1), "Aumento"), CancellationToken.None);
        despues.IsSuccess.Should().BeTrue(despues.Error.Message);
        (await EstadoAsync(d, prima)).Should().Be(PayrollRunStatus.Draft, "el corte de la prima 2026-I es el 30-06: un salario desde julio no la toca");

        var dentro = await handler.Handle(new RegisterSalaryChangeCommand(d.Ana.PublicId, 2_200_000m, new DateTime(2026, 6, 1), "Retroactivo"), CancellationToken.None);
        dentro.IsSuccess.Should().BeTrue(dentro.Error.Message);
        (await EstadoAsync(d, prima)).Should().Be(PayrollRunStatus.Stale, "la base de la prima cambió");
    }

    [Fact]
    public async Task Digitar_o_ajustar_el_saldo_inicial_del_empleado_deja_Stale_su_borrador()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre();
        var prima = await LiquidacionDePrueba.PrimaAsync(d);

        var apertura = await new UpsertBenefitBalanceCommandHandler(d.Db, d.Clock, d.User, d.AuditEmitter, d.StaleMarker)
            .Handle(new UpsertBenefitBalanceCommand(d.Ana.PublicId, new DateOnly(2025, 12, 31), 5m, 1_000_000m, 120_000m, 500_000m), CancellationToken.None);
        apertura.IsSuccess.Should().BeTrue(apertura.Error.Message);
        (await EstadoAsync(d, prima)).Should().Be(PayrollRunStatus.Stale);

        var primaNueva = await LiquidacionDePrueba.PrimaAsync(d, recalculo: true);
        var ajuste = await new AddBenefitBalanceAdjustmentCommandHandler(d.Db, d.Clock, d.User, d.AuditEmitter, d.StaleMarker)
            .Handle(new AddBenefitBalanceAdjustmentCommand(d.Ana.PublicId, new DateOnly(2026, 1, 31), 5m, 1_100_000m, 120_000m, 550_000m, Reason: "Faltaba un tramo"), CancellationToken.None);
        ajuste.IsSuccess.Should().BeTrue(ajuste.Error.Message);
        (await EstadoAsync(d, primaNueva)).Should().Be(PayrollRunStatus.Stale);
    }

    [Fact]
    public async Task El_marcador_solo_toca_borradores_especiales_del_empleado_con_corte_desde_la_fecha()
    {
        var d = LiquidacionDePrueba.ConPrimerSemestre();
        var prima = await LiquidacionDePrueba.PrimaAsync(d);
        var ordinario = d.Borrador(d.Marzo);
        var otro = d.Empleado("Otro", 1_500_000m, new DateTime(2024, 1, 1));

        (await d.StaleMarker.MarkSettlementDraftsStaleAsync([otro.Id], new DateOnly(2026, 1, 1), "x", CancellationToken.None)).Should().Be(0, "otro empleado");
        (await d.StaleMarker.MarkSettlementDraftsStaleAsync([d.Ana.Id], new DateOnly(2026, 7, 1), "x", CancellationToken.None)).Should().Be(0, "después del corte");
        (await d.StaleMarker.MarkSettlementDraftsStaleAsync([], new DateOnly(2026, 1, 1), "x", CancellationToken.None)).Should().Be(0, "sin empleados");
        (await EstadoAsync(d, prima)).Should().Be(PayrollRunStatus.Draft);

        (await d.StaleMarker.MarkSettlementDraftsStaleAsync([d.Ana.Id, otro.Id], new DateOnly(2026, 6, 30), "x", CancellationToken.None)).Should().Be(1);
        await d.Db.SaveChangesAsync();
        (await EstadoAsync(d, prima)).Should().Be(PayrollRunStatus.Stale);
        (await EstadoAsync(d, ordinario)).Should().Be(PayrollRunStatus.Draft, "los borradores ordinarios van por período (MarkStaleAsync)");
    }
}
