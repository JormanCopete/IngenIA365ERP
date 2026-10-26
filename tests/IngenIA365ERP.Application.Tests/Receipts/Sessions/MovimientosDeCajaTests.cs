using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Sessions;

/// <summary>
/// Feature 015, T0081 (FR-030, FR-182; US2-7): abastecer desde la bóveda y retirar a la bóveda con doble custodia (<c>Caja.DobleCustodia</c>), trasladar entre
/// cajas, reclasificar un medio sin tocar el recibo —desde efectivo con soporte y aprobador presente distinto del cajero—, con sus hechos
/// <c>MovimientoDeCajaRegistrado</c> y <c>MedioReclasificado</c>. Escrita en la fase 5; se corre al final del plan.
/// </summary>
public class MovimientosDeCajaTests
{
    private readonly CajaDePrueba _c = new();

    private async Task<Result<CashMovementDto>> MoverAsync(CashSessionDto sesion, CashMovementKindR clase, Guid? segundo, params (Domain.Entities.Core.Payments.CashDenomination, int)[] billetes) =>
        await _c.Mover().Handle(new RegisterCashMovementCommand(sesion.PublicId, clase, CajaDePrueba.Billetes(billetes), "prueba", null, segundo), default);

    [Fact]
    public async Task Abastecer_desde_la_boveda_exige_el_segundo_custodio_y_descuenta_la_boveda()
    {
        await _c.SurtirBovedaAsync((_c.B50, 20));
        var sesion = await _c.AbrirAsync();

        (await MoverAsync(sesion, CashMovementKindR.SupplyFromVault, null, (_c.B50, 4))).Error.Code.Should().Be(ErroresDeRecaudo.CodigoDobleCustodia);
        (await MoverAsync(sesion, CashMovementKindR.SupplyFromVault, _c.E.CajeraBis.PublicId, (_c.B50, 4))).Error.Code
            .Should().Be(ErroresDeRecaudo.CodigoDobleCustodia, "el segundo custodio es la misma persona que la cajera");

        var r = await MoverAsync(sesion, CashMovementKindR.SupplyFromVault, _c.E.Supervisor.PublicId, (_c.B50, 4));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        (await _c.E.Db.VaultDenominationBalances.SingleAsync()).Quantity.Should().Be(16);
        var esperado = await SesionesDeCaja.EfectivoAsync(_c.E.Db, await _c.E.Db.ReceiptsCashSessions.SingleAsync(s => s.PublicId == sesion.PublicId), default);
        esperado.Should().Be(700_000);
        var hecho = await _c.E.Db.IntegrationMessages.SingleAsync(m => m.Type == MovimientoDeCajaRegistradoV1.Type && m.OriginPublicId == r.Value.PublicId); // el surtido de la bóveda emite el suyo
        hecho.OriginModule.Should().Be("REC");
    }

