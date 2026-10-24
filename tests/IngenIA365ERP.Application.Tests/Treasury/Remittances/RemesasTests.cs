using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Vaults;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Application.Tests.Receipts.Sessions;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.Remittances;
using IngenIA365ERP.Application.Treasury.Tareas;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Entities.Receipts.Vaults;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Enums.Treasury;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Treasury.Remittances;

/// <summary>
/// Feature 015, US19, T0223 (R-15; FR-035, FR-105, FR-182): Recaudo despacha desde la bóveda con doble custodia, acta y sello, y registra la remesa por
/// <see cref="IRemesasDeTesoreria.RegistrarAsync"/> en la misma transacción con el hecho <c>RemesaDespachada</c>; Tesorería la confirma con soporte o por la línea del
/// extracto (<c>RemesaConfirmada</c>); banco a bóveda queda en tránsito hasta que la bóveda la recibe; sin confirmar a los días del parámetro alerta; desde una cuenta del
/// fondo de liquidez exige uso aprobado. Prueba escrita, sin correr.
/// </summary>
public class RemesasTests
{
    private readonly EscenarioDeTesoreria _t = new();

    private AuditoriaDeTesoreria Auditoria => new(_t.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance);

    private RemesasDeTesoreria Puerto => new(_t.E.Db, _t.Numerador, Auditoria, _t.E.Reloj);

    private async Task<CashRemittanceDto> EnTransitoAsync(decimal valor = 500_000m)
    {
        var r = await Puerto.RegistrarAsync(new SolicitudDeRemesa(Guid.NewGuid(), _t.Pagos.PublicId, _t.E.OficinaA.PublicId, valor, EscenarioDeRecaudo.Hoy.AddDays(-5),
            "BVMC-0001", Guid.NewGuid(), Guid.NewGuid(), "SELLO-1", Guid.NewGuid()), default);
        await _t.E.Db.SaveChangesAsync();
        var fila = await _t.E.Db.CashRemittances.SingleAsync(x => x.PublicId == r.RemittancePublicId);
        return await RemesasDeTesoreria.DtoAsync(_t.E.Db, fila, EscenarioDeRecaudo.Hoy, default);
    }

    [Fact]
    public async Task La_remesa_queda_en_transito_y_repetir_la_clave_no_crea_otra()
    {
        var clave = Guid.NewGuid();
        var s = new SolicitudDeRemesa(clave, _t.Pagos.PublicId, _t.E.OficinaA.PublicId, 300_000m, EscenarioDeRecaudo.Hoy, "BVMC-0002", Guid.NewGuid(), Guid.NewGuid(), "S-2");

        var a = await Puerto.RegistrarAsync(s, default);
        await _t.E.Db.SaveChangesAsync();
        var b = await Puerto.RegistrarAsync(s, default);

        b.RemittancePublicId.Should().Be(a.RemittancePublicId);
        var fila = await _t.E.Db.CashRemittances.SingleAsync();
        fila.Status.Should().Be(RemittanceState.InTransit);
        fila.Direction.Should().Be(RemittanceDirection.VaultToBank);
        fila.BagSeal.Should().Be("S-2");
    }

    [Fact]
    public async Task Se_confirma_con_soporte_y_emite_la_remesa_confirmada()
    {
        var remesa = await EnTransitoAsync();
        var confirmar = new ConfirmRemittanceCommandHandler(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _t.Emision, Auditoria, _t.E.Reloj);

        var sin = await confirmar.Handle(new ConfirmRemittanceCommand(remesa.PublicId) { OperationKey = Guid.NewGuid() }, default);
        sin.Error.Code.Should().Be(ErroresDeTesoreria.RemesaSinSoporte().Code);

        var r = await confirmar.Handle(new ConfirmRemittanceCommand(remesa.PublicId, Guid.NewGuid()) { OperationKey = Guid.NewGuid() }, default);
        r.Value.Status.Should().Be(RemittanceState.Confirmed);
        r.Value.ConfirmedOn.Should().Be(EscenarioDeRecaudo.Hoy);
        var hecho = await _t.E.Db.IntegrationMessages.SingleAsync(m => m.Type == RemesaConfirmadaV1.Type);
        hecho.PayloadJson.Should().Contain("\"Banco\"").And.Contain("EfectivoEnTransito");
    }

