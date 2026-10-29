using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.Reconciliation;
using IngenIA365ERP.Application.Treasury.Transfers;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Entities.Treasury.Statements;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Treasury.Transfers;

/// <summary>
/// Feature 015, T1, T0407 (FR-104, FR-207, FR-012; US19-4): el traslado entre cuentas propias se pide con aprobación diferida por monto (<c>Treasury.Transfers.Approve</c>);
/// aprobar no mueve dinero; enviarlo emite <c>TrasladoEntreCuentasEnviado</c> y lo deja en tránsito; al cruzar el abono en el extracto de la cuenta de destino o confirmarlo con
/// soporte emite <c>TrasladoConfirmado</c>; desde una cuenta del fondo de liquidez exige un uso aprobado; quien pide no aprueba. Prueba escrita, sin correr.
/// </summary>
public class TrasladosEntreCuentasTests
{
    private readonly EscenarioDeTesoreria _t = new();
    private readonly OwnBankAccount _ahorros;

    public TrasladosEntreCuentasTests()
    {
        _ahorros = new OwnBankAccount
        {
            Code = "AHORROS", Name = "Cuenta de ahorros", BankId = _t.Banco.Id, AccountNumber = "4000555555", BranchId = _t.E.OficinaA.Id,
            Purposes = BankAccountPurpose.Collection, ValidFrom = EscenarioDeRecaudo.Hoy.AddYears(-1),
        };
        _t.E.Db.OwnBankAccounts.Add(_ahorros);
        _t.E.Db.SaveChanges();
    }

    private AuditoriaDeTesoreria Auditoria => new(_t.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance);

    private RequestTransferCommandHandler Pedir() => new(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _t.Numerador, _t.Aprobacion, _t.Fondo, _t.Emision, Auditoria, _t.E.Reloj);

    private MarkTransferSentCommandHandler Enviar() => new(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _t.Fondo, _t.Emision, Auditoria, _t.E.Reloj);

    private ConfirmTransferCommandHandler Confirmar() => new(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _t.Emision, Auditoria, _t.E.Reloj);

    private async Task<TransferDto> AprobadoAsync(OwnBankAccount desde, decimal valor = 50_000_000m)
    {
        var pedido = await Pedir().Handle(new RequestTransferCommand(desde.PublicId, _ahorros.PublicId, valor, "Fondear la cuenta de ahorros") { OperationKey = Guid.NewGuid() },
            default);
        pedido.IsSuccess.Should().BeTrue(pedido.IsFailure ? pedido.Error.Code : null);
        var aprobado = await new AprobableTraslado(_t.E.Db, _t.E.Reloj).AlAprobarAsync(new ApprovalRequest { SourcePublicId = pedido.Value.PublicId }, default);
        aprobado.IsSuccess.Should().BeTrue();
        await _t.E.Db.SaveChangesAsync();
        return pedido.Value;
    }

