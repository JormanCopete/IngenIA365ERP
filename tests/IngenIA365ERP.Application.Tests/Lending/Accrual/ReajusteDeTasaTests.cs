using FluentAssertions;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Rates;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Accrual;

/// <summary>
/// Feature 013, US4, T0473 (FR-067, C47, D10; paso (2) del ciclo): en la fecha de reajuste el ciclo toma el <b>último índice publicado</b>, le suma
/// el margen en la expresión del índice, lo pasa a EA (sellada a seis decimales, T35) y aplica piso y techo (la usura la topa la causación del día); registra la operación
/// <c>RateReset</c> numerada <b>sin filas de libro</b>, con su versión de condiciones (<c>Cause = RateReset</c>, próximo reajuste) y la versión del
/// plan regenerada; y un índice viejo alerta <c>Cartera.IndiceDesactualizado</c>. El crédito de Ana pasa a tasa variable con una versión de
/// condiciones sembrada (IBR 3M + 8 puntos, piso 10 %, techo 24 %, trimestral) con el primer reajuste el 2026-12-10.
/// </summary>
public class ReajusteDeTasaTests : IAsyncLifetime
{
    private CausacionDePrueba _c = null!;
    private static readonly DateOnly Hoy = CausacionDePrueba.Hoy;
    private static readonly DateOnly Reajuste = Hoy.AddDays(9);

    public async Task InitializeAsync()
    {
        _c = await CausacionDePrueba.CrearAsync();
        var v1 = await _c.Db.LoanTermsVersions.AsNoTracking().SingleAsync(t => t.LoanId == _c.Credito.Id);
        _c.Db.LoanTermsVersions.Add(Variable(v1));
        await _c.Db.SaveChangesAsync();
        _c.Db.ChangeTracker.Clear();
    }

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task En_la_fecha_de_reajuste_registra_la_operacion_sin_libro_la_version_y_el_plan_con_el_ultimo_indice_publicado()
    {
        await _c.D.O.L.IbrAsync(0.09m, Reajuste.AddDays(-1));

        await _c.CausarHastaAsync(Reajuste.AddDays(2));

        var op = await _c.Db.LoanOperations.SingleAsync(o => o.Kind == LoanOperationKind.RateReset);
        op.Number.Should().StartWith(ReajusteDeTasa.PrefijoDeReajuste);
        (op.ValueDate, op.ActorKind, op.Amount).Should().Be((Reajuste, Domain.Enums.Integration.ActorKind.Process, 0m));
        (await _c.Db.LoanTransactions.AnyAsync(t => t.OperationId == op.Id)).Should().BeFalse("el reajuste no es una transacción de dinero (C47)");

        var esperada = decimal.Round(MotorDeCartera.TasaVariable(new IndiceDeReferencia(0.09m, 12), 0.08m, 0.10m, 0.24m), 6, MidpointRounding.AwayFromZero);
        var condiciones = await _c.Db.LoanTermsVersions.SingleAsync(t => t.OperationId == op.Id);
        (condiciones.Cause, condiciones.VersionNumber, condiciones.AnnualRate, condiciones.NextResetDate)
            .Should().Be((TermsVersionCause.RateReset, 3, (decimal?)esperada, (DateOnly?)Reajuste.AddMonths(3)));
        var plan = await _c.Db.RepaymentScheduleVersions.SingleAsync(v => v.OperationId == op.Id);
        plan.VersionNumber.Should().Be(2);
        plan.PrincipalBase.Should().Be(3_000_000m);
        (await _c.Db.RepaymentInstallments.CountAsync(i => i.ScheduleVersionId == plan.Id)).Should().Be(24);

        // La causación del día siguiente al reajuste ya usa la tasa nueva: el libro sigue cuadrando con la reconstrucción.
        var cargado = await _c.Cargador().CargarAsync(_c.Credito.Id, null, Reajuste.AddDays(2), default);
        var libro = (await _c.Cargador().LibroAsync([_c.Credito.Id], Reajuste.AddDays(2), default))[_c.Credito.Id];
        MotorDeCartera.Verificar(cargado!.Entrada!, Reajuste.AddDays(2), libro).Should().BeEmpty();
        MotorDeCartera.Reconstruir(cargado.Entrada!, Reajuste.AddDays(2)).TasaDelDia!.CorrienteEA.Should().Be(esperada);
    }

    [Fact]
    public async Task Un_indice_viejo_alerta_IndiceDesactualizado_y_reajusta_igual()
    {
        await _c.D.O.L.IbrAsync(0.09m, Reajuste.AddDays(-20));

        await _c.CausarHastaAsync(Reajuste);

        _c.D.O.L.Levantadas.Should().Contain(a => a.TypeCode == AlertasDeValoresNacionales.IndiceDesactualizado);
        (await _c.Db.LoanOperations.SingleAsync(o => o.Kind == LoanOperationKind.RateReset)).Payload.Should().Contain("\"stale\":true");
    }

    [Fact]
    public async Task Antes_de_la_fecha_de_reajuste_no_reajusta_y_sin_indice_el_credito_queda_en_los_errores()
    {
        await _c.CausarHastaAsync(Reajuste.AddDays(-1));
        (await _c.Db.LoanOperations.AnyAsync(o => o.Kind == LoanOperationKind.RateReset)).Should().BeFalse();

        var corrida = await _c.CausarHastaAsync(Reajuste);

        (await _c.Db.LendingProcessRunErrors.SingleAsync(e => e.ProcessRunId == corrida.Id)).Should().BeEquivalentTo(new
        {
            LoanId = (int?)_c.Credito.Id, Step = "Reajuste", ErrorCode = "NationalValues.Missing",
        });
        (await _c.ControlAsync()).LastAccruedDate.Should().Be(Reajuste.AddDays(-1), "sin índice no se causa el día del reajuste");
    }

    private LoanTermsVersion Variable(LoanTermsVersion v) => new()
    {
        LoanId = v.LoanId, VersionNumber = 2, OperationId = v.OperationId, EffectiveValueDate = v.EffectiveValueDate, RegisteredAt = v.RegisteredAt.AddSeconds(1),
        Cause = TermsVersionCause.RateChange, RulesVersion = v.RulesVersion, CreditLineVersionId = v.CreditLineVersionId, ModalityCode = v.ModalityCode,
        SubsegmentCode = v.SubsegmentCode, RateKind = RateKind.Variable, AnnualRate = v.AnnualRate, IndexCode = "IBR", IndexKey = "3M", Margin = 0.08m,
        RateFloor = 0.10m, RateCeiling = 0.24m, ResetPeriodicity = RepaymentPeriodicity.Quarterly, NextResetDate = Reajuste,
        DefaultRateKind = v.DefaultRateKind, DefaultRateFactor = v.DefaultRateFactor, DayCount = v.DayCount, RateEquivalence = v.RateEquivalence,
        AmortizationSystem = v.AmortizationSystem, Periodicity = v.Periodicity, Installments = v.Installments, TermMonths = v.TermMonths,
        FirstDueDate = v.FirstDueDate, RoundingUnit = v.RoundingUnit, RoundingMode = v.RoundingMode, RoundingMoment = v.RoundingMoment,
        ConceptPriority = v.ConceptPriority, Insurances = v.Insurances, InputsSha256 = v.InputsSha256,
    };
}
