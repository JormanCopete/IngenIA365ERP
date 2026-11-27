using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Application.Lending.Payoffs;
using IngenIA365ERP.Application.Lending.Reports;
using IngenIA365ERP.Application.Lending.Reports.Vistas;
using IngenIA365ERP.Application.Tests.Lending.Disbursements;
using IngenIA365ERP.Application.Tests.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Entities.Lending.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Payoffs;

/// <summary>
/// Feature 028 (T054, T056; HU8, FR-029): el detalle del crédito dice «Recogido por el crédito … el …» en el recogido y «Recogió: …» en el nuevo, y el libro
/// del recogido dice «Recogido con el crédito …» en el pago de la recogida (también en el informe de movimientos). Lee las filas
/// <c>LND_DisbursementPayoffs</c> y, para un desembolso de antes de la 028 sin filas, los pagos <c>CAR</c> con la clave de la recogida; un pago con fila no
/// se cuenta dos veces y un pago <c>CAR</c> con otra gramática (el cruce del retiro) no es una recogida. El escenario es <see cref="DesembolsoDePrueba"/>
/// con el contrato doble (no deja la operación de pago: la pone la prueba). (nuevo)
/// </summary>
public sealed class VinculosDeRecogidaTests : IAsyncLifetime
{
    private DesembolsoDePrueba _d = null!;
    private Loan _viejo = null!;

    public async Task InitializeAsync()
    {
        _d = await DesembolsoDePrueba.CrearAsync();
        _viejo = _d.O.Credito(_d.O.Ana, 1_000_000m, "CR-VIEJO");
    }

    public Task DisposeAsync()
    {
        _d.Dispose();
        return Task.CompletedTask;
    }

    // ------------------------------------------------------------------------------------------------------------ piezas --

    private async Task<DisbursementResultDto> DesembolsadoAsync()
    {
        var s = await _d.AprobadaAsync(_d.O.Solicitud() with { Payoffs = [new PayoffInput(PayoffProductKind.Loan, _viejo.PublicId, PayoffMode.Total)] });
        return await _d.DesembolsadoAsync(_d.Entrada(s));
    }