    [Fact]
    public async Task Se_pide_con_la_aprobacion_diferida_por_monto_y_aprobar_no_mueve_dinero()
    {
        var r = await Pedir().Handle(new RequestTransferCommand(_t.Pagos.PublicId, _ahorros.PublicId, 50_000_000m, "Fondear") { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.Status.Should().Be(TransferState.PendingApproval);
        await _t.Motor.Received(1).SolicitarAsync(Arg.Is<SolicitudDeAprobacion>(s => s.Subject == ApprovalSubjects.TreasuryTransfer
            && s.PermisoLimitado == "Treasury.Transfers.Approve" && s.Amount == 50_000_000m), Arg.Any<CancellationToken>());
        (await _t.E.Db.IntegrationMessages.AnyAsync()).Should().BeFalse("pedir y aprobar no mueven dinero");
    }

    [Fact]
    public async Task No_traslada_a_la_misma_cuenta()
    {
        var r = await Pedir().Handle(new RequestTransferCommand(_t.Pagos.PublicId, _t.Pagos.PublicId, 1m, "x") { OperationKey = Guid.NewGuid() }, default);
        r.Error.Code.Should().Be(ErroresDeTesoreria.CodigoTrasladoMismaCuenta);
    }

    [Fact]
    public async Task Enviarlo_lo_deja_en_transito_con_su_hecho()
    {
        var t = await AprobadoAsync(_t.Pagos);

        var r = await Enviar().Handle(new MarkTransferSentCommand(t.PublicId, BankReference: "PORTAL-77") { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.Status.Should().Be(TransferState.Sent);
        var hecho = await _t.E.Db.IntegrationMessages.SingleAsync(m => m.Type == TrasladoEntreCuentasEnviadoV1.Type);
        hecho.PayloadJson.Should().Contain(RolesDeTesoreriaT1.FondosEnTransito).And.Contain("\"Banco\"");
    }

    [Fact]
    public async Task Confirmarlo_exige_soporte_o_la_linea_del_extracto_y_emite_el_hecho()
    {
        var t = await AprobadoAsync(_t.Pagos);
        (await Enviar().Handle(new MarkTransferSentCommand(t.PublicId) { OperationKey = Guid.NewGuid() }, default)).IsSuccess.Should().BeTrue();

        var sin = await Confirmar().Handle(new ConfirmTransferCommand(t.PublicId) { OperationKey = Guid.NewGuid() }, default);
        var r = await Confirmar().Handle(new ConfirmTransferCommand(t.PublicId, Guid.NewGuid()) { OperationKey = Guid.NewGuid() }, default);

        sin.Error.Code.Should().Be(ErroresDeTesoreria.CodigoTrasladoSinSoporte);
        r.Value.Status.Should().Be(TransferState.Confirmed);
        (await _t.E.Db.IntegrationMessages.AnyAsync(m => m.Type == TrasladoConfirmadoV1.Type)).Should().BeTrue();
    }

    [Fact]
    public async Task El_cruce_del_abono_en_la_conciliacion_de_la_cuenta_de_destino_lo_confirma()
    {
        var t = await AprobadoAsync(_t.Pagos);
        (await Enviar().Handle(new MarkTransferSentCommand(t.PublicId) { OperationKey = Guid.NewGuid() }, default)).IsSuccess.Should().BeTrue();
        var linea = new BankStatementLine
        {
            BankStatementId = 1, BankAccountId = _ahorros.Id, LineNumber = 1, MovementDate = EscenarioDeRecaudo.Hoy, Direction = StatementLineDirection.Credit,
            Amount = 50_000_000m, Fingerprint = "f1",
        };
        _t.E.Db.TreasuryBankStatementLines.Add(linea);
        await _t.E.Db.SaveChangesAsync();

        await ConciliacionMinima.EfectosAsync(_t.E.Db, _t.Emision, _ahorros, ReconciliationRecordKind.Transfer, t.PublicId, linea, _t.E.Cajera.Id, DateTime.UtcNow, default);
        await _t.E.Db.SaveChangesAsync();

        var fila = await _t.E.Db.TreasuryTransfers.SingleAsync();
        fila.Status.Should().Be(TransferState.Confirmed);
        fila.ConfirmedByStatementLinePublicId.Should().Be(linea.PublicId);
        ConciliacionMinima.HechosDelCruce([ReconciliationRecordKind.Transfer]).Should().Contain(TrasladoConfirmadoV1.Type);
    }

    [Fact]
    public async Task Desde_una_cuenta_del_fondo_de_liquidez_exige_uso_aprobado()
    {
        var r = await Pedir().Handle(new RequestTransferCommand(_t.FondoDeLiquidez.PublicId, _ahorros.PublicId, 5_000_000m, "Liquidez") { OperationKey = Guid.NewGuid() },
            default);
        r.Error.Code.Should().Be(ErroresDeTesoreria.UsoDelFondoRequerido(5_000_000m).Code);
    }

    [Fact]
    public async Task Quien_pide_no_aprueba()
    {
        var fuente = new FuenteDeAprobacionDeTesoreria(_t.E.Db, _t.E.Personas, [new AprobableTraslado(_t.E.Db, _t.E.Reloj)]);
        var solicitud = new ApprovalRequest { Subject = ApprovalSubjects.TreasuryTransfer, RequestedByUserId = _t.E.Cajera.Id, CreatedByUserId = _t.E.Cajera.Id };

        var r = await fuente.AlAprobarElNivelAsync(solicitud, _t.E.Cajera.Id, default, esElUltimo: true, default);

        r.Error.Code.Should().Be(ErroresDeTesoreria.CodigoTrasladoAutoaprobado);
    }
}
