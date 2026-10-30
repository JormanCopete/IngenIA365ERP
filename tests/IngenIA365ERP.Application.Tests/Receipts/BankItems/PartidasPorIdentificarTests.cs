using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Receipts.BankItems;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.CreditBalances;
using IngenIA365ERP.Application.Receipts.Tareas;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Receipts.BankItems;

/// <summary>
/// Feature 015, US6, T0188 (FR-070, FR-072, FR-073, FR-199; D10): la partida sin persona con su hecho; identificada entera o en partes; clasificada a un concepto con la
/// aprobación de otra persona; devuelta al consignante por orden de pago (y reabierta si la orden se rechaza); los escalones 30, 60 y 90 alertan y el consignante
/// repetido va al oficial; llevarla a ingreso es una decisión registrada con aprobación, nunca automática; el giro devuelto se clasifica al concepto «giro devuelto»
/// enlazado a la orden, alerta al origen y al tesorero y no cambia el resultado. Prueba escrita, sin correr.
/// </summary>
public class PartidasPorIdentificarTests
{
    private readonly BancosDePrueba _b = new();

    private async Task<UnidentifiedItemDto> PartidaAsync(decimal valor, int diasAtras = 1, string? consignante = "9001")
    {
        var abono = _b.Abono(valor, diasAtras, consignante: consignante);
        var r = await _b.DejarComoPartida().Handle(new MarkBankItemUnidentifiedCommand(abono.PublicId) { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        return r.Value;
    }

    [Fact]
    public async Task La_partida_nace_sin_persona_con_su_hecho_de_reconocimiento()
    {
        var p = await PartidaAsync(75_000m);

        p.Status.Should().Be(UnidentifiedItemState.Open);
        p.Balance.Should().Be(75_000m);
        (await _b.K.C.E.Db.IntegrationMessages.AnyAsync(m => m.Type == PartidaPorIdentificarReconocidaV1.Type)).Should().BeTrue();
    }

    [Fact]
    public async Task Se_identifica_despues_en_partes()
    {
        var p = await PartidaAsync(100_000m);
        var identificar = new IdentifyUnidentifiedItemCommandHandler(_b.K.C.E.Db, _b.K.C.E.Alcance, _b.K.C.E.Actor, _b.Aplicacion, _b.Auditoria, _b.K.C.E.Reloj);

        var r = await identificar.Handle(new IdentifyUnidentifiedItemCommand(p.PublicId, [_b.CuotaA(60_000m)]) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.Status.Should().Be(UnidentifiedItemState.PartiallyIdentified);
        r.Value.Balance.Should().Be(40_000m);
        var hecho = await _b.K.C.E.Db.IntegrationMessages.SingleAsync(m => m.Type == ReciboDeBancoEmitidoV1.Type);
        hecho.Should().NotBeNull("la identificación de una partida es un recibo de banco desde PartidasPorIdentificar");
    }

    [Fact]
    public async Task La_clasificacion_a_concepto_pide_la_aprobacion_de_otra_persona()
    {
        var p = await PartidaAsync(20_000m);
        _b.K.C.Motor.SolicitarAsync(Arg.Any<Application.Common.Approvals.SolicitudDeAprobacion>(), Arg.Any<CancellationToken>())
            .Returns(ci => Application.Common.Models.Result.Success<ApprovalRequest?>(new ApprovalRequest
            {
                SourcePublicId = ci.ArgAt<Application.Common.Approvals.SolicitudDeAprobacion>(0).SourcePublicId, Subject = ApprovalSubjects.BankItemClassification,
            }));
        var clasificar = new ClassifyUnidentifiedItemCommandHandler(_b.K.C.E.Db, _b.K.C.E.Alcance, _b.K.C.E.Actor, _b.K.C.Cerrojo, _b.K.C.Presente, _b.Emision, _b.Auditoria,
            _b.K.C.E.Reloj);

        var r = await clasificar.Handle(new ClassifyUnidentifiedItemCommand(p.PublicId, _b.Rendimiento.PublicId, "Rendimientos del mes") { OperationKey = Guid.NewGuid() }, default);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Approvals.PresenceRequired", "la clasificación la aprueba otra persona presente (sujeto BankItemClassification)");
        (await _b.K.C.E.Db.UnidentifiedItemResolutions.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Clasificada_con_aprobacion_queda_clasificada_con_su_hecho()
    {
        var p = await PartidaAsync(20_000m);
        var clasificar = new ClassifyUnidentifiedItemCommandHandler(_b.K.C.E.Db, _b.K.C.E.Alcance, _b.K.C.E.Actor, _b.K.C.Cerrojo, _b.K.C.Presente, _b.Emision, _b.Auditoria,
            _b.K.C.E.Reloj);

        var r = await clasificar.Handle(new ClassifyUnidentifiedItemCommand(p.PublicId, _b.Rendimiento.PublicId, "Rendimientos") { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.Status.Should().Be(UnidentifiedItemState.Classified);
        (await _b.K.C.E.Db.IntegrationMessages.AnyAsync(m => m.Type == PartidaClasificadaV1.Type)).Should().BeTrue();
    }

    [Fact]
    public async Task Se_devuelve_por_orden_de_pago_y_se_reabre_si_la_orden_se_rechaza()
    {
        var p = await PartidaAsync(30_000m);
        var orden = Guid.NewGuid();
        _b.Ordenes.EmitirAsync(Arg.Any<SolicitudDeOrdenDePago>(), Arg.Any<CancellationToken>()).Returns(new OrdenDePagoEmitidaDto(orden, "OP-000010", "Ready"));
        var devolver = new RefundUnidentifiedItemByOrderCommandHandler(_b.K.C.E.Db, _b.K.C.E.Alcance, _b.K.C.E.Actor, _b.K.C.Cerrojo, _b.Ordenes, _b.Emision, _b.Auditoria,
            _b.K.C.E.Reloj);

        var r = await devolver.Handle(new RefundUnidentifiedItemByOrderCommand(p.PublicId, _b.K.Asociado.PublicId) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.Status.Should().Be(UnidentifiedItemState.ReturnedToDepositor);
        await _b.Ordenes.Received(1).EmitirAsync(Arg.Is<SolicitudDeOrdenDePago>(s => s.OrderKind == ClasesDeOrdenDeRecaudo.DevolucionDePartida && s.NetAmount == 30_000m),
            Arg.Any<CancellationToken>());
        (await _b.K.C.E.Db.IntegrationMessages.AnyAsync(m => m.Type == PartidaDevueltaAlConsignanteV1.Type)).Should().BeTrue();

        var resultado = new ResultadoDeOrdenesDeRecaudo(_b.K.C.E.Db, _b.K.C.Cerrojo, _b.Auditoria, _b.K.C.E.Reloj);
        await resultado.InformarAsync(orden, "Rejected", EscenarioDeRecaudo.Hoy, "Cuenta inexistente", default);
        await _b.K.C.E.Db.SaveChangesAsync();

        var reabierta = await _b.K.C.E.Db.UnidentifiedItems.SingleAsync();
        reabierta.Balance.Should().Be(30_000m);
        reabierta.Status.Should().Be(UnidentifiedItemState.Open);
        (await _b.K.C.E.Db.UnidentifiedItemResolutions.AnyAsync(x => x.Kind == UnidentifiedItemResolutionKind.RefundRejected)).Should().BeTrue();
    }

    [Fact]
    public async Task Los_escalones_alertan_una_vez_cada_uno_y_el_consignante_repetido_va_al_oficial()
    {
        await PartidaAsync(10_000m, diasAtras: 35, consignante: "7777");
        await PartidaAsync(15_000m, diasAtras: 95, consignante: "7777");
        var antiguedad = new AntiguedadDeRecaudo(_b.K.C.E.Db, _b.K.C.E.Parametros, _b.Movimientos, Substitute.For<Application.Common.Calendar.ICalendarioHabil>(),
            _b.K.C.E.Alertas, _b.K.C.E.Reloj);

        var primera = await antiguedad.PartidasAsync(default);
        var segunda = await antiguedad.PartidasAsync(default);

        primera.Should().Be(3, "un escalón de 30, uno de 90 y el consignante repetido");
        (await _b.K.C.E.Db.UnidentifiedItems.Select(p => p.AgingStep).OrderBy(x => x).ToListAsync()).Should().Equal((short)30, (short)90);
        segunda.Should().Be(1, "el escalón ya alertado no se repite (sólo la señal del consignante, que la deduplica la alerta)");
        await _b.K.C.E.Alertas.Received().LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.RecaudoPartidaAntigua
            && a.RecipientPermissions != null && a.RecipientPermissions.Contains(AntiguedadDeRecaudo.PermisoDelOficial)), Arg.Any<CancellationToken>());
        (await _b.K.C.E.Db.UnidentifiedItems.AllAsync(p => p.Status == UnidentifiedItemState.Open)).Should().BeTrue("la antigüedad sólo alerta");
    }

    [Fact]
    public async Task Llevarla_a_ingreso_exige_la_politica_que_lo_admite()
    {
        var p = await PartidaAsync(5_000m, diasAtras: 120);
        // Un handler por pedido: el lector de parámetros recuerda lo leído durante el pedido (en la API es de ámbito).
        MoveUnidentifiedItemToIncomeCommandHandler aIngreso() => new(_b.K.C.E.Db, _b.K.C.E.Alcance, _b.K.C.E.Actor, _b.K.C.Cerrojo, _b.K.C.E.Parametros, _b.K.C.Presente,
            _b.Emision, _b.Auditoria, _b.K.C.E.Reloj);

        var sinPolitica = await aIngreso().Handle(new MoveUnidentifiedItemToIncomeCommand(p.PublicId, _b.Rendimiento.PublicId, "Vencida") { OperationKey = Guid.NewGuid() }, default);
        sinPolitica.Error.Code.Should().Be("Receipts.UnidentifiedItem.DestinationNotIncome", "por defecto siguen como pasivo (D10)");

        _b.K.C.E.Parametro(ParametrosDeRecaudoYTesoreria.PartidasDestinoVencido, "Ingreso"); // los valores admitidos son SiguenComoPasivo e Ingreso
        var conPolitica = await aIngreso().Handle(new MoveUnidentifiedItemToIncomeCommand(p.PublicId, _b.Rendimiento.PublicId, "Vencida") { OperationKey = Guid.NewGuid() }, default);
        conPolitica.IsSuccess.Should().BeTrue(conPolitica.IsFailure ? conPolitica.Error.Code : null);
        conPolitica.Value.Status.Should().Be(UnidentifiedItemState.Classified);
    }

    [Fact]
    public async Task El_giro_devuelto_se_clasifica_enlazado_a_la_orden_y_alerta()
    {
        var abono = _b.Abono(400_000m);
        var orden = Guid.NewGuid();

        var r = await _b.Clasificar().Handle(new ClassifyBankItemCommand(abono.PublicId, _b.GiroDevuelto.PublicId, "El banco devolvió la transferencia", null, orden)
        {
            OperationKey = Guid.NewGuid(),
        }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code : null);
        r.Value.Outcome.Should().Be(BankItemOutcome.ReturnedDisbursement);
        r.Value.PaymentOrderPublicId.Should().Be(orden);
        await _b.K.C.E.Alertas.Received().LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.TesoreriaGiroDevuelto), Arg.Any<CancellationToken>());
        var sinOrden = await _b.Clasificar().Handle(new ClassifyBankItemCommand(_b.Abono(1_000m).PublicId, _b.GiroDevuelto.PublicId, "x") { OperationKey = Guid.NewGuid() }, default);
        sinOrden.Error.Code.Should().Be("Receipts.BankItem.ReturnedDisbursementOrderRequired");
    }
}