    [Fact]
    public async Task Sin_confirmar_a_los_dias_del_parametro_alerta()
    {
        _t.E.Parametro(ParametrosDeRecaudoYTesoreria.RemesasDiasParaConfirmar, "2", ParametrosDeRecaudoYTesoreria.ModuloTesoreria);
        await EnTransitoAsync();

        var alertadas = await new VigilanciaDeTesoreria(_t.E.Db, _t.Parametros, Substitute.For<ICalendarioHabil>(), _t.E.Alertas, _t.E.Reloj).RemesasSinConfirmarAsync(default);

        alertadas.Should().Be(1);
        await _t.E.Alertas.Received().LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.TesoreriaRemesaSinConfirmar), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Del_banco_a_la_boveda_queda_en_transito_hasta_que_la_boveda_la_recibe()
    {
        var boveda = Guid.NewGuid();
        var r = await new RegisterBankToVaultRemittanceCommandHandler(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _t.Numerador, _t.Fondo, _t.Emision, Auditoria, _t.E.Reloj)
            .Handle(new RegisterBankToVaultRemittanceCommand(_t.Pagos.PublicId, boveda, _t.E.OficinaA.PublicId, 200_000m, "S-3") { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.Status.Should().Be(RemittanceState.InTransit);
        (await _t.E.Db.IntegrationMessages.AnyAsync(m => m.Type == RemesaConfirmadaV1.Type)).Should().BeTrue();

        var otraBoveda = () => Puerto.RecibidaEnBovedaAsync(r.Value.PublicId, Guid.NewGuid(), Guid.NewGuid(), 200_000m, default);
        await otraBoveda.Should().ThrowAsync<InvalidOperationException>();
        var estado = await Puerto.RecibidaEnBovedaAsync(r.Value.PublicId, boveda, Guid.NewGuid(), 200_000m, default);
        estado.State.Should().Be(nameof(RemittanceState.Confirmed));
    }

    [Fact]
    public async Task Desde_una_cuenta_del_fondo_de_liquidez_exige_uso_aprobado()
    {
        var r = await new RegisterBankToVaultRemittanceCommandHandler(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _t.Numerador, _t.Fondo, _t.Emision, Auditoria, _t.E.Reloj)
            .Handle(new RegisterBankToVaultRemittanceCommand(_t.FondoDeLiquidez.PublicId, Guid.NewGuid(), _t.E.OficinaA.PublicId, 200_000m) { OperationKey = Guid.NewGuid() },
                default);

        r.Error.Code.Should().Be(ErroresDeTesoreria.UsoDelFondoRequerido(200_000m).Code);
    }

    [Fact]
    public async Task Recaudo_despacha_desde_la_boveda_con_doble_custodia_acta_y_sello_en_la_misma_transaccion()
    {
        var c = new CajaDePrueba();
        var remesas = Substitute.For<IRemesasDeTesoreria>();
        var remesa = Guid.NewGuid();
        remesas.RegistrarAsync(Arg.Any<SolicitudDeRemesa>(), Arg.Any<CancellationToken>()).Returns(new RemesaRegistradaDto(remesa, "RM000001"));
        c.E.Db.ReceiptDocumentTypes.Add(new ReceiptDocumentType { Code = "MC", Name = "Movimiento de caja", Kind = ReceiptDocumentKind.CashMovement });
        c.E.Db.VaultDenominationBalances.Add(new VaultDenominationBalance { VaultId = c.Boveda.Id, CashDenominationId = c.B50.Id, Quantity = 10, Amount = 10 * c.B50.Value });
        c.E.Db.SaveChanges();
        c.E.Como(c.E.Supervisor);
        var despachar = new DispatchVaultRemittanceCommandHandler(c.E.Db, c.E.Alcance, c.E.Actor, c.E.Reloj, c.Cerrojo, c.Numerador, c.E.Segregacion, c.E.Parametros, remesas,
            c.Emision, new AuditoriaDeRecaudo(c.E.Servicios, NullLogger<AuditoriaDeRecaudo>.Instance));
        var cuenta = Guid.NewGuid();

        var sinSegundo = await despachar.Handle(new DispatchVaultRemittanceCommand(c.Boveda.PublicId, cuenta, CajaDePrueba.Billetes((c.B50, 4)), "S-9", Guid.NewGuid())
            { OperationKey = Guid.NewGuid() }, default);
        sinSegundo.Error.Code.Should().Be(ErroresDeRecaudo.DobleCustodia().Code);

        var r = await despachar.Handle(new DispatchVaultRemittanceCommand(c.Boveda.PublicId, cuenta, CajaDePrueba.Billetes((c.B50, 4)), "S-9", Guid.NewGuid(),
            c.E.OtroCajero.PublicId) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.RemittanceNumber.Should().Be("RM000001");
        await remesas.Received(1).RegistrarAsync(Arg.Is<SolicitudDeRemesa>(s => s.Amount == 4 * c.B50.Value && s.BagSeal == "S-9" && s.OwnBankAccountPublicId == cuenta),
            Arg.Any<CancellationToken>());
        var movimiento = await c.E.Db.VaultMovements.SingleAsync();
        movimiento.Kind.Should().Be(VaultMovementKind.RemittanceOut);
        movimiento.RemittancePublicId.Should().Be(remesa);
        (await c.E.Db.VaultDenominationBalances.SingleAsync()).Quantity.Should().Be(6);
        (await c.E.Db.IntegrationMessages.AnyAsync(m => m.Type == RemesaDespachadaV1.Type)).Should().BeTrue();
    }
}