    private async Task<LoanDto> CreditoAsync(Guid credito)
    {
        var r = await new GetLoanQueryHandler(_d.Db, _d.G.Alcance, _d.G.Auditoria).Handle(new GetLoanQuery(credito), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    /// <summary>Un pago de Cartera con la clave <paramref name="clave"/> y su fila de capital en el libro de <paramref name="credito"/>.</summary>
    private LoanOperation PagoCar(Loan credito, string clave, decimal monto, PaymentModality modalidad = PaymentModality.FullPayoff, string? recibo = null)
    {
        var operacion = new LoanOperation
        {
            LoanId = credito.Id, Kind = LoanOperationKind.Payment, Number = $"PAG-{Guid.NewGuid():N}"[..12], Sequence = 1, ValueDate = DesembolsoDePrueba.Hoy,
            AccountingDate = DesembolsoDePrueba.Hoy, RegisteredAt = _d.G.UtcNow, Amount = monto, CollectorCode = ClavesDeRecaudador.Cartera,
            IdempotencyKey = clave, PaymentModality = modalidad, ReceiptNumber = recibo, Payload = "{}", InputsSha256 = "x", RulesVersion = "LND-2026.1",
        };
        _d.Db.LoanOperations.Add(operacion);
        _d.Db.SaveChanges();
        _d.Db.LoanTransactions.Add(new LoanTransaction
        {
            LoanId = credito.Id, Kind = LoanTransactionKind.PaymentAllocation, Concept = LoanConcept.Principal, Amount = -monto, ValueDate = DesembolsoDePrueba.Hoy,
            AccountingDate = DesembolsoDePrueba.Hoy, RegisteredAt = operacion.RegisteredAt, OperationId = operacion.Id,
        });
        _d.Db.SaveChanges();
        _d.Db.ChangeTracker.Clear();
        return operacion;
    }

    // -------------------------------------------------------------------------------------------------------- el detalle --

    [Fact]
    public async Task El_recogido_dice_quien_lo_recogio_y_el_nuevo_lo_que_recogio()
    {
        var r = await DesembolsadoAsync();

        var recogido = await CreditoAsync(_viejo.PublicId);
        var nuevo = await CreditoAsync(r.LoanPublicId);

        recogido.PaidOffBy.Should().ContainSingle().Which.Should()
            .Be(new LoanPayoffLinkDto("Loan", r.LoanPublicId, r.LoanNumber, DesembolsoDePrueba.Hoy, "Total", 1_000_000m, false));
        recogido.PaidOff.Should().BeEmpty();
        nuevo.PaidOff.Should().ContainSingle().Which.Should()
            .Be(new LoanPayoffLinkDto("Loan", _viejo.PublicId, "CR-VIEJO", DesembolsoDePrueba.Hoy, "Total", 1_000_000m, false));
        nuevo.PaidOffBy.Should().BeEmpty();

        var vinculo = (await VinculosDeRecogida.DeLosRecogidosAsync(_d.Db, [_viejo.Id], default)).Single();
        TextosDeVinculosDeRecogida.RecogidoPor(vinculo).Should().Be($"Recogido por el crédito {r.LoanNumber} el {DesembolsoDePrueba.Hoy:dd/MM/yyyy}");
        TextosDeVinculosDeRecogida.RecogidoCon(vinculo).Should().Be($"Recogido con el crédito {r.LoanNumber}");

        // Al anular el crédito nuevo antes del giro la recogida queda reversada y el vínculo lo dice.
        var fila = await _d.Db.DisbursementPayoffs.SingleAsync();
        fila.Reversar(_d.G.UtcNow, fila.IdempotencyKey + ":reverso");
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();
        (await CreditoAsync(_viejo.PublicId)).PaidOffBy.Single().Reversed.Should().BeTrue();
    }

    [Fact]
    public async Task El_pago_de_la_recogida_dice_con_que_credito_en_el_libro_y_en_el_informe_de_movimientos()
    {
        var r = await DesembolsadoAsync();
        var fila = await _d.Db.DisbursementPayoffs.AsNoTracking().SingleAsync();
        var pago = PagoCar(_viejo, fila.IdempotencyKey, 1_000_000m, recibo: r.OperationNumber);

        var libro = await new ListLoanTransactionsQueryHandler(_d.Db, _d.G.Alcance, _d.G.Auditoria)
            .Handle(new ListLoanTransactionsQuery(_viejo.PublicId), default);

        libro.Value.Items.Single(t => t.OperationPublicId == pago.PublicId).Description.Should().Be($"Recogido con el crédito {r.LoanNumber}");
        libro.Value.Items.Where(t => t.OperationPublicId != pago.PublicId).Should().OnlyContain(t => t.Description == null);
        (await CreditoAsync(_viejo.PublicId)).PaidOffBy.Should().ContainSingle("el pago y su fila llevan la misma clave: se cuenta una vez");

        var movimientos = await new TransactionsReportQueryHandler(_d.Db, _d.G.Alcance, _d.G.Reloj).Handle(new TransactionsReportQuery(
            new FiltrosDeInformeDeCartera { From = DesembolsoDePrueba.Hoy.AddDays(-1), To = DesembolsoDePrueba.Hoy.AddDays(1) }, _viejo.PublicId), default);
        var recibo = TransactionsReportQueryHandler.Columnas.Select(c => c.Nombre).ToList().IndexOf("Recibo");
        movimientos.Value.Filas.Select(f => f.Valores[recibo]).Should().Contain($"{r.OperationNumber} · Recogido con el crédito {r.LoanNumber}");
    }

    // ------------------------------------------------------------------------------------- desembolsos de antes de la 028 --

    [Fact]
    public async Task Un_desembolso_de_antes_sin_filas_se_lee_de_los_pagos_CAR_y_otra_gramatica_no_es_recogida()
    {
        var r = await DesembolsadoAsync();
        var desembolso = await _d.Db.LoanOperations.AsNoTracking().SingleAsync(o => o.PublicId == r.OperationPublicId);
        var antiguo = _d.O.Credito(_d.O.Ana, 400_000m, "CR-ANTIGUO");
        var otro = _d.O.Credito(_d.O.Ana, 300_000m, "CR-RETIRO");
        // Un abono de antes de la 028 (sin fila) y reversado, y un pago CAR del cruce del retiro, con otra gramática de clave.
        var abono = PagoCar(antiguo, RecogidaEnElDesembolso.Clave(desembolso.PublicId, antiguo.PublicId), 150_000m, PaymentModality.ExtraordinaryPrincipal);
        var operacion = await _d.Db.LoanOperations.SingleAsync(o => o.Id == abono.Id);
        operacion.ReversedByOperationId = desembolso.Id;
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();
        PagoCar(otro, $"{ClavesDeRecaudador.Cartera}:RETIRO:{Guid.NewGuid():N}", 300_000m);

        var delAntiguo = (await CreditoAsync(antiguo.PublicId)).PaidOffBy.Should().ContainSingle().Subject;
        delAntiguo.Should().Be(new LoanPayoffLinkDto("Loan", r.LoanPublicId, r.LoanNumber, DesembolsoDePrueba.Hoy, "Partial", 150_000m, true));
        (await CreditoAsync(otro.PublicId)).PaidOffBy.Should().BeEmpty("el cruce del retiro no es la recogida de un desembolso");

        var nuevo = await CreditoAsync(r.LoanPublicId);
        nuevo.PaidOff.Select(p => (p.Number, p.Mode, p.Applied, p.Reversed)).Should().BeEquivalentTo(new[]
        {
            ("CR-VIEJO", "Total", 1_000_000m, false),
            ("CR-ANTIGUO", "Partial", 150_000m, true),
        });
        (await VinculosDeRecogida.DeLosPagosAsync(_d.Db, [abono.Id], default)).Should().ContainKey(abono.Id);
    }
}

/// <summary>
/// Feature 028 (T054): con el contrato de obligaciones real la recogida deja su operación de pago, y el libro del crédito recogido la nombra «Recogido con el
/// crédito …» y su detalle «Recogido por el crédito … el …». El escenario es <see cref="PagosDePrueba"/> (Ana, 3.000.000). (nuevo)
/// </summary>
public sealed class VinculosDeRecogidaConElContratoRealTests
{
    [Fact]
    public async Task El_pago_real_de_la_recogida_queda_nombrado_en_el_libro_y_en_el_detalle()
    {
        using var p = await PagosDePrueba.CrearAsync();
        var s = await p.D.AprobadaAsync(p.D.O.Solicitud(monto: 5_000_000m) with
        {
            Payoffs = [new PayoffInput(PayoffProductKind.Loan, p.Credito.PublicId, PayoffMode.Total)],
        });
        var r = await p.D.DesembolsadoAsync(p.D.Entrada(s), p.Contrato());
        var pago = r.Payoffs.Single().ReceiptOperationPublicId;

        var libro = await new ListLoanTransactionsQueryHandler(p.Db, p.G.Alcance, p.G.Auditoria)
            .Handle(new ListLoanTransactionsQuery(p.Credito.PublicId, PageSize: 200), default);
        var filasDelPago = libro.Value.Items.Where(t => t.OperationPublicId == pago).ToList();
        filasDelPago.Should().NotBeEmpty().And.OnlyContain(t => t.Description == $"Recogido con el crédito {r.LoanNumber}");

        var detalle = await new GetLoanQueryHandler(p.Db, p.G.Alcance, p.G.Auditoria).Handle(new GetLoanQuery(p.Credito.PublicId), default);
        var por = detalle.Value.PaidOffBy.Should().ContainSingle().Subject;
        (por.LoanPublicId, por.Number, por.Mode, por.Applied, por.Reversed).Should()
            .Be(((Guid?)r.LoanPublicId, r.LoanNumber, "Total", r.Payoffs.Single().Applied, false));
    }
}
