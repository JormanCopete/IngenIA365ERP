using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Cash;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Sessions;

/// <summary>
/// 2026-11-25 (revisión de la caja única, pedido del dueño: «simple y seguro»). El coordinador comprueba el arqueo antes de tocar la caja del punto de venta,
/// entra a Recaudo lo que ésta contó al cerrar y no lo vuelve a pedir, rechaza un medio en negativo antes de cerrar y no abre una segunda caja del punto de
/// venta en la misma sesión; la comprobación suma lo del punto de venta sin escribir nada.
/// </summary>
public class CajaUnicaTests
{
    private readonly CajaDePrueba _c = new(estricto: false);
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly Guid _pos = Guid.NewGuid();

    private CajaUnica Coordinador() => new(_c.E.Db, _sender, _c.E.Parametros, _c.E.Reloj, _c.E.Alcance, _c.E.Actor, _c.E.Segregacion);

    private SesionVinculadaDto Vinculada(bool abierta, decimal efectivo) =>
        new(_pos, "CJ1", "ALMACEN", abierta, [new LoDelPuntoDeVentaDto(_c.Efectivo.Id, _c.Efectivo.PublicId, "EF", PaymentMeansClass.Cash,
            CashCountMethod.PhysicalCount, efectivo)]);

    private void Leidas(params SesionVinculadaDto?[] sucesivas)
    {
        var resultados = sucesivas.Select(s => Task.FromResult(Result.Success(s))).ToArray();
        _sender.Send(Arg.Any<GetLinkedCashSessionQuery>(), Arg.Any<CancellationToken>()).Returns(resultados[0], resultados[1..]);
    }

