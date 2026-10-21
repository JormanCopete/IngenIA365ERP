using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Segregation;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Disbursements;

/// <summary>
/// Feature 013, US3, T0421 (data-model §7.4, §7.11; interfaces.md §4.3; api.md §8.4, §9.2; C11, C12): anular una orden sólo emitida, sin anulación
/// pedida y fuera de un archivo vivo, con <c>AnulacionDeOrdenDeGiro</c> (clave <c>Confirmation:{orden:N}</c>, <c>related</c> = la orden) y la
/// intervención <c>AnularOrdenDeGiro</c>, sin mover el estado; el nuevo giro sólo con el crédito pendiente de giro, otro tramo y otra orden enlazada;
/// anular el crédito pendiente reversa por el contrato cada pago de recogida.
/// </summary>
public sealed class OrdenesYAnulacionTests : IAsyncLifetime
{
    private DesembolsoDePrueba _d = null!;

    public async Task InitializeAsync() => _d = await DesembolsoDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _d.Dispose();
        return Task.CompletedTask;
    }

    private async Task<DisbursementResultDto> DesembolsadoAsync(CreditApplicationDto? s = null, DisbursementInput? entrada = null)
    {
        s ??= await _d.AprobadaAsync();
        return await _d.DesembolsadoAsync(entrada ?? _d.Entrada(s));
    }

    private async Task RechazadaAsync(DisbursementResultDto d)
    {
        var emitido = await _d.EmitirResultado().Handle(new EmitTestDisbursementResultCommand(d.Order!.PublicId, "Rejected", "TRF-9", DesembolsoDePrueba.Hoy)
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        _d.Db.ChangeTracker.Clear();
        (await _d.Consumir().Handle(new ConsumeTreasuryResultCommand([emitido.Value.MessagePublicId]), default)).IsSuccess.Should().BeTrue();
        _d.Db.ChangeTracker.Clear();
    }

    private async Task<IngenIA365ERP.Application.Common.Models.Result<DisbursementOrderDto>> AnularOrdenAsync(Guid orden)
    {
        var r = await _d.AnularOrden().Handle(new VoidDisbursementOrderCommand(orden, "El deudor pidió otra cuenta") { OperationKey = Guid.NewGuid() }, default);
        _d.Db.ChangeTracker.Clear();
        return r;
    }

    // --------------------------------------------------------------------------------------------------- anular la orden --

    [Fact]
    public async Task Anular_una_orden_emitida_emite_la_anulacion_y_la_orden_sigue_emitida()
    {
        var d = await DesembolsadoAsync();

        var r = await AnularOrdenAsync(d.Order!.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : string.Empty);
        r.Value.Status.Should().Be("Issued");
        r.Value.VoidRequested.Should().BeTrue();
        var m = await _d.Db.IntegrationMessages.AsNoTracking().SingleAsync(x => x.Type == AnulacionDeOrdenDeGiroV1.Type);
        (m.OriginPublicId, m.OriginEventKey, m.RelatedPublicId).Should()
            .Be((d.Order.PublicId, ClavesDeEvento.ConfirmacionPor(d.Order.PublicId), (Guid?)d.Order.PublicId));
        var giro = await _d.Db.IntegrationMessages.AsNoTracking().SingleAsync(x => x.Type == OrdenDeGiroEmitidaV1.Type);
        (await _d.Db.IntegrationMessageDependencies.AsNoTracking().AnyAsync(x => x.MessageId == m.Id && x.DependsOnMessageId == giro.Id))
            .Should().BeTrue("espera a su OrdenDeGiroEmitida por la arista de su origen (C12)");
        (await _d.Db.CaseParticipations.Select(c => c.Action).ToListAsync()).Should().Contain(AccionesDeSegregacion.AnularOrdenDeGiro);
    }

    [Fact]
    public async Task Una_orden_con_anulacion_pedida_o_ya_resuelta_no_se_anula()
    {
        var d = await DesembolsadoAsync();
        await AnularOrdenAsync(d.Order!.PublicId);

        var otra = await AnularOrdenAsync(d.Order.PublicId);

        otra.Error.Code.Should().Be(ErroresDeDesembolso.CodigoOrdenNoAnulable);
        DesembolsoDePrueba.Datos(otra.Error).GetProperty("status").GetString().Should().Be("Issued");
    }

    [Fact]
    public async Task Una_orden_en_un_archivo_vivo_no_se_anula_hasta_anular_el_archivo()
    {
        var d = await DesembolsadoAsync();
        var archivo = new DisbursementFile
        {
            Number = "AD-000001", BankId = _d.Banco.Id, FormatId = 1, FormatCode = "CSV", PaymentDate = DesembolsoDePrueba.Hoy, DailySequence = 1,
            SourceAccountNumber = "1", LineCount = 1, TotalAmount = 3_000_000m, FileName = "a.csv", FileSha256 = new string('a', 64),
            FileAttachmentPublicId = Guid.NewGuid(), GeneratedAt = _d.G.UtcNow, GeneratedByUserId = _d.Cajera.Id,
        };
        _d.Db.DisbursementFiles.Add(archivo);
        await _d.Db.SaveChangesAsync();
        var orden = await _d.Db.DisbursementOrders.SingleAsync();
        orden.CurrentDisbursementFileId = archivo.Id;
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();

        var r = await AnularOrdenAsync(d.Order!.PublicId);

        r.Error.Code.Should().Be(ErroresDeDesembolso.CodigoOrdenEnArchivo);
        DesembolsoDePrueba.Datos(r.Error).GetProperty("filePublicId").GetGuid().Should().Be(archivo.PublicId);
    }

    // ----------------------------------------------------------------------------------------------------- nuevo giro --

    [Fact]
    public async Task El_nuevo_giro_exige_el_credito_pendiente_de_giro()
    {
        var d = await DesembolsadoAsync();

        var r = await _d.GirarDeNuevo().Handle(new ReissueDisbursementOrderCommand(d.Order!.PublicId, DisbursementForm.Check, null, "otra vez")
        {
            OperationKey = Guid.NewGuid(),
        }, default);

        r.Error.Code.Should().Be(ErroresDeDesembolso.CodigoOrdenNoReemitible);
    }

    [Fact]
    public async Task El_nuevo_giro_crea_otro_tramo_y_otra_orden_enlazada_y_el_credito_vuelve_a_activo()
    {
        var d = await DesembolsadoAsync();
        await RechazadaAsync(d);

        var r = await _d.GirarDeNuevo().Handle(new ReissueDisbursementOrderCommand(d.Order!.PublicId, DisbursementForm.Transfer,
            new BeneficiaryInput(_d.O.Ana.PublicId, _d.Banco.PublicId, BeneficiaryInput.Corriente, "777"), "Cuenta corregida") { OperationKey = Guid.NewGuid() }, default);
        _d.Db.ChangeTracker.Clear();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.PreviousOrderNumber.Should().Be(d.Order.Number);
        var anterior = await _d.Db.DisbursementOrders.SingleAsync(o => o.PublicId == d.Order.PublicId);
        var nueva = await _d.Db.DisbursementOrders.SingleAsync(o => o.PublicId == r.Value.PublicId);
        nueva.PreviousOrderId.Should().Be(anterior.Id);
        var tramos = await _d.Db.LoanOperations.Where(o => o.Kind == LoanOperationKind.Disbursement).OrderBy(o => o.Id).ToListAsync();
        tramos.Should().HaveCount(2);
        (tramos[1].TrancheNumber, tramos[1].RelatedOperationId).Should().Be((tramos[0].TrancheNumber, (long?)tramos[0].Id));
        (await _d.Db.Loans.SingleAsync()).Status.Should().Be(LoanStatus.Active);
        var giro = await _d.Db.IntegrationMessages.AsNoTracking().Where(m => m.Type == OrdenDeGiroEmitidaV1.Type).OrderByDescending(m => m.Id).FirstAsync();
        giro.RelatedPublicId.Should().Be(d.Order.PublicId);
        JsonDocument.Parse(giro.PayloadJson).RootElement.GetProperty("replacesOrderPublicId").GetGuid().Should().Be(d.Order.PublicId);
    }

    // ---------------------------------------------------------------------------------------------- anular el crédito --

    [Fact]
    public async Task Un_credito_activo_no_se_anula()
    {
        var d = await DesembolsadoAsync();

        var r = await _d.AnularCredito().Handle(new VoidPendingLoanCommand(d.LoanPublicId, "desistió") { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be(ErroresDeDesembolso.CodigoCreditoNoPendiente);
    }

    [Fact]
    public async Task Anular_el_credito_pendiente_reversa_cada_recogida_por_el_contrato()
    {
        var viejo = _d.O.Credito(_d.O.Ana, 1_000_000m, "CR-VIEJO");
        var s = await _d.AprobadaAsync(_d.O.Solicitud() with { OwnLoanPayoffs = [new OwnLoanPayoffInput(viejo.PublicId)] });
        var d = await _d.DesembolsadoAsync(_d.Entrada(s) with { OwnLoanPayoffs = [new OwnLoanPayoffInput(viejo.PublicId)] });
        // El contrato real (fase 9) deja la operación de pago del recogido; aquí se registra la que el doble informó.
        var clave = RecogidaEnElDesembolso.Clave(d.OperationPublicId, viejo.PublicId);
        _d.Db.LoanOperations.Add(new LoanOperation
        {
            LoanId = viejo.Id, Kind = LoanOperationKind.Payment, Number = "PAG-1", Sequence = 1, ValueDate = DesembolsoDePrueba.Hoy,
            AccountingDate = DesembolsoDePrueba.Hoy, RegisteredAt = _d.G.UtcNow, Amount = 1_000_000m, CollectorCode = "CAR", CollectorClass = "OwnLoanPayoff",
            IdempotencyKey = clave, Payload = "{}", InputsSha256 = "x", RulesVersion = "LND-2026.1",
        });
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();
        await RechazadaAsync(d);
        var reverso = Guid.NewGuid();
        SolicitudDeReverso? pedida = null;
        _d.Contrato.ReversarAsync(Arg.Do<SolicitudDeReverso>(x => pedida = x), Arg.Any<CancellationToken>()).Returns(Task.FromResult(
            new RespuestaDelContratoDto<ComprobanteDeReversoDto>(ResultadoDelContrato.Applied, "PagoReversado", "Reversado",
                new ComprobanteDeReversoDto(reverso, "PRV-1", Guid.NewGuid(), "PAG-1", viejo.PublicId, viejo.Number, DesembolsoDePrueba.Hoy, _d.G.UtcNow,
                    1_000_000m, "desistió", [], [], new SaldosDeObligacionDto(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0), true, []))));

        var r = await _d.AnularCredito().Handle(new VoidPendingLoanCommand(d.LoanPublicId, "El asociado desistió") { OperationKey = Guid.NewGuid() }, default);
        _d.Db.ChangeTracker.Clear();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.PayoffReversalOperationPublicIds.Should().Equal(reverso);
        (pedida!.Collector.Code, pedida.Collector.Class, pedida.PaymentIdempotencyKey).Should().Be(("CAR", ClaseDeRecaudador.OwnLoanPayoff, clave));
        var credito = await _d.Db.Loans.SingleAsync(l => l.PublicId == d.LoanPublicId);
        (credito.Status, credito.VoidedOn, credito.VoidReason).Should().Be((LoanStatus.Voided, (DateOnly?)DesembolsoDePrueba.Hoy, "El asociado desistió"));
    }

    [Fact]
    public async Task Si_el_contrato_no_reversa_una_recogida_el_credito_no_se_anula()
    {
        var viejo = _d.O.Credito(_d.O.Ana, 1_000_000m, "CR-VIEJO");
        var s = await _d.AprobadaAsync(_d.O.Solicitud() with { OwnLoanPayoffs = [new OwnLoanPayoffInput(viejo.PublicId)] });
        var d = await _d.DesembolsadoAsync(_d.Entrada(s) with { OwnLoanPayoffs = [new OwnLoanPayoffInput(viejo.PublicId)] });
        _d.Db.LoanOperations.Add(new LoanOperation
        {
            LoanId = viejo.Id, Kind = LoanOperationKind.Payment, Number = "PAG-1", Sequence = 1, ValueDate = DesembolsoDePrueba.Hoy,
            AccountingDate = DesembolsoDePrueba.Hoy, RegisteredAt = _d.G.UtcNow, Amount = 1_000_000m, CollectorCode = "CAR",
            IdempotencyKey = RecogidaEnElDesembolso.Clave(d.OperationPublicId, viejo.PublicId), Payload = "{}", InputsSha256 = "x", RulesVersion = "LND-2026.1",
        });
        await _d.Db.SaveChangesAsync();
        _d.Db.ChangeTracker.Clear();
        await RechazadaAsync(d);
        _d.Contrato.ReversarAsync(Arg.Any<SolicitudDeReverso>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(
            new RespuestaDelContratoDto<ComprobanteDeReversoDto>(ResultadoDelContrato.Rejected, ContratoDeObligacionesSinActivar.CodigoNoActivada, "no", null)));

        var r = await _d.AnularCredito().Handle(new VoidPendingLoanCommand(d.LoanPublicId, "desistió") { OperationKey = Guid.NewGuid() }, default);

        r.Error.Code.Should().Be(ContratoDeObligacionesSinActivar.CodigoNoActivada);
        _d.Db.ChangeTracker.Clear();
        (await _d.Db.Loans.SingleAsync(l => l.PublicId == d.LoanPublicId)).Status.Should().Be(LoanStatus.PendingDisbursement);
    }
}
