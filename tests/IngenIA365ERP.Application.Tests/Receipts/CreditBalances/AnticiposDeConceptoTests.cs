using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.CreditBalances;
using IngenIA365ERP.Application.Receipts.Setup;
using IngenIA365ERP.Application.Receipts.Voidings;
using IngenIA365ERP.Application.Tests.Receipts.CashDesk;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Receipts.Configuration;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.CreditBalances;

/// <summary>
/// 2026-11-23 (pedido del dueño: la administración que el asociado paga antes de que se cause): el concepto vario «anticipo».
/// <list type="bullet">
/// <item>cobrarlo en la caja deja un saldo a favor de la persona con el concepto, y la caja lo muestra con su medio;</item>
/// <item>cruzarlo con el medio del concepto baja el anticipo, de lo más antiguo a lo más nuevo, y no pasa de lo disponible;</item>
/// <item>anular el recibo que lo cruzó lo devuelve al anticipo (no a un saldo a favor genérico); anular el que lo recibió lo cancela, pero no si ya se cruzó;</item>
/// <item>un anticipo no se devuelve como saldo a favor ni alerta por antigüedad.</item>
/// </list>
/// </summary>
public sealed class AnticiposDeConceptoTests
{
    private readonly CajaConContratos _k = new();
    private readonly IAutoridadDeOtroAprobador _permisos = Substitute.For<IAutoridadDeOtroAprobador>();
    private readonly MiscConcept _administracion;
    private readonly PaymentMeans _medioDelAnticipo;

    public AnticiposDeConceptoTests()
    {
        _medioDelAnticipo = _k.C.E.Medio("ANT", PaymentMeansClass.Other, 0, CashCountMethod.None);
        _k.C.E.Ofrecer(_k.C.Punto, _medioDelAnticipo);
        _administracion = new MiscConcept
        {
            Code = "ANTADM", Name = "Anticipo de administración", Direction = MiscConceptDirection.Income, IsAdvance = true, AdvanceMeansId = _medioDelAnticipo.Id,
        };
        _k.C.E.Db.MiscConcepts.Add(_administracion);
        _k.C.E.Db.ReceiptDocumentTypes.Add(new ReceiptDocumentType { Code = "AN", Name = "Anulación", Kind = ReceiptDocumentKind.Voiding });
        _k.C.E.Db.SaveChanges();
        _k.Obligaciones.ReversarAsync(Arg.Any<SolicitudDeReverso>(), Arg.Any<CancellationToken>())
            .Returns(new RespuestaDelContratoDto<ComprobanteDeReversoDto>(ResultadoDelContrato.Applied, "OK", "", null));
        _permisos.TienePermisoAsync(Arg.Any<int>(), AprobarAnulacion.PermisoSinSegundaPersona, Arg.Any<CancellationToken>()).Returns(true);
    }

    private static ItemPedidoDto Anticipo(MiscConcept c, decimal valor) => new(ApplicationTarget.MiscConcept, valor, MiscConceptPublicId: c.PublicId);

    private MedioDeCajaDto ConElAnticipo(decimal valor) => new(_medioDelAnticipo.PublicId, valor, "ANTICIPO");

    private async Task<ReceiptDto> ConfirmarAsync(IReadOnlyList<ItemPedidoDto> items, IReadOnlyList<MedioDeCajaDto> medios)
    {
        var op = await _k.PreparadaAsync(items, medios);
        var r = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        return r.Value;
    }

    private AuditoriaDeRecaudo Auditoria => new(_k.C.E.Servicios, NullLogger<AuditoriaDeRecaudo>.Instance);

    private async Task<IngenIA365ERP.Application.Common.Models.Result<ReceiptVoidingDto>> AnularAsync(ReceiptDto recibo)
    {
        var pedida = await new RequestReceiptVoidingCommandHandler(_k.C.E.Db, _k.C.E.Alcance, _k.C.E.Actor, _k.C.Cerrojo, _k.C.Motor, _permisos, Auditoria, _k.C.E.Reloj)
            .Handle(new RequestReceiptVoidingCommand(recibo.PublicId, "Prueba") { OperationKey = Guid.NewGuid() }, default);
        if (pedida.IsFailure) return IngenIA365ERP.Application.Common.Models.Result.Failure<ReceiptVoidingDto>(pedida.Error);
        return await new ApproveReceiptVoidingCommandHandler(_k.C.E.Db, _k.C.E.Alcance, _k.C.E.Actor, _k.C.Cerrojo, _k.C.Numerador, _k.C.Motor, _k.C.E.Personas,
                _permisos, _k.Obligaciones, _k.Cuentas, _k.C.Emision, Auditoria, _k.C.E.Reloj)
            .Handle(new ApproveReceiptVoidingCommand(pedida.Value.PublicId, null) { OperationKey = Guid.NewGuid() }, default);
    }

