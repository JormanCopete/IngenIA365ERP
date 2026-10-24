using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Receipts.Sessions;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Sessions;

/// <summary>
/// Feature 015, T0085 (FR-028, FR-181, FR-183): lo esperado avisa al llegar al tope de aviso (traslado a bóveda); el tope de bloqueo se sella en la sesión y
/// con él la caja rechaza recibos en efectivo (<c>Receipts.Session.CashBlockCapReached</c>, que aplica la operación de caja de la fase 6); descontar un faltante
/// por Nómina exige la autorización escrita del caso adjunta; el sobrante pasa a ingreso sólo desde su fecha y con otra persona (<c>SobranteLlevadoAIngreso</c>).
/// Escrita en la fase 5; se corre al final del plan.
/// </summary>
public class TopesFaltantesYSobrantesTests
{
    private readonly CajaDePrueba _c = new();

    [Fact]
    public async Task Lo_esperado_avisa_al_llegar_al_tope_de_aviso()
    {
        _c.E.Parametro(ParametrosDeRecaudoYTesoreria.CajaArqueoCiego, "false");
        _c.C1.CashWarnCap = 500_000;
        await _c.E.Db.SaveChangesAsync();
        var sesion = await _c.AbrirAsync();

        var esperado = await _c.Esperado().Handle(new GetSessionExpectedQuery(sesion.PublicId), default);

        esperado.Value.CashWarnCapReached.Should().BeTrue();
    }

    [Fact]
    public async Task El_tope_de_bloqueo_queda_sellado_y_su_error_dice_el_tope()
    {
        var sesion = await _c.AbrirAsync();
        var guardada = await _c.E.Db.ReceiptsCashSessions.SingleAsync(s => s.PublicId == sesion.PublicId);
        ValoresSelladosDeLaSesion.De(guardada).CashBlockCap.Should().Be(3_000_000);
        var error = ErroresDeRecaudo.TopeDeBloqueo(3_000_000);
        error.Code.Should().Be("Receipts.Session.CashBlockCapReached");
        error.Message.Should().Contain("bóveda");
    }

    [Fact]
    public async Task Descontar_un_faltante_por_Nomina_exige_la_autorizacion_del_caso()
    {
        var sesion = await _c.AbrirAsync();
        await _c.Contar().Handle(new RegisterClosingCountCommand(sesion.PublicId, [_c.EnBilletes("Moneda", (_c.B50, 9), (_c.B20, 2), (_c.B1, 9))]), default);
        var faltante = await _c.E.Db.CashierReceivables.SingleAsync();
        _c.E.Como(_c.E.Supervisor);

        var sinAdjunto = await _c.Autorizar().Handle(new AttachPayrollDeductionAuthorizationCommand(faltante.PublicId, Guid.NewGuid()), default);
        sinAdjunto.Error.Code.Should().Be(ErroresDeRecaudo.CodigoAutorizacionDeDescuento);

        var adjunto = new Attachment
        {
            OwnerEntityType = AdjuntosDeModulo.SoporteDeCaja, OwnerEntityPublicId = sesion.PublicId, FileName = "autorizacion.pdf",
            ContentType = "application/pdf", SizeBytes = 10, Sha256Hex = "x", StoragePath = "x",
        };
        _c.E.Db.Attachments.Add(adjunto);
        await _c.E.Db.SaveChangesAsync();
        var r = await _c.Autorizar().Handle(new AttachPayrollDeductionAuthorizationCommand(faltante.PublicId, adjunto.PublicId), default);

        r.Value.HasPayrollAuthorization.Should().BeTrue();
    }

    [Fact]
    public async Task El_sobrante_pasa_a_ingreso_solo_desde_su_fecha_y_por_otra_persona()
    {
        _c.E.Parametro(ParametrosDeRecaudoYTesoreria.CajaSobrantesDiasAntesDeIngreso, "0");
        var sesion = await _c.AbrirAsync();
        await _c.Contar().Handle(new RegisterClosingCountCommand(sesion.PublicId, [_c.EnBilletes("Sobró", (_c.B50, 10), (_c.B1, 1))]), default);
        var sobrante = await _c.E.Db.CashSurpluses.SingleAsync();

        _c.E.Como(_c.E.CajeraBis);
        (await _c.SobranteAIngreso().Handle(new MoveSurplusToIncomeCommand(sobrante.PublicId), default)).Error.Code
            .Should().Be(ErroresDeRecaudo.CodigoAutoaprobacionDeArqueo);

        _c.E.Como(_c.E.Supervisor);
        var r = await _c.SobranteAIngreso().Handle(new MoveSurplusToIncomeCommand(sobrante.PublicId), default);

        r.Value.Status.Should().Be(CashSurplusState.MovedToIncome);
        (await _c.E.Db.IntegrationMessages.AnyAsync(m => m.Type == SobranteLlevadoAIngresoV1.Type)).Should().BeTrue();
        (await _c.SobranteAIngreso().Handle(new MoveSurplusToIncomeCommand(sobrante.PublicId), default)).Error.Code
            .Should().Be(ErroresDeRecaudo.CodigoSobranteNoEsPasivo);
    }

    [Fact]
    public async Task Sin_dias_fijados_el_sobrante_sigue_como_pasivo()
    {
        var sesion = await _c.AbrirAsync();
        await _c.Contar().Handle(new RegisterClosingCountCommand(sesion.PublicId, [_c.EnBilletes("Sobró", (_c.B50, 10), (_c.B1, 1))]), default);
        var sobrante = await _c.E.Db.CashSurpluses.SingleAsync();
        sobrante.ToIncomeFrom.Should().BeNull();
        _c.E.Como(_c.E.Supervisor);

        var r = await _c.SobranteAIngreso().Handle(new MoveSurplusToIncomeCommand(sobrante.PublicId), default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoSobranteNoVencido);
    }
}
