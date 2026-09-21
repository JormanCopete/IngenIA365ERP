using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Payroll.OpeningBalances;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Entities.Payroll.Transactions;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Policies;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.OpeningBalances;

/// <summary>
/// T033 (feature 010, R3, FR-007; contracts/api.md §4): el saldo inicial se digita y se reemplaza
/// mientras nada lo consumió; consumido, se ajusta con motivo; nunca negativo ni posterior a la
/// primera corrida aprobada; el listado sabe quién ingresó antes del arranque y no lo tiene.
/// </summary>
public class BenefitBalanceCommandsTests
{
    private readonly NominaTestData _d = new();

    private UpsertBenefitBalanceCommandHandler Upsert() => new(_d.Db, _d.Clock, _d.User, _d.AuditEmitter);
    private AddBenefitBalanceAdjustmentCommandHandler Ajuste() => new(_d.Db, _d.Clock, _d.User, _d.AuditEmitter);

    private UpsertBenefitBalanceCommand Apertura(decimal vacaciones = 10m, decimal cesantias = 1_500_000m, DateOnly? corte = null) =>
        new(_d.Ana.PublicId, corte ?? new DateOnly(2025, 12, 31), vacaciones, cesantias, 180_000m, 900_000m, Notes: "Del liquidador anterior");