    [Fact]
    public async Task Cobrar_el_anticipo_deja_un_saldo_a_favor_con_su_concepto_y_la_caja_lo_muestra_con_su_medio()
    {
        await _k.AbrirAsync();
        var recibo = await ConfirmarAsync([Anticipo(_administracion, 200_000m)], [_k.Efectivo(200_000m)]);

        var saldo = await _k.C.E.Db.PayerCreditBalances.SingleAsync();
        (saldo.Origin, saldo.MiscConceptId, saldo.Amount, saldo.Balance, saldo.PersonId, saldo.OriginDocumentPublicId)
            .Should().Be((PayerCreditBalanceOrigin.ConceptAdvance, _administracion.Id, 200_000m, 200_000m, _k.Asociado.Id, recibo.PublicId));
        (await _k.C.E.Db.PayerCreditBalanceMovements.SingleAsync()).Kind.Should().Be(PayerCreditBalanceMovementKind.Recognized);

        var pagables = await _k.Pagables().Handle(new GetPayablesQuery(_k.Asociado.PublicId), default);
        var disponible = pagables.Value.Advances.Should().ContainSingle().Subject;
        (disponible.ConceptCode, disponible.Balance, disponible.MeansCode).Should().Be(("ANTADM", 200_000m, "ANT"));
        pagables.Value.MiscConcepts.Single(c => c.Code == "ANTADM").IsAdvance.Should().BeTrue();
    }

    [Fact]
    public async Task Cruzar_con_el_medio_del_anticipo_lo_baja_y_no_pasa_de_lo_disponible()
    {
        await _k.AbrirAsync();
        await ConfirmarAsync([Anticipo(_administracion, 200_000m)], [_k.Efectivo(200_000m)]);

        var demasiado = await _k.PreparadaAsync([_k.Cuota(_k.CreditoA, 250_000m)], [ConElAnticipo(250_000m)]);
        var rechazo = await _k.Confirmacion().ConfirmarAsync(demasiado.PublicId, Guid.NewGuid(), null, default);
        rechazo.Error.Code.Should().Be(AnticiposDeConcepto.CodigoInsuficiente);
        await _k.Abandonar().Handle(new AbandonCashOperationCommand(demasiado.PublicId), default);

        var cruce = await ConfirmarAsync([_k.Cuota(_k.CreditoA, 150_000m)], [ConElAnticipo(150_000m)]);

        var saldo = await _k.C.E.Db.PayerCreditBalances.SingleAsync();
        (saldo.Balance, saldo.Status).Should().Be((50_000m, PayerCreditBalanceState.Open));
        var aplicado = await _k.C.E.Db.PayerCreditBalanceMovements.SingleAsync(m => m.Kind == PayerCreditBalanceMovementKind.AppliedByReceipt);
        (aplicado.Amount, aplicado.DocumentPublicId).Should().Be((150_000m, cruce.PublicId));
    }

    [Fact]
    public async Task Anular_el_recibo_que_cruzo_devuelve_el_valor_al_anticipo_y_no_deja_un_saldo_a_favor_generico()
    {
        await _k.AbrirAsync();
        await ConfirmarAsync([Anticipo(_administracion, 200_000m)], [_k.Efectivo(200_000m)]);
        var cruce = await ConfirmarAsync([_k.Cuota(_k.CreditoA, 150_000m)], [ConElAnticipo(150_000m)]);

        var anulada = await AnularAsync(cruce);

        anulada.IsSuccess.Should().BeTrue(anulada.IsFailure ? anulada.Error.Code : null);
        var saldos = await _k.C.E.Db.PayerCreditBalances.ToListAsync();
        saldos.Should().ContainSingle("lo cruzado vuelve al anticipo, no a un saldo a favor nuevo");
        (saldos[0].Balance, saldos[0].Status).Should().Be((200_000m, PayerCreditBalanceState.Open));
        (await _k.C.E.Db.PayerCreditBalanceMovements.AnyAsync(m => m.Kind == PayerCreditBalanceMovementKind.ReopenedByVoiding && m.Amount == 150_000m))
            .Should().BeTrue();
    }

