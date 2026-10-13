using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Application.Tests.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Migration;

/// <summary>
/// Feature 013, US6, T0572 (FR-055, FR-143, T83; mensajes.md §15.4; data-model §21.5): <see cref="ConciliacionDeVentasDeLaCarga"/> lista sin consumir
/// las ventas a crédito acumuladas hasta el corte (<c>IMensajesEntrantes.PendientesAsync</c>) y las clasifica por factura y pago: migrada (la parte
/// <c>loans</c> trae la factura), cancelada antes del corte (la parte <c>sales</c>), no migrada con saldo, o fuera de Cartera por la marca de la versión
/// anterior del cuadre; cuenta los ajustes hasta y después del corte; una factura y un pago no dan dos filas; y no consume nada.
/// </summary>
public sealed class ConciliacionDeVentasDeLaCargaTests : IAsyncLifetime
{
    private static readonly DateOnly Corte = VentasACreditoDePrueba.Hoy.AddDays(-10);
    private VentasACreditoDePrueba _v = null!;
    private MigrationLoad _carga = null!;

    public async Task InitializeAsync()
    {
        _v = await VentasACreditoDePrueba.CrearAsync();
        _carga = new MigrationLoad { Number = 1, CutoffDate = Corte, Status = MigrationLoadStatus.Applied, CreatedByUserId = _v.Cajera.Id, CurrentCheckVersion = 2 };
        _v.Db.MigrationLoads.Add(_carga);
        _v.Db.SaveChanges();
    }

    public Task DisposeAsync()
    {
        _v.Dispose();
        return Task.CompletedTask;
    }

    private ConciliacionDeVentasDeLaCarga Conciliacion() => new(_v.Db, new MensajesEntrantes(_v.Db));

    private MigrationLoadPart Parte(string clave)
    {
        var p = new MigrationLoadPart { MigrationLoadId = _carga.Id, PartKey = clave, FileName = clave + ".xlsx", FileSha256 = "x", ImportedByUserId = _v.Cajera.Id, ImportedAt = _v.G.UtcNow };
        _v.Db.MigrationLoadParts.Add(p);
        _v.Db.SaveChanges();
        return p;
    }

    private async Task<Guid> VentaAsync(VentasACreditoDePrueba.Venta v) => await _v.EmitirAsync(v, VentasACreditoDePrueba.ContenidoV2(v));

    private async Task AjusteAsync(VentasACreditoDePrueba.Venta v, Guid mensaje, DateOnly fecha) =>
        await _v.EmitirAsync(v, new AjusteDeVentaACreditoV1
        {
            AdjustmentClass = "CreditNote", Amount = -10_000m, OriginalMessageId = mensaje, OriginalCreditPaymentPublicId = v.Pago,
            OriginalDocument = new DocumentRefV1 { PublicId = v.Documento, DocumentClass = DocumentClass.PosEquivalentDocument, Number = v.Numero },
            PaymentMeansCode = "CREDASOC", AccountsReceivableRecordedBy = "Contabilidad",
        }, relacionado: v.Documento, documento: Guid.NewGuid(), numero: "NC-" + Guid.NewGuid().ToString("N")[..6], fecha: fecha);

