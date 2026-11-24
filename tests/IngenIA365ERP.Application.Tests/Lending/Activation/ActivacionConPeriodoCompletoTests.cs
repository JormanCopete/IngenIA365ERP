using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.AccrualProcesses;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Application.Tests.Lending.Accrual;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Activation;

/// <summary>
/// 2026-11-24 (feature 025, fase 4, HU4; FR-014, FR-015, D5): la activación de una carga con la opción «el período en curso al corte se causa completo»
/// sobre la cartera de <see cref="ActivacionConCarteraDePrueba"/> (corte el 2026-12-01, activación el día siguiente): <c>CR-200</c> de Beto, al día, cuya
/// cuota 1 vence dos días después del corte y nació un mes antes; <c>CR-100</c> de Ana, que llega con intereses en suspenso; <c>CO-300</c> castigado.
/// <list type="bullet">
/// <item>con la opción, la cuota 1 de <c>CR-200</c> lleva el interés del período completo: lo anterior al corte entra el día de la activación (fila
/// <c>CutoverInterestAdjustment</c>, hecho <c>InteresDelCorte</c>), el plan muestra el período desde el desembolso, el crédito queda marcado y la vista
/// previa de la carga dijo lo mismo; el suspendido y el castigado quedan como hoy;</item>
/// <item>el interés que la cuota trae en la carga cuenta como causado;</item>
/// <item>sin la opción, el comportamiento de hoy (D5): nada de esto;</item>
/// <item>el recálculo de la carga (fase 3) no lo vuelve a tomar y la causación y la verificación de la noche no lo revierten.</item>
/// </list>
/// </summary>
public sealed class ActivacionConPeriodoCompletoTests : IAsyncLifetime
{
    private ActivacionConCarteraDePrueba _a = null!;
    private CausacionDePrueba _c = null!;

    public async Task InitializeAsync()
    {
        _a = await ActivacionConCarteraDePrueba.CrearAsync();
        _c = CausacionDePrueba.Sobre(_a.D);
        await _c.SembrarTiposDeHechoAsync();
        _a.Db.ChangeTracker.Clear();
    }

    public Task DisposeAsync()
    {
        _a.Dispose();
        return Task.CompletedTask;
    }

    private IngenIA365ERP.Application.Tests.Common.TestApplicationDbContext Db => _a.Db;

    private static readonly DateOnly Corte = ActivacionConCarteraDePrueba.Corte;
    private static readonly DateOnly Activacion = ActivacionConCarteraDePrueba.Activacion;

    /// <summary>Enciende (o apaga) la opción en la carga y, si se pide, le pone interés cargado a la cuota 1 de <c>CR-200</c> (en la cuota y en el saldo).</summary>
    private async Task PrepararAsync(bool conOpcion, decimal cargadoEnBeto = 0m)
    {
        var carga = await Db.MigrationLoads.SingleAsync(l => l.Id == _a.Carga.Id);
        carga.CauseFullInProgressPeriod = conOpcion;
        if (cargadoEnBeto > 0m)
        {
            var cuota = await Db.MigrationInstallmentRows.SingleAsync(r => r.SourceLoanNumber == "CR-200" && r.InstallmentNumber == 1);
            Db.MigrationInstallmentRows.Remove(cuota);
            Db.MigrationInstallmentRows.Add(new Domain.Entities.Lending.Migration.MigrationInstallmentRow
            {
                MigrationLoadPartId = cuota.MigrationLoadPartId, RowNumber = cuota.RowNumber, SourceLoanNumber = cuota.SourceLoanNumber,
                InstallmentNumber = cuota.InstallmentNumber, DueDate = cuota.DueDate, Principal = cuota.Principal, CurrentInterest = cargadoEnBeto,
            });
            var saldo = await Db.MigrationBalanceRows.FirstAsync(r => r.SourceLoanNumber == "CR-200");
            Db.MigrationBalanceRows.Add(new Domain.Entities.Lending.Migration.MigrationBalanceRow
            {
                MigrationLoadPartId = saldo.MigrationLoadPartId, RowNumber = 900, SourceLoanNumber = "CR-200", Concept = LoanConcept.CurrentInterest,
                Amount = cargadoEnBeto,
            });
        }
        await Db.SaveChangesAsync();
        Db.ChangeTracker.Clear();
        await _a.AplicarAsync();
        await _a.AceptarTodoAsync();
    }