    [Fact]
    public async Task El_recibo_que_recibio_el_anticipo_no_se_anula_si_ya_se_cruzo_y_entero_se_anula_con_el()
    {
        await _k.AbrirAsync();
        var recibido = await ConfirmarAsync([Anticipo(_administracion, 200_000m)], [_k.Efectivo(200_000m)]);
        var cruce = await ConfirmarAsync([_k.Cuota(_k.CreditoA, 50_000m)], [ConElAnticipo(50_000m)]);

        (await AnularAsync(recibido)).Error.Code.Should().Be(AnticiposDeConcepto.CodigoYaCruzado);

        (await AnularAsync(cruce)).IsSuccess.Should().BeTrue();
        var entero = await AnularAsync(recibido);

        entero.IsSuccess.Should().BeTrue(entero.IsFailure ? entero.Error.Code : null);
        var saldo = await _k.C.E.Db.PayerCreditBalances.SingleAsync(s => s.Origin == PayerCreditBalanceOrigin.ConceptAdvance);
        (saldo.Balance, saldo.Status).Should().Be((0m, PayerCreditBalanceState.Resolved));
        (await _k.C.E.Db.PayerCreditBalanceMovements.AnyAsync(m => m.Kind == PayerCreditBalanceMovementKind.VoidedWithReceipt)).Should().BeTrue();
    }

    [Fact]
    public async Task El_anticipo_no_se_devuelve_como_saldo_a_favor()
    {
        await _k.AbrirAsync();
        await ConfirmarAsync([Anticipo(_administracion, 200_000m)], [_k.Efectivo(200_000m)]);
        var saldo = await _k.C.E.Db.PayerCreditBalances.SingleAsync();

        saldo.EsAnticipo.Should().BeTrue();
        var r = await new ApplyPayerCreditBalanceCommandHandler(_k.C.E.Db, _k.C.E.Alcance, _k.C.E.Actor, _k.C.Cerrojo, _k.C.Emision, Auditoria, _k.C.E.Reloj)
            .Handle(new ApplyPayerCreditBalanceCommand(saldo.PublicId, 10_000m, (await _k.C.E.Db.IssuedReceipts.FirstAsync()).PublicId)
            {
                OperationKey = Guid.NewGuid(),
            }, default);
        r.Error.Code.Should().Be(AnticiposDeConcepto.CodigoNoSeDevuelve);
    }

    [Fact]
    public async Task Las_marcas_del_concepto_se_validan_al_crearlo()
    {
        var crear = new CreateMiscConceptCommandHandler(_k.C.E.Db);
        var tercero = new Person { BusinessName = "Fondo Solidario", TaxId = "900300400" };
        _k.C.E.Db.People.Add(tercero);
        var efectivo = _k.C.Efectivo;
        await _k.C.E.Db.SaveChangesAsync();

        (await crear.Handle(new CreateMiscConceptCommand("SOLID", "Solidario", MiscConceptDirection.Income, false, false, false, false, null,
            CollectedForThirdParty: true), default)).Error.Code.Should().Be(CodigosDeConceptosVarios.MarcasIncompatibles, "sin tercero fijo");
        var solidario = await crear.Handle(new CreateMiscConceptCommand("SOLID", "Solidario", MiscConceptDirection.Income, false, false, false, false, null,
            MiscConceptThirdPartyMode.FixedThirdParty, tercero.PublicId, CollectedForThirdParty: true), default);
        solidario.IsSuccess.Should().BeTrue(solidario.IsFailure ? solidario.Error.Message : null);
        (solidario.Value.CollectedForThirdParty, solidario.Value.RequiresCrossDocument).Should().Be((true, true));

        (await crear.Handle(new CreateMiscConceptCommand("ANT2", "Anticipo", MiscConceptDirection.Income, false, false, false, false, null,
            IsAdvance: true, AdvanceMeansPublicId: efectivo.PublicId), default)).Error.Code.Should().Be(CodigosDeConceptosVarios.MarcasIncompatibles, "el efectivo no es «otro»");
        (await crear.Handle(new CreateMiscConceptCommand("ANT3", "Anticipo", MiscConceptDirection.Income, false, false, false, false, null,
            IsAdvance: true, AdvanceMeansPublicId: _medioDelAnticipo.PublicId), default)).Error.Code.Should().Be(CodigosDeConceptosVarios.MarcasIncompatibles,
            "el medio ya cruza el anticipo de otro concepto");
        var otro = _k.C.E.Medio("ANT-B", PaymentMeansClass.Other, 0, CashCountMethod.None);
        (await crear.Handle(new CreateMiscConceptCommand("ANT4", "Anticipo", MiscConceptDirection.Income, false, false, true, false, null,
            IsAdvance: true, AdvanceMeansPublicId: otro.PublicId), default)).Error.Code.Should().Be(CodigosDeConceptosVarios.MarcasIncompatibles, "un anticipo no exige factura");
        var bien = await crear.Handle(new CreateMiscConceptCommand("ANT4", "Anticipo", MiscConceptDirection.Income, false, false, false, false, null,
            IsAdvance: true, AdvanceMeansPublicId: otro.PublicId), default);
        (bien.Value.IsAdvance, bien.Value.AdvanceMeansCode).Should().Be((true, "ANT-B"));
    }
}