    [Fact]
    public async Task Sin_doble_custodia_basta_el_que_registra()
    {
        _c.E.Parametro(ParametrosDeRecaudoYTesoreria.CajaDobleCustodia, "false");
        await _c.SurtirBovedaAsync((_c.B50, 20));
        var sesion = await _c.AbrirAsync();

        (await MoverAsync(sesion, CashMovementKindR.SupplyFromVault, null, (_c.B50, 2))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task La_boveda_sin_esas_denominaciones_no_abastece()
    {
        await _c.SurtirBovedaAsync((_c.B50, 1));
        var sesion = await _c.AbrirAsync();

        var r = await MoverAsync(sesion, CashMovementKindR.SupplyFromVault, _c.E.Supervisor.PublicId, (_c.B50, 3));

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoSaldoDeBovedaInsuficiente);
    }

    [Fact]
    public async Task Retirar_a_la_boveda_no_saca_mas_de_lo_esperado_y_suma_a_la_boveda()
    {
        var sesion = await _c.AbrirAsync();

        (await MoverAsync(sesion, CashMovementKindR.WithdrawToVault, _c.E.Supervisor.PublicId, (_c.B50, 11))).Error.Code
            .Should().Be(ErroresDeRecaudo.CodigoEfectivoInsuficiente);
        (await MoverAsync(sesion, CashMovementKindR.WithdrawToVault, _c.E.Supervisor.PublicId, (_c.B50, 6))).IsSuccess.Should().BeTrue();

        (await _c.E.Db.VaultDenominationBalances.SingleAsync()).Amount.Should().Be(300_000);
        (await _c.E.Db.VaultMovements.SingleAsync()).Kind.Should().Be(VaultMovementKind.ReceiveFromBox);
    }

    [Fact]
    public async Task El_traslado_entre_cajas_mueve_lo_esperado_de_una_a_otra()
    {
        var origen = await _c.AbrirAsync();
        var destino = await _c.AbrirAsync(_c.C2, _c.E.OtroCajero);
        _c.E.Como(_c.E.Cajera);

        var r = await _c.Mover().Handle(new RegisterCashMovementCommand(origen.PublicId, CashMovementKindR.TransferBetweenBoxes,
            CajaDePrueba.Billetes((_c.B50, 2)), "Cambio", destino.PublicId), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var sesiones = await _c.E.Db.ReceiptsCashSessions.ToListAsync();
        (await SesionesDeCaja.EfectivoAsync(_c.E.Db, sesiones.Single(s => s.PublicId == origen.PublicId), default)).Should().Be(400_000);
        (await SesionesDeCaja.EfectivoAsync(_c.E.Db, sesiones.Single(s => s.PublicId == destino.PublicId), default)).Should().Be(600_000);
    }

    [Fact]
    public async Task Reclasificar_desde_efectivo_exige_soporte_y_aprobador_presente_y_no_toca_el_recibo()
    {
        var sesion = await _c.AbrirAsync();
        var sinSoporte = await _c.Reclasificar().Handle(new ReclassifyMeansCommand(sesion.PublicId, _c.Efectivo.PublicId, _c.Transferencia.PublicId, 100_000,
            "Era transferencia"), default);
        sinSoporte.Error.Code.Should().Be(ErroresDeRecaudo.CodigoSoporteRequerido);

        var nueva = new ApprovalRequest { Subject = ApprovalSubjects.CashMeansReclassification, SourceType = ApprovalSourceTypes.ReceiptsOperation, SourcePublicId = sesion.PublicId };
        _c.Motor.SolicitarAsync(Arg.Any<SolicitudDeAprobacion>(), Arg.Any<CancellationToken>())
            .Returns(_ => { _c.E.Db.ApprovalRequests.Add(nueva); return Result.Success<ApprovalRequest?>(nueva); });
        var pedida = await _c.Reclasificar().Handle(new ReclassifyMeansCommand(sesion.PublicId, _c.Efectivo.PublicId, _c.Transferencia.PublicId, 100_000,
            "Era transferencia", Guid.NewGuid()), default);
        pedida.Error.Code.Should().Be(ErroresDeRecaudo.CodigoPresenciaRequerida);
        await _c.Motor.Received().SolicitarAsync(Arg.Is<SolicitudDeAprobacion>(s => s.Participantes.Contains(_c.E.Cajera.Id)), Arg.Any<CancellationToken>());
        (await _c.E.Db.CashMovements.CountAsync()).Should().Be(1, "sólo la apertura: el 428 no deja la reclasificación");
    }

    [Fact]
    public async Task Reclasificar_entre_medios_que_no_son_efectivo_no_pide_aprobador_y_emite_su_hecho()
    {
        var sesion = await _c.AbrirAsync();
        var otra = _c.E.Medio("CONS", Domain.Enums.Core.PaymentMeansClass.BankDeposit, 0);
        _c.E.Ofrecer(_c.Punto, otra);
        // La consignación no estaba sellada en esta sesión: no se reclasifica a ella.
        var ajeno = await _c.Reclasificar().Handle(new ReclassifyMeansCommand(sesion.PublicId, _c.Transferencia.PublicId, otra.PublicId, 1, "x"), default);
        ajeno.Error.Code.Should().Be("Generic.NotFound");

        var r = await _c.Reclasificar().Handle(new ReclassifyMeansCommand(sesion.PublicId, _c.Transferencia.PublicId, _c.Efectivo.PublicId, 1, "x"), default);
        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoMovimientoInvalido, "la transferencia no tiene esperado todavía");
    }

    [Fact]
    public async Task La_sesion_olvidada_no_recibe_movimientos()
    {
        var sesion = await _c.AbrirAsync();
        var s = await _c.E.Db.ReceiptsCashSessions.SingleAsync();
        s.Status = CashSessionState.ForgottenOpen;
        await _c.E.Db.SaveChangesAsync();

        var r = await MoverAsync(sesion, CashMovementKindR.WithdrawToVault, _c.E.Supervisor.PublicId, (_c.B50, 1));

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoSesionOlvidada);
    }

    [Fact]
    public async Task Los_hechos_de_la_caja_nacen_sin_pasar_si_la_contabilidad_no_esta_iniciada()
    {
        var sesion = await _c.AbrirAsync();
        await MoverAsync(sesion, CashMovementKindR.WithdrawToVault, _c.E.Supervisor.PublicId, (_c.B50, 1));

        var entrega = await _c.E.Db.IntegrationMessageDeliveries.SingleAsync();
        entrega.Mode.Should().Be(DeliveryMode.NotPosted);
    }
}
