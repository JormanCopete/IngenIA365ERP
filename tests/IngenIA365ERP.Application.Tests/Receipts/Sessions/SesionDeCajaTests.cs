using FluentAssertions;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Sessions;

/// <summary>
/// Feature 015, T0080 (FR-022, FR-029; US2-1, US2-2): abrir la sesión con la base contada por denominaciones; la caja ocupada dice quién la tiene
/// (<c>Receipts.Session.AlreadyOpen</c>); con <c>Caja.UnaSesionPorCajero</c> el cajero no abre una segunda (<c>.CashierExclusive</c>); los valores sellados no
/// cambian aunque cambie la caja; sin sesión, <c>Receipts.Session.Required</c>. Escrita en la fase 5; se corre al final del plan.
/// </summary>
public class SesionDeCajaTests
{
    private readonly CajaDePrueba _c = new();

    [Fact]
    public async Task Abre_con_la_base_por_denominaciones_y_deja_el_movimiento_de_apertura_numerado()
    {
        _c.E.Como(_c.E.Cajera);
        var r = await _c.Abrir().Handle(new OpenCashSessionCommand(_c.C1.PublicId, CajaDePrueba.Billetes((_c.B50, 8), (_c.B20, 5))), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.OpeningBase.Should().Be(500_000);
        r.Value.Status.Should().Be(CashSessionState.Open);
        r.Value.Means.Select(m => m.Code).Should().BeEquivalentTo(["EF", "TR"]);
        var apertura = await _c.E.Db.CashMovements.SingleAsync();
        apertura.Kind.Should().Be(CashMovementKindR.Opening);
        apertura.Number.Should().Be("000001");
        (await _c.E.Db.CashMovementDenominations.SumAsync(d => d.Amount)).Should().Be(500_000);
        (await _c.E.Db.CollectionPointDays.SingleAsync()).BusinessDate.Should().Be(EscenarioDeRecaudo.Hoy);
    }

    [Fact]
    public async Task La_caja_ocupada_dice_quien_tiene_la_sesion()
    {
        await _c.AbrirAsync();
        _c.E.AsignarCajero(_c.C1, _c.E.OtroCajero);
        _c.E.Como(_c.E.OtroCajero);

        var r = await _c.Abrir().Handle(new OpenCashSessionCommand(_c.C1.PublicId, CajaDePrueba.Billetes((_c.B50, 10))), default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoSesionYaAbierta);
        r.Error.Message.Should().Contain("carla");
    }

    [Fact]
    public async Task Con_una_sesion_por_cajero_no_abre_una_segunda_caja()
    {
        await _c.AbrirAsync();
        _c.E.AsignarCajero(_c.C2, _c.E.Cajera);

        var r = await _c.Abrir().Handle(new OpenCashSessionCommand(_c.C2.PublicId, CajaDePrueba.Billetes((_c.B50, 10))), default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoCajeroExclusivo);
    }

    [Fact]
    public async Task Sin_una_sesion_por_cajero_abre_las_dos()
    {
        _c.E.Parametro(ParametrosDeRecaudoYTesoreria.CajaUnaSesionPorCajero, "false");
        await _c.AbrirAsync();
        _c.E.AsignarCajero(_c.C2, _c.E.Cajera);

        var r = await _c.Abrir().Handle(new OpenCashSessionCommand(_c.C2.PublicId, CajaDePrueba.Billetes((_c.B50, 10))), default);

        r.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task No_abre_quien_no_es_cajero_vigente_de_la_caja()
    {
        _c.E.Como(_c.E.Supervisor);
        var r = await _c.Abrir().Handle(new OpenCashSessionCommand(_c.C1.PublicId, CajaDePrueba.Billetes((_c.B50, 10))), default);
        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoNoEsCajeroDeLaCaja);
    }

    [Fact]
    public async Task Los_valores_sellados_no_cambian_aunque_cambie_la_caja()
    {
        var sesion = await _c.AbrirAsync();
        _c.C1.CashBlockCap = 100;
        _c.C1.CashWarnCap = 50;
        _c.E.Parametro(ParametrosDeRecaudoYTesoreria.CajaArqueoCiego, "false");
        await _c.E.Db.SaveChangesAsync();

        var guardada = await _c.E.Db.ReceiptsCashSessions.SingleAsync(s => s.PublicId == sesion.PublicId);
        var sellados = ValoresSelladosDeLaSesion.De(guardada);
        sellados.CashBlockCap.Should().Be(3_000_000);
        sellados.CashWarnCap.Should().Be(2_000_000);
        sellados.BlindCount.Should().BeTrue("el arqueo ciego se selló al abrir");
        sellados.Means.Should().HaveCount(2);
    }

    [Fact]
    public async Task Con_fondo_fijo_la_base_tiene_que_ser_el_fondo()
    {
        _c.C1.BaseMode = CashBoxBaseMode.FixedFund;
        await _c.E.Db.SaveChangesAsync();
        _c.E.Como(_c.E.Cajera);

        var r = await _c.Abrir().Handle(new OpenCashSessionCommand(_c.C1.PublicId, CajaDePrueba.Billetes((_c.B50, 9))), default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoBaseDistintaDelFondo);
    }

    [Fact]
    public async Task Sin_sesion_la_cajera_no_tiene_sesion_actual_y_el_error_es_Session_Required()
    {
        _c.E.Como(_c.E.Cajera);
        var actual = await _c.Actual().Handle(new GetCurrentSessionQuery(), default);
        actual.Value.Should().BeNull();
        ErroresDeRecaudo.SesionRequerida().Code.Should().Be("Receipts.Session.Required");
    }

    [Fact]
    public async Task Con_arqueo_ciego_lo_esperado_no_se_ve_hasta_registrar_lo_contado()
    {
        var sesion = await _c.AbrirAsync();
        var oculto = await _c.Esperado().Handle(new GetSessionExpectedQuery(sesion.PublicId), default);
        oculto.Value.Hidden.Should().BeTrue();
        oculto.Value.Total.Should().BeNull();

        _c.E.Parametro(ParametrosDeRecaudoYTesoreria.CajaArqueoCiego, "false");
        var otra = await _c.AbrirAsync(_c.C2, _c.E.OtroCajero);
        var visible = await _c.Esperado().Handle(new GetSessionExpectedQuery(otra.PublicId), default);
        visible.Value.Hidden.Should().BeFalse();
        visible.Value.Total.Should().Be(500_000);
    }
}
