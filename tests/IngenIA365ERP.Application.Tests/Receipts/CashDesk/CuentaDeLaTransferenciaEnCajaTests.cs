using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Setup;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Receipts.CashDesk;

/// <summary>
/// Feature 015, segunda ronda del 2026-10-27 (D-f, T0492; FR-231): la transferencia recibida en caja entra a una cuenta propia habilitada para el medio en la caja. Con
/// una sola habilitada se propone; con varias el cajero elige (si no, <c>OwnBankAccountRequired</c> con las opciones); una que no está habilitada se rechaza. El medio
/// del recibo guarda la cuenta y la porción del <c>ReciboEmitido</c> la lleva en el débito a <c>BancoRecaudo</c>.
/// </summary>
public class CuentaDeLaTransferenciaEnCajaTests
{
    private readonly CajaConContratos _k = new();

    [Fact]
    public async Task Con_una_cuenta_habilitada_se_propone_y_llega_al_recibo_y_al_hecho()
    {
        await _k.AbrirAsync();
        var op = await _k.PreparadaAsync([_k.Cuota(_k.CreditoA, 100_000)], [_k.Transferencia(100_000)]);

        var r = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code + r.Error.Message : string.Empty);
        var medio = await _k.C.E.Db.ReceiptMeans.SingleAsync();
        medio.OwnBankAccountPublicId.Should().Be(_k.C.CuentaDeTransferencia);
        var hecho = await _k.C.E.Db.IntegrationMessages.SingleAsync(m => m.Type == ReciboEmitidoV1.Type);
        var contenido = JsonSerializer.Deserialize<ReciboEmitidoV1>(hecho.PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        contenido.Lines.Single(l => l.Role == RolesDeRecaudo.BancoRecaudo).Dimensions.OwnBankAccountPublicId.Should().Be(_k.C.CuentaDeTransferencia);
    }

    [Fact]
    public async Task Con_varias_el_cajero_elige_y_una_no_habilitada_se_rechaza()
    {
        var otra = Guid.NewGuid();
        _k.C.E.Db.TransferAccounts.Add(new TransferAccount
        {
            CollectionPointId = _k.C.Punto.Id, PaymentMeansId = _k.C.Transferencia.Id, OwnBankAccountPublicId = otra, AccountLabel = "CTA-2 · Banco ****9876",
        });
        _k.C.E.Db.SaveChanges();
        await _k.AbrirAsync();
        var op = (await new CreateCashOperationCommandHandler(_k.C.E.Db, _k.C.E.Alcance, _k.C.E.Reloj, _k.Listas)
            .Handle(new CreateCashOperationCommand(_k.Sesion.PublicId, _k.Asociado.PublicId), default)).Value;
        await _k.Items().Handle(new UpdateCashOperationItemsCommand(op.PublicId, [_k.Cuota(_k.CreditoA, 100_000)]), default);

        var sinElegir = await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op.PublicId, [_k.Transferencia(100_000)]), default);
        sinElegir.Error.Code.Should().Be(CuentasDeTransferencia.CodigoRequerida);
        var ajena = await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op.PublicId,
            [_k.Transferencia(100_000) with { OwnBankAccountPublicId = Guid.NewGuid() }]), default);
        ajena.Error.Code.Should().Be(CuentasDeTransferencia.CodigoNoHabilitada);
        var elegida = await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op.PublicId, [_k.Transferencia(100_000) with { OwnBankAccountPublicId = otra }]), default);
        elegida.IsSuccess.Should().BeTrue(elegida.IsFailure ? elegida.Error.Code : null);

        var r = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code + r.Error.Message : string.Empty);
        (await _k.C.E.Db.ReceiptMeans.SingleAsync()).OwnBankAccountPublicId.Should().Be(otra);
    }
}
