using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Inventory;
using IngenIA365ERP.Application.Receipts.Compliance;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Receipts.Compliance;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Compliance;

/// <summary>
/// Feature 015, R1, T0386 (FR-204; E-03; TR28; FR-099): el registro único de transacciones en efectivo.
/// <list type="bullet">
/// <item>las señales y el reporte suman el efectivo del punto de venta por <see cref="IEfectivoDelPuntoDeVenta"/> sin copiar sus filas a <c>RCD_CashTransactions</c>;</item>
/// <item>los egresos de caja en efectivo cuentan; los anulados (y la fila de su anulación) no.</item>
/// </list>
/// Prueba escrita, sin correr.
/// </summary>
public sealed class RegistroUnicoDeEfectivoTests
{
    private readonly EscenarioDeRecaudo _e = new();
    private readonly IEfectivoDelPuntoDeVenta _pos = Substitute.For<IEfectivoDelPuntoDeVenta>();
    private readonly Person _ana;

    public RegistroUnicoDeEfectivoTests()
    {
        _ana = new Person { FirstName = "Ana", LastName = "Asociada", TaxId = "1001" };
        _e.Db.People.Add(_ana);
        _e.Db.SaveChanges();
        _pos.PorPeriodoAsync(Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(
            [new EfectivoDelPuntoDeVentaDto(Guid.NewGuid(), "FV-9", new DateOnly(2027, 1, 20), _ana.PublicId, 3_000_000m, 0m, null)]);
    }

    private void Fila(CashTransactionDocumentKind clase, decimal efectivo, Guid? documento = null, CashTransactionDirection sentido = CashTransactionDirection.In) =>
        _e.Db.ReceiptsCashTransactions.Add(new CashTransaction
        {
            DocumentPublicId = documento ?? Guid.NewGuid(), DocumentNumber = "DOC", DocumentKind = clase, Direction = sentido, PersonId = _ana.Id, PayerPersonId = _ana.Id,
            CashAmount = efectivo, Channel = ReceiptChannel.CashDesk, BranchId = _e.OficinaA.Id, OccurredOn = new DateOnly(2027, 1, 10), Month = 202701,
        });

    [Fact]
    public async Task El_reporte_suma_el_punto_de_venta_sin_copiar_sus_filas_y_cuenta_los_egresos()
    {
        Fila(CashTransactionDocumentKind.Receipt, 2_000_000m);
        Fila(CashTransactionDocumentKind.CashDisbursement, 1_000_000m, sentido: CashTransactionDirection.Out);
        await _e.Db.SaveChangesAsync();
        var filasAntes = await _e.Db.ReceiptsCashTransactions.CountAsync();

        var t = await new PreparacionDelReporteDeTransacciones(_e.Db, _e.Parametros, _pos).TransaccionesAsync(new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 31), default);

        t.Sum(x => x.Valor).Should().Be(6_000_000m, "recibo 2.000.000 + egreso 1.000.000 + punto de venta 3.000.000");
        t.Should().OnlyContain(x => x.Medio == Domain.Receipts.Cumplimiento.MedioDeTransaccionReportable.Efectivo);
        t.Single(x => x.Valor == 1_000_000m).Sentido.Should().Be(CashTransactionDirection.Out, "el egreso es un retiro (antes se perdía el sentido)");
        t.Should().Contain(x => x.Origen == "PointOfSale" && x.Documento == "FV-9");
        (await _e.Db.ReceiptsCashTransactions.CountAsync()).Should().Be(filasAntes, "el punto de venta no se copia");
    }

    [Fact]
    public async Task Las_senales_tambien_ven_el_efectivo_del_punto_de_venta()
    {
        var registro = new RegistroDeEfectivo(_e.Db, _e.Lector, _e.Alertas, _e.Reloj, _pos);

        var del = await registro.DelPuntoDeVentaAsync([_ana.Id], new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 31), default);

        del.Should().ContainSingle(x => x.PersonId == _ana.Id && x.Efectivo == 3_000_000m);
        (await new RegistroDeEfectivo(_e.Db, _e.Lector, _e.Alertas, _e.Reloj).DelPuntoDeVentaAsync([_ana.Id], new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 31), default))
            .Should().BeEmpty("sin puerto, sólo lo de Recaudo");
    }

    [Fact]
    public async Task Un_egreso_anulado_no_cuenta_en_el_reporte()
    {
        var egreso = new Domain.Entities.Receipts.Operations.CashDisbursement
        {
            Number = "EG-1", Kind = CashDisbursementKind.MiscExpense, Amount = 1_000_000m, Status = CashDisbursementStatus.Voided, IdempotencyKey = "k", VerificationCode = "v",
        };
        _e.Db.CashDisbursements.Add(egreso);
        Fila(CashTransactionDocumentKind.CashDisbursement, 1_000_000m, egreso.PublicId, CashTransactionDirection.Out);
        Fila(CashTransactionDocumentKind.Voiding, -1_000_000m, sentido: CashTransactionDirection.Out);
        await _e.Db.SaveChangesAsync();

        var t = await new PreparacionDelReporteDeTransacciones(_e.Db, _e.Parametros).TransaccionesAsync(new DateOnly(2027, 1, 1), new DateOnly(2027, 1, 31), default);

        t.Should().BeEmpty();
    }
}