    private async Task<LendingActivationDto> ActivarAsync()
    {
        var r = await _a.ActivarAsync(_a.Orden(_a.Acta()));
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    private async Task<MigrationFullPeriodInterestDto> VistaPreviaAsync()
    {
        var r = await new GetMigrationFullPeriodInterestQueryHandler(Db).Handle(new GetMigrationFullPeriodInterestQuery(_a.Carga.PublicId), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    private Task<Domain.Entities.Lending.Loans.Loan> CreditoAsync(string numero) => Db.Loans.AsNoTracking().SingleAsync(l => l.Number == numero);

    // ------------------------------------------------------------------------------------------------------------ pruebas --

    [Fact]
    public async Task Con_la_opcion_la_primera_cuota_lleva_el_periodo_completo_y_lo_anterior_entra_el_dia_de_la_activacion()
    {
        await PrepararAsync(conOpcion: true);
        var previa = await VistaPreviaAsync();
        previa.CauseFullInProgressPeriod.Should().BeTrue();
        var deBeto = previa.Items.Should().ContainSingle(i => i.LoanNumber == "CR-200").Subject;
        (deBeto.Installment, deBeto.PeriodStart, deBeto.MissingDays).Should().Be((1, Corte.AddDays(-30), 30), "la cuota 1 empezó con el desembolso");
        deBeto.Added.Should().Be(deBeto.FullPeriodInterest - deBeto.FromCutoff - deBeto.Loaded).And.BeGreaterThan(1m);
        previa.Items.Should().NotContain(i => i.LoanNumber == "CR-100", "llega con intereses en suspenso: lo anterior al corte no es ingreso");
        previa.Total.Should().Be(previa.Items.Sum(i => i.Added));

        var activada = await ActivarAsync();

        activada.FullPeriodInterest.Should().NotBeNull();
        activada.FullPeriodInterest!.Loans.Should().Be(1);
        activada.FullPeriodInterest.Total.Should().Be(deBeto.Added, "la activación da lo mismo que la vista previa para un crédito al día");
        var beto = await CreditoAsync("CR-200");
        var fila = await Db.LoanTransactions.AsNoTracking().SingleAsync(t => t.LoanId == beto.Id && t.Kind == LoanTransactionKind.CutoverInterestAdjustment);
        (fila.Concept, fila.InstallmentNumber, fila.Amount, fila.ValueDate).Should().Be((LoanConcept.CurrentInterest, (int?)1, deBeto.Added, Activacion),
            "ingreso del día de la activación, en la misma cuota (vence después)");
        fila.ProcessRunId.Should().NotBeNull();
        fila.MessagePublicId.Should().NotBeNull("lleva su hecho InteresDelCorte");
        (await Db.IntegrationMessages.CountAsync(m => m.Type == InteresDelCorteV1.Type)).Should().Be(1);
        (await Db.LoanControls.AsNoTracking().SingleAsync(c => c.LoanId == beto.Id)).CutoverAdjustedAt.Should().NotBeNull();

        // El plan migrado muestra el período desde el desembolso; el plan nuevo, el interés completo en la cuota 1.
        var migrado = await Db.RepaymentScheduleVersions.AsNoTracking().SingleAsync(v => v.LoanId == beto.Id && v.IsMigratedPlan);
        (await Db.RepaymentInstallments.AsNoTracking().SingleAsync(i => i.ScheduleVersionId == migrado.Id && i.InstallmentNumber == 1))
            .PeriodStart.Should().Be(Corte.AddDays(-30));
        var nuevo = await Db.RepaymentScheduleVersions.AsNoTracking().Where(v => v.LoanId == beto.Id).OrderByDescending(v => v.VersionNumber).FirstAsync();
        nuevo.IsMigratedPlan.Should().BeFalse();
        (await Db.RepaymentInstallments.AsNoTracking().SingleAsync(i => i.ScheduleVersionId == nuevo.Id && i.InstallmentNumber == 1))
            .Interest.Should().Be(deBeto.FullPeriodInterest, "la cuota 1 lleva el interés de su período completo");

        // Al vencer, la cuota 1 debe el interés del período completo, sin mora.
        var vence = Corte.AddDays(2);
        var alVencer = MotorDeCartera.Reconstruir((await _c.Cargador().CargarAsync(beto.Id, null, vence, default))!.Entrada!, vence);
        alVencer.Saldo(LoanConcept.CurrentInterest, 1).Should().Be(deBeto.FullPeriodInterest);
        alVencer.AlturaDeMora.Dias.Should().Be(0);

        // El suspendido y el castigado, como hoy.
        var ana = await CreditoAsync("CR-100");
        var castigado = await CreditoAsync("CO-300");
        (await Db.LoanTransactions.AnyAsync(t => (t.LoanId == ana.Id || t.LoanId == castigado.Id) && t.Kind == LoanTransactionKind.CutoverInterestAdjustment))
            .Should().BeFalse();
        (await Db.LoanControls.AsNoTracking().Where(c => c.LoanId == ana.Id || c.LoanId == castigado.Id).AllAsync(c => c.CutoverAdjustedAt == null))
            .Should().BeTrue("quedan sin marca: el recálculo de la carga puede tomarlos después");
    }

    [Fact]
    public async Task El_interes_que_la_cuota_trae_en_la_carga_cuenta_como_causado()
    {
        await PrepararAsync(conOpcion: true, cargadoEnBeto: 5_000m);
        var deBeto = (await VistaPreviaAsync()).Items.Single(i => i.LoanNumber == "CR-200");
        deBeto.Loaded.Should().Be(5_000m);
        deBeto.Added.Should().Be(deBeto.FullPeriodInterest - deBeto.FromCutoff - 5_000m);

        await ActivarAsync();

        var beto = await CreditoAsync("CR-200");
        (await Db.LoanTransactions.AsNoTracking().SingleAsync(t => t.LoanId == beto.Id && t.Kind == LoanTransactionKind.CutoverInterestAdjustment))
            .Amount.Should().Be(deBeto.Added);
        var vence = Corte.AddDays(2);
        MotorDeCartera.Reconstruir((await _c.Cargador().CargarAsync(beto.Id, null, vence, default))!.Entrada!, vence)
            .Saldo(LoanConcept.CurrentInterest, 1).Should().Be(deBeto.FullPeriodInterest, "lo cargado más lo del corte más lo causado: el período completo");
    }

    [Fact]
    public async Task Sin_la_opcion_el_interes_nace_desde_el_corte_como_hoy()
    {
        await PrepararAsync(conOpcion: false);
        var previa = await VistaPreviaAsync();
        previa.CauseFullInProgressPeriod.Should().BeFalse();
        previa.Items.Should().Contain(i => i.LoanNumber == "CR-200", "la vista previa dice lo que sumaría");

        var activada = await ActivarAsync();

        activada.FullPeriodInterest.Should().BeNull();
        (await Db.LoanTransactions.AnyAsync(t => t.Kind == LoanTransactionKind.CutoverInterestAdjustment)).Should().BeFalse();
        (await Db.LoanControls.AnyAsync(c => c.CutoverAdjustedAt != null)).Should().BeFalse();
        (await Db.IntegrationMessages.AnyAsync(m => m.Type == InteresDelCorteV1.Type)).Should().BeFalse();
        (await Db.LendingProcessRuns.AnyAsync(r => r.Kind == LendingProcessKind.CutoverRecalculation)).Should().BeFalse();
        var beto = await CreditoAsync("CR-200");
        var migrado = await Db.RepaymentScheduleVersions.AsNoTracking().SingleAsync(v => v.LoanId == beto.Id && v.IsMigratedPlan);
        (await Db.RepaymentInstallments.AsNoTracking().SingleAsync(i => i.ScheduleVersionId == migrado.Id && i.InstallmentNumber == 1))
            .PeriodStart.Should().Be(Corte, "sin la opción el período de la primera cuota empieza en el corte (D5)");
        (await Db.RepaymentScheduleVersions.CountAsync(v => v.LoanId == beto.Id)).Should().Be(1);
    }

    [Fact]
    public async Task El_recalculo_de_la_carga_no_lo_vuelve_a_tomar()
    {
        await PrepararAsync(conOpcion: true);
        await ActivarAsync();
        var beto = await CreditoAsync("CR-200");
        var hoy = _c.D.G.HoyLocal;

        var clase = new RecalculoDelCorte(Db, _c.Cargador(), _c.Partes(), _c.D.Registro(), _c.D.O.Cerrojo, _c.D.FechaContable(), _c.D.G.Actor, _c.D.G.Reloj);
        var calculo = await clase.CalcularAsync(new ContextoDelCalculo(new ParametrosDelProceso(AccrualProcessKind.CutoverRecalculation,
            AccrualProcessPeriodicity.Daily, null, hoy, hoy, AccrualProcessConcepts.CurrentInterest, null, null, null), AlcanceDeCartera.Total, hoy), default);

        calculo.IsSuccess.Should().BeTrue();
        calculo.Value.Lineas.Single(l => l.CreditoId == beto.Id && l.Tipo == InteresDelCorteV1.Type).Motivo
            .Should().Be(AccrualExclusionReason.AlreadyAdjusted, "la HU1 y la HU4 no se suman");
    }

    [Fact]
    public async Task La_causacion_y_la_verificacion_de_la_noche_no_lo_revierten()
    {
        await PrepararAsync(conOpcion: true);
        var activada = await ActivarAsync();
        var beto = await CreditoAsync("CR-200");

        var hasta = _c.D.G.HoyLocal.AddDays(1);
        await _c.CausarHastaAsync(hasta);
        Db.ChangeTracker.Clear();

        var libro = await _c.Cargador().LibroAsync([beto.Id], hasta, default);
        var entrada = (await _c.Cargador().CargarAsync(beto.Id, null, hasta, default))!.Entrada!;
        MotorDeCartera.Verificar(entrada, (await _c.ControlAsync(beto.Id)).LastAccruedDate, libro[beto.Id]).Should()
            .BeEmpty("el libro sigue igual a la reconstrucción: el ajuste de la activación es su entrada");
        (await Db.LoanTransactions.AsNoTracking()
                .Where(t => t.LoanId == beto.Id && t.Concept == LoanConcept.CurrentInterest && t.InstallmentNumber == 1 && t.Kind != LoanTransactionKind.Accrual
                            && t.Kind != LoanTransactionKind.OpeningBalance)
                .SumAsync(t => t.Amount))
            .Should().Be((await Db.LoanTransactions.AsNoTracking()
                .SingleAsync(t => t.LoanId == beto.Id && t.Kind == LoanTransactionKind.CutoverInterestAdjustment)).Amount,
                "nada revirtió el interés del período completo");
        activada.FullPeriodInterest!.Loans.Should().Be(1);
    }
}
