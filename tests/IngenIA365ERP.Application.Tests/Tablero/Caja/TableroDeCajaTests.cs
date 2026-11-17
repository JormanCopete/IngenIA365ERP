using FluentAssertions;
using IngenIA365ERP.Application.Common.Scope;
using IngenIA365ERP.Application.Tablero;
using IngenIA365ERP.Application.Tablero.Caja;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Operations;
using IngenIA365ERP.Domain.Entities.Treasury.Payments;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Enums.Treasury;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Tablero.Caja;

/// <summary>2026-11-17: el recaudo del tablero de inicio (alcance de Recaudo, sin anulados, «Solo lo mío» por cajero) y los de Tesorería.</summary>
public class TableroDeCajaTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 17);
    private const int Oficina = 1;
    private const int OtraOficina = 2;
    private const int Cajera = 7;

    private static IAlcanceDeRecaudo Alcance(params int[] oficinas)
    {
        var alcance = Substitute.For<IAlcanceDeRecaudo>();
        alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(oficinas.Length == 0
            ? AlcanceDeRecaudo.Todo
            : new AlcanceDeRecaudo(false, oficinas.ToHashSet(), new HashSet<int>(), new HashSet<int>(), new HashSet<int>()));
        return alcance;
    }

    private static Receipt Recibo(long id, DateOnly dia, decimal valor, int oficina = Oficina, int? cajero = Cajera, ReceiptState estado = ReceiptState.Valid) =>
        new() { Id = id, Number = $"R{id}", BranchId = oficina, CashierUserId = cajero, ValueDate = dia, TotalApplied = valor, Status = estado };

    [Fact]
    public async Task El_recaudo_del_dia_respeta_el_alcance_los_anulados_y_solo_lo_mio()
    {
        var db = TestDbContextFactory.Create();
        db.IssuedReceipts.AddRange(
            Recibo(1, Hoy, 100_000m),
            Recibo(2, Hoy, 50_000m, cajero: 8),
            Recibo(3, Hoy, 999_000m, estado: ReceiptState.Voided),
            Recibo(4, Hoy, 70_000m, oficina: OtraOficina),
            Recibo(5, Hoy.AddDays(-1), 30_000m));
        await db.SaveChangesAsync();

        var indicador = new RecaudoDelDia(db, Alcance(Oficina));
        var todo = (await indicador.CalcularAsync(new ContextoDeIndicador(false, null, null, Hoy), default)).Value;
        var mio = (await indicador.CalcularAsync(new ContextoDeIndicador(true, Cajera, null, Hoy), default)).Value;
        var sinUsuario = (await indicador.CalcularAsync(new ContextoDeIndicador(true, null, null, Hoy), default)).Value;

        todo.Valor.Should().Be(150_000m, "sin el anulado ni la otra oficina");
        todo.Anterior.Should().Be(30_000m);
        mio.Valor.Should().Be(100_000m);
        sinUsuario.Valor.Should().Be(0m);
    }

    [Fact]
    public async Task El_recaudo_por_medio_de_pago_agrupa_lo_recibido_hoy()
    {
        var db = TestDbContextFactory.Create();
        db.PaymentMeans.AddRange(new Domain.Entities.Core.Payments.PaymentMeans { Id = 1, Code = "EF", Name = "Efectivo" }, new Domain.Entities.Core.Payments.PaymentMeans { Id = 2, Code = "TJ", Name = "Tarjeta" });
        db.IssuedReceipts.AddRange(Recibo(1, Hoy, 80_000m), Recibo(2, Hoy, 20_000m));
        db.ReceiptMeans.AddRange(
            new ReceiptMeans { Id = 1, ReceiptId = 1, PaymentMeansId = 1, Amount = 60_000m },
            new ReceiptMeans { Id = 2, ReceiptId = 1, PaymentMeansId = 2, Amount = 20_000m },
            new ReceiptMeans { Id = 3, ReceiptId = 2, PaymentMeansId = 1, Amount = 20_000m },
            new ReceiptMeans { Id = 4, ReceiptId = 2, PaymentMeansId = 2, Amount = 5_000m, Status = ReceiptMeansState.WrittenOff });
        await db.SaveChangesAsync();

        var r = (await new RecaudoPorMedioDePago(db, Alcance()).CalcularAsync(new ContextoDeIndicador(false, null, null, Hoy), default)).Value;

        r.Serie.Should().Equal(new PuntoDeSerie("Efectivo", 80_000m), new PuntoDeSerie("Tarjeta", 20_000m));
        r.Valor.Should().Be(100_000m);
    }

    [Fact]
    public async Task Pagos_por_girar_suma_solo_lo_que_no_ha_salido_al_banco()
    {
        var db = TestDbContextFactory.Create();
        db.PaymentOrders.AddRange(
            new PaymentOrder { Id = 1, Number = "O1", NetAmount = 100m, Status = PaymentOrderState.Ready },
            new PaymentOrder { Id = 2, Number = "O2", NetAmount = 50m, Status = PaymentOrderState.Approved },
            new PaymentOrder { Id = 3, Number = "O3", NetAmount = 999m, Status = PaymentOrderState.Disbursed },
            new PaymentOrder { Id = 4, Number = "O4", NetAmount = 999m, Status = PaymentOrderState.Voided });
        await db.SaveChangesAsync();

        var r = (await new PagosPorGirar(db).CalcularAsync(new ContextoDeIndicador(false, null, null, Hoy), default)).Value;

        r.Valor.Should().Be(150m);
        r.Nota.Should().Be("2 órdenes");
    }

    [Fact]
    public async Task Las_cuentas_bancarias_por_aprobar_cuentan_solo_las_pendientes()
    {
        var db = TestDbContextFactory.Create();
        db.PersonBankAccounts.AddRange(
            new PersonBankAccount { Id = 1, Status = PersonBankAccountState.PendingApproval },
            new PersonBankAccount { Id = 2, Status = PersonBankAccountState.Active },
            new PersonBankAccount { Id = 3, Status = PersonBankAccountState.PendingApproval, IsDeleted = true });
        await db.SaveChangesAsync();

        (await new CuentasBancariasPorAprobar(db).ContarAsync(new ContextoDeIndicador(false, null, null, Hoy), default)).Should().Be(1);
    }
}