    private void Respuestas()
    {
        _sender.Send(Arg.Any<CloseCashSessionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success<CloseCashSessionResultDto>(null!)));
        _sender.Send(Arg.Any<RegisterPointOfSaleIntakeCommand>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(Result.Success()));
    }

    [Fact]
    public async Task Si_el_arqueo_se_va_a_rechazar_la_caja_del_punto_de_venta_sigue_abierta()
    {
        var sesion = await _c.AbrirAsync();
        Leidas(Vinculada(abierta: true, 50_000m));
        Respuestas();
        IReadOnlyDictionary<int, decimal>? comprobado = null;

        var r = await Coordinador().PrepararCierreAsync(sesion.PublicId, Guid.NewGuid(), p =>
        {
            comprobado = p;
            return Task.FromResult(Result.Failure(ErroresDeRecaudo.SinMotivo(["EF"])));
        }, default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoSinMotivo);
        comprobado.Should().ContainKey(_c.Efectivo.Id).WhoseValue.Should().Be(50_000m, "la comprobación cuenta lo que el punto de venta lleva recibido");
        await _sender.DidNotReceive().Send(Arg.Any<CloseCashSessionCommand>(), Arg.Any<CancellationToken>());
        await _sender.DidNotReceive().Send(Arg.Any<RegisterPointOfSaleIntakeCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cerrada_la_caja_del_punto_de_venta_entra_a_Recaudo_lo_que_conto_su_cierre()
    {
        var sesion = await _c.AbrirAsync();
        // Abierta con 50.000 en vivo; después del cierre su esperado vivo sería cero, y la consulta da lo contado.
        Leidas(Vinculada(abierta: true, 50_000m), Vinculada(abierta: false, 50_000m));
        Respuestas();

        var r = await Coordinador().PrepararCierreAsync(sesion.PublicId, Guid.NewGuid(), _ => Task.FromResult(Result.Success()), default);

        r.IsSuccess.Should().BeTrue();
        await _sender.Received(1).Send(Arg.Is<CloseCashSessionCommand>(c => c.DesdeLaCajaUnica && c.CashSessionPublicId == _pos), Arg.Any<CancellationToken>());
        await _sender.Received(1).Send(Arg.Is<RegisterPointOfSaleIntakeCommand>(i => i.PosSessionPublicId == _pos
            && i.Means.Single().Expected == 50_000m), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Si_lo_del_punto_de_venta_ya_entro_repetir_el_arqueo_no_vuelve_a_pedir_nada()
    {
        var sesion = await _c.AbrirAsync();
        var ingreso = await new RegisterPointOfSaleIntakeCommandHandler(_c.E.Db, _c.E.Alcance, _c.E.Actor, _c.E.Reloj, _c.Cerrojo, _c.Numerador, _c.Emision)
            .Handle(new RegisterPointOfSaleIntakeCommand(sesion.PublicId, _pos, Vinculada(abierta: false, 50_000m).Means), default);
        ingreso.IsSuccess.Should().BeTrue(ingreso.IsFailure ? $"{ingreso.Error.Code}: {ingreso.Error.Message}" : null);
        Leidas(Vinculada(abierta: false, 50_000m));
        Respuestas();
        var comprobaciones = 0;

        var r = await Coordinador().PrepararCierreAsync(sesion.PublicId, Guid.NewGuid(), _ =>
        {
            comprobaciones++;
            return Task.FromResult(Result.Success());
        }, default);

        r.IsSuccess.Should().BeTrue();
        comprobaciones.Should().Be(0, "ya está en lo esperado de Recaudo: el arqueo lo comprueba él mismo");
        await _sender.DidNotReceive().Send(Arg.Any<RegisterPointOfSaleIntakeCommand>(), Arg.Any<CancellationToken>());
        var esperado = await IngenIA365ERP.Application.Receipts.Sessions.SesionesDeCaja.EfectivoAsync(_c.E.Db, _c.E.Db.ReceiptsCashSessions.Single(s => s.PublicId == sesion.PublicId), default);
        esperado.Should().Be(550_000m, "la base de 500.000 más los 50.000 del punto de venta");
    }

    [Fact]
    public async Task Un_medio_que_quedo_en_negativo_en_el_punto_de_venta_se_rechaza_antes_de_cerrar()
    {
        var sesion = await _c.AbrirAsync();
        Leidas(Vinculada(abierta: true, -20_000m));
        Respuestas();

        var r = await Coordinador().PrepararCierreAsync(sesion.PublicId, Guid.NewGuid(), _ => Task.FromResult(Result.Success()), default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoPuntoDeVentaEnNegativo);
        await _sender.DidNotReceive().Send(Arg.Any<CloseCashSessionCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Cerrada_con_el_arqueo_no_se_abre_otra_caja_del_punto_de_venta_en_la_misma_sesion()
    {
        _c.E.Parametro(ParametrosDeRecaudoYTesoreria.CajaSesionUnica, "true");
        _c.C1.PosCashRegisterPublicId = Guid.NewGuid();
        _c.E.Db.SaveChanges();
        var sesion = await _c.AbrirAsync();
        Leidas(Vinculada(abierta: false, 50_000m));

        var r = await Coordinador().AbrirAsync(sesion.PublicId, Guid.NewGuid(), default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoPuntoDeVentaYaCerrado);
        await _sender.DidNotReceive().Send(Arg.Any<IngenIA365ERP.Application.Inventory.Cash.OpenCashSessionCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Otro_cajero_no_abre_la_caja_del_punto_de_venta_de_una_sesion_ajena()
    {
        _c.E.Parametro(ParametrosDeRecaudoYTesoreria.CajaSesionUnica, "true");
        _c.C1.PosCashRegisterPublicId = Guid.NewGuid();
        _c.E.Db.SaveChanges();
        var sesion = await _c.AbrirAsync();
        Leidas([null]);
        _c.E.Como(_c.E.OtroCajero);

        var r = await Coordinador().AbrirAsync(sesion.PublicId, Guid.NewGuid(), default);

        r.IsFailure.Should().BeTrue();
        await _sender.DidNotReceive().Send(Arg.Any<IngenIA365ERP.Application.Inventory.Cash.OpenCashSessionCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task La_comprobacion_suma_lo_del_punto_de_venta_y_no_escribe_nada()
    {
        var estricta = new CajaDePrueba(estricto: true);
        var sesion = await estricta.AbrirAsync();
        var comprobar = new CheckClosingCountQueryHandler(estricta.E.Db, estricta.E.Alcance, estricta.E.Actor, estricta.E.Reloj, estricta.Arqueos,
            estricta.E.Parametros, estricta.E.Segregacion);
        var delPuntoDeVenta = new Dictionary<int, decimal> { [estricta.Efectivo.Id] = 50_000m };

        var cuadrado = await comprobar.Handle(new CheckClosingCountQuery(sesion.PublicId,
            [new ConteoDeMedioDto(estricta.Efectivo.PublicId, 550_000m)], false, null, delPuntoDeVenta), default);
        var sinLoDelPuntoDeVenta = await comprobar.Handle(new CheckClosingCountQuery(sesion.PublicId,
            [new ConteoDeMedioDto(estricta.Efectivo.PublicId, 550_000m)], false, null, new Dictionary<int, decimal>()), default);

        cuadrado.IsSuccess.Should().BeTrue(cuadrado.IsFailure ? $"{cuadrado.Error.Code}: {cuadrado.Error.Message}" : null);
        sinLoDelPuntoDeVenta.Error.Code.Should().Be(ErroresDeRecaudo.CodigoSinMotivo, "sin lo del punto de venta, los 50.000 son un sobrante sin motivo");
        estricta.E.Db.ReceiptsCashCounts.Should().BeEmpty();
        estricta.E.Db.ReceiptsCashSessions.Single().Status.Should().Be(CashSessionState.Open);
    }
}
