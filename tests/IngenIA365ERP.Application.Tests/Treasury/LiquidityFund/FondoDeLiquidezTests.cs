using FluentAssertions;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.LiquidityFund;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Treasury.BankAccounts;
using IngenIA365ERP.Domain.Enums.Treasury;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Treasury.LiquidityFund;

/// <summary>
/// Feature 015, US10, T0128 (FR-207; SC-028): ningún lote, remesa ni traslado sale de una cuenta del fondo sin un uso aprobado por el monto; el uso lleva causal,
/// soporte, aprobación por otra persona y la alerta del aviso previo a la Supersolidaria; una cuenta que no es del fondo pasa sin más (los abonos no se restringen).
/// Prueba escrita, sin correr.
/// </summary>
public class FondoDeLiquidezTests
{
    private readonly EscenarioDeTesoreria _t = new();

    private RequestLiquidityFundUseCommandHandler Pedir() => new(_t.E.Db, _t.Alcance, _t.E.Actor, _t.Cerrojo, _t.Numerador,
        new AprobacionDiferidaEnTesoreria(_t.E.Db, _t.Motor, _t.E.Actor), _t.E.Alertas, _t.E.Reloj);

    private LiquidityFundUse Uso(LiquidityFundUseState estado, decimal monto)
    {
        var uso = new LiquidityFundUse
        {
            Number = $"UF-{_t.E.Db.LiquidityFundUses.Count() + 1}", BankAccountId = _t.FondoDeLiquidez.Id, Cause = LiquidityFundCause.DepositorAttention, Amount = monto,
            Reason = "retiros masivos", SupportAttachmentPublicId = Guid.NewGuid(), Status = estado, RequestedByUserId = _t.E.Cajera.Id,
        };
        _t.E.Db.LiquidityFundUses.Add(uso);
        _t.E.Db.SaveChanges();
        return uso;
    }

    [Fact]
    public async Task Sin_uso_aprobado_por_el_monto_no_sale_nada_del_fondo()
    {
        Uso(LiquidityFundUseState.Approved, 1_000_000m);
        Uso(LiquidityFundUseState.PendingApproval, 9_000_000m);
        var r = await _t.Fondo.ExigirAsync(_t.FondoDeLiquidez, 5_000_000m, null, default);
        r.Error.Code.Should().Be("Treasury.LiquidityFund.UseRequired");
        (await _t.Fondo.ExigirAsync(_t.FondoDeLiquidez, 800_000m, null, default)).Value.Should().NotBeNull();
    }

    [Fact]
    public async Task Una_cuenta_que_no_es_del_fondo_no_se_restringe() =>
        (await _t.Fondo.ExigirAsync(_t.Pagos, 50_000_000m, null, default)).IsSuccess.Should().BeTrue();

    [Fact]
    public async Task El_uso_se_pide_con_aprobacion_y_alerta_el_aviso_previo()
    {
        var r = await Pedir().Handle(new RequestLiquidityFundUseCommand(_t.FondoDeLiquidez.PublicId, LiquidityFundCause.DepositReduction, 3_000_000m, "reducción",
            Guid.NewGuid()) { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Status.Should().Be(LiquidityFundUseState.PendingApproval);
        await _t.Motor.Received(1).SolicitarAsync(Arg.Is<IngenIA365ERP.Application.Common.Approvals.SolicitudDeAprobacion>(s => s.Subject == ApprovalSubjects.LiquidityFundUse
            && s.PermisoLimitado == "Treasury.LiquidityFund.Approve" && s.Amount == 3_000_000m), Arg.Any<CancellationToken>());
        await _t.E.Alertas.Received(1).LevantarAsync(Arg.Is<AlertaALevantar>(a => a.TypeCode == TiposDeAlerta.TesoreriaUsoDelFondoDeLiquidez), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Solo_se_pide_el_uso_de_una_cuenta_del_fondo()
    {
        var r = await Pedir().Handle(new RequestLiquidityFundUseCommand(_t.Pagos.PublicId, LiquidityFundCause.DepositReduction, 1m, "x", Guid.NewGuid())
            { OperationKey = Guid.NewGuid() }, default);
        r.Error.Code.Should().Be("Treasury.LiquidityFund.NotLiquidityFundAccount");
    }

    [Fact]
    public async Task La_aprobacion_completa_deja_el_uso_aprobado_y_la_devolucion_rechazado()
    {
        var uso = Uso(LiquidityFundUseState.PendingApproval, 2_000_000m);
        var aprobable = new AprobableUsoDelFondo(_t.E.Db, _t.E.Reloj);
        (await aprobable.AlAprobarAsync(new ApprovalRequest { SourcePublicId = uso.PublicId }, default)).IsSuccess.Should().BeTrue();
        uso.Status.Should().Be(LiquidityFundUseState.Approved);
        (await aprobable.AlDevolverAsync(new ApprovalRequest { SourcePublicId = uso.PublicId }, "no", default)).Error.Code.Should().Be("Treasury.LiquidityFund.NotPending");
    }

    [Fact]
    public void Quien_pidio_el_uso_no_lo_aprueba() =>
        ErroresDeTesoreria.UsoDelFondoAutoaprobado().Code.Should().Be("Treasury.LiquidityFund.SelfApproval");
}
