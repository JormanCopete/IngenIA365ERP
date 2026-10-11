using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Lending.Ledger;
using IngenIA365ERP.Application.Tests.Lending.Accrual;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Ledger;

/// <summary>
/// Feature 013, US4, T0478 (data-model §5.3; T33): Σ libro a d = reconstrucción(d, ahora) por concepto y cuota para todo d ≤ <c>LastAccruedDate</c>.
/// Después del ciclo no hay diferencias; una fila agregada a mano aparece como diferencia con lo que dice el libro y lo que da la reconstrucción y
/// levanta alerta; se verifica completo, por créditos o por muestra; y no corrige nada.
/// </summary>
public class VerifyLendingLedgerIntegrityQueryHandlerTests : IAsyncLifetime
{
    private CausacionDePrueba _c = null!;
    private static readonly DateOnly Hoy = CausacionDePrueba.Hoy;

    public async Task InitializeAsync()
    {
        _c = await CausacionDePrueba.CrearAsync();
        await _c.CausarHastaAsync(Hoy.AddDays(10));
    }

    public Task DisposeAsync()
    {
        _c.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Despues_del_ciclo_el_libro_cuadra_con_la_reconstruccion()
    {
        var r = await _c.Verificar().Handle(new VerifyLendingLedgerIntegrityQuery(), default);

        r.Value.Checked.Should().Be(1);
        r.Value.UpTo.Should().Be(Hoy.AddDays(10));
        r.Value.Mismatches.Should().BeEmpty();
    }

    [Fact]
    public async Task Una_fila_de_mas_es_una_diferencia_con_libro_y_reconstruccion_levanta_alerta_y_no_se_corrige()
    {
        var dia = Hoy.AddDays(4);
        var real = await _c.Db.LoanTransactions.AsNoTracking().SingleAsync(t => t.Kind == LoanTransactionKind.Accrual && t.ValueDate == dia);
        _c.Db.LoanTransactions.Add(new LoanTransaction
        {
            LoanId = _c.Credito.Id, ProcessRunId = real.ProcessRunId, Kind = LoanTransactionKind.AccrualAdjustment, Concept = LoanConcept.CurrentInterest,
            InstallmentNumber = 1, Amount = 50m, ValueDate = dia, AccountingDate = dia,
        });
        await _c.Db.SaveChangesAsync();
        var filas = await _c.Db.LoanTransactions.CountAsync();

        var r = await _c.Verificar().Handle(new VerifyLendingLedgerIntegrityQuery([_c.Credito.PublicId]), default);

        r.Value.Mismatches.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            LoanPublicId = _c.Credito.PublicId, Date = dia, Concept = "CurrentInterest", Installment = (int?)1, Ledger = real.Amount + 50m, Reconstruction = real.Amount,
        });
        _c.D.O.L.Levantadas.Should().Contain(a => a.TypeCode == TiposDeAlerta.CreditoSinCausar && a.EntityPublicId == _c.Credito.PublicId);
        (await _c.Db.LoanTransactions.CountAsync()).Should().Be(filas, "verificar no corrige nada");
        (await _c.Verificar().Handle(new VerifyLendingLedgerIntegrityQuery(UpTo: Hoy.AddDays(3)), default)).Value.Mismatches
            .Should().BeEmpty("hasta el día 3 el libro cuadra");
    }

    [Fact]
    public async Task Verifica_por_muestra_y_por_creditos()
    {
        var otro = await _c.DesembolsarAsync(_c.D.O.Solicitud(solicitante: _c.D.O.Beto));

        (await _c.Verificar().Handle(new VerifyLendingLedgerIntegrityQuery(SampleSize: 1), default)).Value.Checked.Should().Be(1);
        (await _c.Verificar().Handle(new VerifyLendingLedgerIntegrityQuery(), default)).Value.Checked.Should().Be(2);
        (await _c.Verificar().Handle(new VerifyLendingLedgerIntegrityQuery([otro.PublicId]), default)).Value.Checked.Should().Be(1);
        (await _c.Verificar().Handle(new VerifyLendingLedgerIntegrityQuery([Guid.NewGuid()]), default)).Value.Checked.Should().Be(0);
    }
}