    [Fact]
    public async Task Digitar_crea_la_apertura_y_volver_a_digitar_la_reemplaza_en_la_misma_fila()
    {
        var primera = await Upsert().Handle(Apertura(), CancellationToken.None);
        primera.IsSuccess.Should().BeTrue(primera.Error.Message);

        var segunda = await Upsert().Handle(Apertura(vacaciones: 12.5m, cesantias: 1_600_000m), CancellationToken.None);
        segunda.IsSuccess.Should().BeTrue(segunda.Error.Message);
        segunda.Value.Should().Be(primera.Value, "mientras nada lo consumió, es la misma fila corregida");

        var filas = await _d.Db.EmployeeBenefitOpeningBalances.Where(b => b.EmployeeId == _d.Ana.Id).ToListAsync();
        filas.Should().ContainSingle();
        filas[0].Kind.Should().Be(OpeningBalanceKind.Opening);
        filas[0].PendingVacationDays.Should().Be(12.5m);
        filas[0].AccruedSeverance.Should().Be(1_600_000m);
        filas[0].UpdatedBy.Should().Be("ana@demo");
        await _d.Audit.Received(2).AppendAsync(Arg.Is<AuditEventDocument>(e => e.Action == AuditEventTypes.PayrollOpeningBalanceChanged), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Un_valor_negativo_se_rechaza_con_su_codigo()
    {
        var r = await Upsert().Handle(Apertura(vacaciones: -1m), CancellationToken.None);

        r.Error.Code.Should().Be("Payroll.BenefitBalance.NegativeValue");
        r.Error.Message.Should().Contain("vacaciones");
        new UpsertBenefitBalanceCommandValidator().Validate(Apertura(cesantias: -5m)).IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Un_corte_posterior_a_la_primera_corrida_aprobada_del_empleado_se_rechaza()
    {
        var run = _d.Borrador(_d.Marzo);
        run.Status = PayrollRunStatus.Approved;
        _d.Db.PayrollRunEmployees.Add(new PayrollRunEmployee { PayrollRunId = run.Id, EmployeeId = _d.Ana.Id, PayrollPlanId = _d.Plan.Id, DaysWorked = 30 });
        await _d.Db.SaveChangesAsync();

        var tarde = await Upsert().Handle(Apertura(corte: new DateOnly(2026, 3, 15)), CancellationToken.None);
        tarde.Error.Code.Should().Be("Payroll.BenefitBalance.AsOfAfterFirstRun");
        tarde.Error.Message.Should().Contain("01/03/2026");

        var aTiempo = await Upsert().Handle(Apertura(corte: new DateOnly(2026, 2, 28)), CancellationToken.None);
        aTiempo.IsSuccess.Should().BeTrue(aTiempo.Error.Message);
    }

    private async Task<PayrollRun> ConsumirAsync(EmployeeBenefitOpeningBalance saldo)
    {
        var prima = new PayrollRun
        {
            Kind = PayrollRunKind.ServiceBonus, Version = 1, Status = PayrollRunStatus.Approved, CutoffDate = new DateOnly(2026, 6, 30), Year = 2026, Semester = 1,
            CalculatedAt = NominaTestData.Ahora, CalculatedBy = "ana@demo", InputsHash = new string('b', 64), CreatedBy = "test",
        };
        _d.Db.PayrollRuns.Add(prima);
        await _d.Db.SaveChangesAsync();
        saldo.ConsumedByRunId = prima.Id;
        await _d.Db.SaveChangesAsync();
        return prima;
    }

    [Fact]
    public async Task Consumido_no_se_reemplaza_y_el_error_nombra_la_corrida()
    {
        var creado = await Upsert().Handle(Apertura(), CancellationToken.None);
        var saldo = await _d.Db.EmployeeBenefitOpeningBalances.SingleAsync(b => b.PublicId == creado.Value);
        var prima = await ConsumirAsync(saldo);
        _d.Db.DescartarCambios();

        var r = await Upsert().Handle(Apertura(vacaciones: 20m), CancellationToken.None);

        r.IsSuccess.Should().BeFalse();
        r.Error.Code.Should().Be("Payroll.BenefitBalance.Consumed");
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { runPublicIds = new[] { prima.PublicId } });
        (await _d.Db.EmployeeBenefitOpeningBalances.AsNoTracking().SingleAsync(b => b.PublicId == creado.Value)).PendingVacationDays.Should().Be(10m);
    }

    [Fact]
    public async Task Consumido_se_corrige_con_un_ajuste_con_motivo_que_apunta_a_la_fila_anterior_y_pasa_a_ser_el_vigente()
    {
        var creado = await Upsert().Handle(Apertura(), CancellationToken.None);
        var saldo = await _d.Db.EmployeeBenefitOpeningBalances.SingleAsync(b => b.PublicId == creado.Value);
        await ConsumirAsync(saldo);
        _d.Db.DescartarCambios();

        var sinMotivo = new AddBenefitBalanceAdjustmentCommandValidator().Validate(
            new AddBenefitBalanceAdjustmentCommand(_d.Ana.PublicId, new DateOnly(2025, 12, 31), 11m, 1_500_000m, 180_000m, 900_000m, ""));
        sinMotivo.IsValid.Should().BeFalse();

        var r = await Ajuste().Handle(new AddBenefitBalanceAdjustmentCommand(_d.Ana.PublicId, new DateOnly(2025, 12, 31), 11m, 1_500_000m, 180_000m, 900_000m,
            "Certificado del fondo con un día más de vacaciones"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var ajuste = await _d.Db.EmployeeBenefitOpeningBalances.AsNoTracking().SingleAsync(b => b.PublicId == r.Value);
        ajuste.Kind.Should().Be(OpeningBalanceKind.Adjustment);
        ajuste.AdjustsBalanceId.Should().Be(saldo.Id);
        ajuste.AdjustmentReason.Should().Contain("Certificado");
        ajuste.ConsumedByRunId.Should().BeNull("la liquidación siguiente lo consumirá");

        var detalle = await new GetBenefitBalanceQueryHandler(_d.Db, _d.Policies, _d.Clock).Handle(new GetBenefitBalanceQuery(_d.Ana.PublicId), CancellationToken.None);
        detalle.Value.Current!.PublicId.Should().Be(ajuste.PublicId);
        detalle.Value.Current.PendingVacationDays.Should().Be(11m);
        detalle.Value.History.Should().HaveCount(2);
        detalle.Value.History[0].AdjustsBalancePublicId.Should().Be(saldo.PublicId);
        detalle.Value.History[1].ConsumedBy.Should().NotBeNull();

        var repetido = await Ajuste().Handle(new AddBenefitBalanceAdjustmentCommand(_d.Ana.PublicId, new DateOnly(2025, 12, 31), 11m, 1_500_000m, 180_000m, 900_000m, "otro"), CancellationToken.None);
        repetido.Error.Code.Should().Be("Payroll.BenefitBalance.AsOfDuplicate", "el índice único es (empleado, corte, tipo)");
    }

    [Fact]
    public async Task Sin_saldo_previo_no_hay_nada_que_ajustar()
    {
        var r = await Ajuste().Handle(new AddBenefitBalanceAdjustmentCommand(_d.Ana.PublicId, new DateOnly(2025, 12, 31), 11m, 0m, 0m, 0m, "x"), CancellationToken.None);
        r.Error.Code.Should().Be("Payroll.BenefitBalance.NoBalanceToAdjust");
    }

    [Fact]
    public async Task El_listado_marca_a_quien_ingreso_antes_del_arranque_sin_saldo_y_el_filtro_deja_solo_a_esos()
    {
        // Arranque de la nómina en la plataforma: 2026-01-01 (política). Ana ingresó en 2025; Beto, después del arranque.
        _d.Db.CompanyPolicies.Add(new CompanyPolicy { Key = CompanyPolicyKeys.ArranqueNominaFecha, Value = "2026-01-01", ValidFrom = new DateOnly(2026, 1, 1), CreatedBy = "seed" });
        await _d.Db.SaveChangesAsync();
        var beto = _d.Empleado("Beto", 1_800_000m, new DateTime(2026, 2, 1));
        var carla = _d.Empleado("Carla", 2_200_000m, new DateTime(2024, 6, 1));
        await Upsert().Handle(new UpsertBenefitBalanceCommand(carla.PublicId, new DateOnly(2025, 12, 31), 5m, 0m, 0m, 0m), CancellationToken.None);

        var todos = await new ListBenefitBalancesQueryHandler(_d.Db, _d.Policies, _d.Clock).Handle(new ListBenefitBalancesQuery(), CancellationToken.None);
        todos.Value.Should().HaveCount(3);
        todos.Value.Single(x => x.EmployeePublicId == _d.Ana.PublicId).Should().Match<BenefitBalanceSummaryDto>(x => x.HiredBeforeStart && x.AsOfDate == null && x.IsEditable);
        todos.Value.Single(x => x.EmployeePublicId == beto.PublicId).HiredBeforeStart.Should().BeFalse();
        todos.Value.Single(x => x.EmployeePublicId == carla.PublicId).AsOfDate.Should().Be(new DateOnly(2025, 12, 31));

        var faltantes = await new ListBenefitBalancesQueryHandler(_d.Db, _d.Policies, _d.Clock).Handle(new ListBenefitBalancesQuery(OnlyMissing: true), CancellationToken.None);
        faltantes.Value.Should().ContainSingle().Which.EmployeePublicId.Should().Be(_d.Ana.PublicId);
    }

    [Fact]
    public async Task Sin_politica_de_arranque_manda_el_primer_periodo_de_la_cooperativa()
    {
        // Marzo 2026 es el único período: Ana (ingreso 2025-01-15) queda antes del arranque.
        var faltantes = await new ListBenefitBalancesQueryHandler(_d.Db, _d.Policies, _d.Clock).Handle(new ListBenefitBalancesQuery(OnlyMissing: true), CancellationToken.None);
        faltantes.Value.Should().ContainSingle().Which.EmployeePublicId.Should().Be(_d.Ana.PublicId);

        var detalle = await new GetBenefitBalanceQueryHandler(_d.Db, _d.Policies, _d.Clock).Handle(new GetBenefitBalanceQuery(_d.Ana.PublicId), CancellationToken.None);
        detalle.Value.PayrollStartDate.Should().Be(new DateOnly(2026, 3, 1));
        detalle.Value.HiredBeforeStart.Should().BeTrue();
        detalle.Value.Current.Should().BeNull();
    }
}