    [Fact]
    public async Task Clasifica_cada_venta_hasta_el_corte_por_factura_y_pago_sin_consumir()
    {
        var migrada = _v.NuevaVenta(numero: "PV01-MIG", fecha: Corte.AddDays(-20));
        var cancelada = _v.NuevaVenta(numero: "PV01-CAN", fecha: Corte.AddDays(-15));
        var sinMigrar = _v.NuevaVenta(numero: "PV01-SIN", fecha: Corte.AddDays(-5));
        var marcada = _v.NuevaVenta(numero: "PV01-D29", fecha: Corte.AddDays(-3));
        var posterior = _v.NuevaVenta(numero: "PV01-POS", fecha: Corte.AddDays(2));
        var loans = Parte("loans");
        var sales = Parte("sales");
        _v.Db.MigrationLoanRows.Add(new MigrationLoanRow { MigrationLoadPartId = loans.Id, RowNumber = 1, SourceLoanNumber = "S-1", PersonId = _v.Ana.Id,
            CreditLineCode = "CONLIB", InvoiceNumber = "PV01-MIG", DisbursementDate = migrada.Fecha, OriginalAmount = 600_000m });
        _v.Db.MigrationSaleRows.Add(new MigrationSaleRow { MigrationLoadPartId = sales.Id, RowNumber = 1, InvoiceNumber = "PV01-CAN", PersonId = _v.Ana.Id,
            SaleDate = cancelada.Fecha, OriginalAmount = 600_000m, CancellationReason = "Pagada antes del corte" });
        _v.Db.MigrationSaleReconciliations.Add(new MigrationSaleReconciliation
        {
            MigrationLoadId = _carga.Id, CheckVersion = 1, MessagePublicId = Guid.NewGuid(), SaleDocumentPublicId = marcada.Documento, InvoiceNumber = "PV01-D29",
            CreditPaymentPublicId = marcada.Pago, SaleDate = marcada.Fecha, Amount = marcada.Monto, Outcome = SaleReconciliationOutcome.OutsideLendingD29,
            MarkedFromList = true, MarkReason = "Venta de servicios sin Cartera (D29)", MarkedByUserId = _v.Cajera.Id, MarkedAt = _v.G.UtcNow,
        });
        _v.Db.SaveChanges();
        var mensajeMigrada = await VentaAsync(migrada);
        await VentaAsync(cancelada);
        await VentaAsync(sinMigrar);
        await VentaAsync(marcada);
        await VentaAsync(posterior);
        await AjusteAsync(migrada, mensajeMigrada, Corte.AddDays(-1));
        await AjusteAsync(migrada, mensajeMigrada, Corte.AddDays(3));

        var r = await Conciliacion().ConciliarAsync(_carga, 2, default);
        await _v.Db.SaveChangesAsync();

        r.IsSuccess.Should().BeTrue();
        (r.Value.Migradas, r.Value.CanceladasAntesDelCorte, r.Value.NoMigradasConSaldo, r.Value.FueraDeCartera).Should().Be((1, 1, 1, 1));
        var filas = await _v.Db.MigrationSaleReconciliations.AsNoTracking().Where(f => f.CheckVersion == 2).ToListAsync();
        filas.Should().HaveCount(4, "la venta posterior al corte no se concilia");
        var deLaMigrada = filas.Single(f => f.InvoiceNumber == "PV01-MIG");
        (deLaMigrada.Outcome, deLaMigrada.MigrationLoanRowId is not null, deLaMigrada.AdjustmentsUpToCutoff, deLaMigrada.AdjustmentsAfterCutoff)
            .Should().Be((SaleReconciliationOutcome.Migrated, true, 1, 1));
        filas.Single(f => f.InvoiceNumber == "PV01-CAN").MigrationSaleRowId.Should().NotBeNull();
        var deLaMarcada = filas.Single(f => f.InvoiceNumber == "PV01-D29");
        (deLaMarcada.Outcome, deLaMarcada.MarkedFromList, deLaMarcada.MarkReason).Should()
            .Be((SaleReconciliationOutcome.OutsideLendingD29, true, "Venta de servicios sin Cartera (D29)"), "la marca se copia a la versión nueva");
        (await _v.Db.LendingInboundMessages.CountAsync()).Should().Be(0, "conciliar no consume nada");
    }

    [Fact]
    public async Task Una_factura_y_un_pago_no_dan_dos_filas()
    {
        var venta = _v.NuevaVenta(numero: "PV01-DOS", fecha: Corte.AddDays(-2));
        await VentaAsync(venta);
        // El mismo pago llegó también desde otro documento (un reenvío): una sola fila (FR-055).
        await _v.EmitirAsync(venta, VentasACreditoDePrueba.ContenidoV2(venta), documento: Guid.NewGuid());

        var r = await Conciliacion().ConciliarAsync(_carga, 2, default);

        r.Value.Filas.Should().ContainSingle();
    }
}
