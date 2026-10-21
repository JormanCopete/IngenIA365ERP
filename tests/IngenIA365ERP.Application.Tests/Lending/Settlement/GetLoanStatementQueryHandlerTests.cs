using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Lending.Settlement;
using IngenIA365ERP.Application.Tests.Lending.Accrual;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Settlement;

/// <summary>
/// Feature 013, US4, T0477 (FR-069; api.md §8.1): el estado de cuenta a (D, R) desde la reconstrucción: cuotas pagadas, vencidas y por vencer con lo
/// que debe cada una, días de mora, saldos por concepto, lo que cuesta cancelar, las versiones del plan, las marcas y los no aplicados de
/// <c>LND_InstallmentNonDeductions</c>; la calificación y la etapa quedan vacías hasta US9 y K4. Consultar no escribe y se audita.
/// </summary>
public class GetLoanStatementQueryHandlerTests : IAsyncLifetime
{
    private CausacionDePrueba _c = null!;
    private static readonly DateOnly Hoy = CausacionDePrueba.Hoy;

    public async Task InitializeAsync() => _c = await CausacionDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task El_estado_de_cuenta_trae_las_cuotas_la_mora_los_saldos_las_versiones_y_los_no_aplicados()
    {
        var d = new DateOnly(2027, 1, 20);
        _c.Ir(d);
        var cuota = await _c.Db.RepaymentInstallments.FirstAsync(i => i.InstallmentNumber == 1);
        _c.Db.InstallmentNonDeductions.Add(new InstallmentNonDeduction
        {
            LoanId = _c.Credito.Id, RepaymentInstallmentId = cuota.Id, InstallmentNumber = 1, DueDate = cuota.DueDate, Origin = default, Reason = default,
            ReasonText = "Sin saldo en la nómina", ExpectedAmount = cuota.Total, NotDeductedAmount = cuota.Total,
        });
        await _c.Db.SaveChangesAsync();

        var r = await _c.EstadoDeCuenta().Handle(new GetLoanStatementQuery(_c.Credito.PublicId, d), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var e = r.Value;
        e.Installments.Should().HaveCount(24);
        e.Installments[0].Status.Should().Be(EstadosDeCuota.Vencida);
        e.Installments[0].Owed.Should().ContainKey("Principal").And.ContainKey("DefaultInterest");
        e.Installments[1].Status.Should().Be(EstadosDeCuota.PorVencer);
        e.DaysPastDue.Should().BePositive();
        (e.OldestUnpaidInstallment, e.InstallmentsPastDue).Should().Be((1, 1));
        e.Balances["Principal"].Should().Be(3_000_000m);
        e.ToPayOff.Should().Be(e.Balances.Values.Sum());
        e.ScheduleVersions.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Version = 1, Cause = "Origination", InstallmentCount = 24 });
        e.Marks.UsuryCapped.Should().BeTrue("la mora del día está topada");
        e.NotApplied.Should().ContainSingle().Which.ReasonText.Should().Be("Sin saldo en la nómina");
        (e.InformativeRating, e.OfficialRating, e.CollectionStage).Should().Be((null, null, null));
        e.Fingerprint.Should().StartWith("sha256:");
        (await _c.D.G.EventosAsync()).Should().Contain(AuditEventTypes.LendingPayoffViewed);
    }

    [Fact]
    public async Task Al_dia_no_hay_mora_y_la_primera_cuota_esta_por_vencer()
    {
        var r = await _c.EstadoDeCuenta().Handle(new GetLoanStatementQuery(_c.Credito.PublicId, Hoy.AddDays(10)), default);

        r.Value.DaysPastDue.Should().Be(0);
        r.Value.Installments.Should().OnlyContain(i => i.Status == EstadosDeCuota.PorVencer);
        (await _c.Db.LoanTransactions.CountAsync(t => t.Kind == LoanTransactionKind.Accrual)).Should().Be(0, "consultar no escribe");
    }
}
