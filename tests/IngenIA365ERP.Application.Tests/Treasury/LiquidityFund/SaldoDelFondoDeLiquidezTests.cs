using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Application.Treasury.Liquidity;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Treasury.LiquidityFund;

/// <summary>
/// Feature 015, T2, fase 43 (T0458; FR-139; E-02 e): la foto de la posición guarda la posición del día y, si hay cuentas marcadas como fondo de liquidez, emite en el mismo
/// guardado <c>SaldoDelFondoDeLiquidez</c> v1 (<c>TES → Lending</c>) con esas cuentas y su saldo, originado por la persona que la toma; sin cuentas del fondo no emite; una
/// foto de un día futuro se rechaza. Prueba escrita, sin correr.
/// </summary>
public class SaldoDelFondoDeLiquidezTests
{
    private readonly EscenarioDeTesoreria _t = new();

    public SaldoDelFondoDeLiquidezTests()
    {
        _t.E.Como(_t.E.Cajera);
        _t.Contabilidad.SaldoEnLibrosAsync(_t.Pagos.PublicId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(10_000_000m);
        _t.Contabilidad.SaldoEnLibrosAsync(_t.FondoDeLiquidez.PublicId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(5_000_000m);
    }

    private TakeLiquiditySnapshotCommandHandler Foto() => new(_t.E.Db,
        new PosicionDeLiquidez(_t.E.Db, _t.Contabilidad, NullLogger<PosicionDeLiquidez>.Instance), _t.E.Actor, _t.Numerador, _t.Emision,
        new AuditoriaDeTesoreria(_t.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance), _t.E.Reloj);

    [Fact]
    public async Task La_foto_informa_a_Captaciones_el_saldo_de_las_cuentas_del_fondo()
    {
        var r = await Foto().Handle(new TakeLiquiditySnapshotCommand { OperationKey = Guid.NewGuid() }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        r.Value.LiquidityFund.Should().Be(5_000_000m);
        r.Value.Number.Should().StartWith(TakeLiquiditySnapshotCommandHandler.CodigoDeFoto);
        var mensaje = await _t.E.Db.IntegrationMessages.SingleAsync(m => m.Type == SaldoDelFondoDeLiquidezV1.Type);
        r.Value.FundMessagePublicId.Should().Be(mensaje.PublicId);
        (await _t.E.Db.TreasuryLiquiditySnapshots.SingleAsync()).Date.Should().Be(EscenarioDeRecaudo.Hoy);
    }

    [Fact]
    public async Task Sin_cuentas_del_fondo_no_se_emite_nada()
    {
        var fondo = await _t.E.Db.OwnBankAccounts.SingleAsync(c => c.Id == _t.FondoDeLiquidez.Id);
        fondo.Purposes = BankAccountPurpose.Payments;
        await _t.E.Db.SaveChangesAsync();

        var r = await Foto().Handle(new TakeLiquiditySnapshotCommand { OperationKey = Guid.NewGuid() }, default);

        r.Value.FundMessagePublicId.Should().BeNull();
        (await _t.E.Db.IntegrationMessages.AnyAsync(m => m.Type == SaldoDelFondoDeLiquidezV1.Type)).Should().BeFalse();
    }

    [Fact]
    public async Task Una_foto_de_un_dia_futuro_se_rechaza()
    {
        var r = await Foto().Handle(new TakeLiquiditySnapshotCommand(EscenarioDeRecaudo.Hoy.AddDays(1)) { OperationKey = Guid.NewGuid() }, default);
        r.Error.Code.Should().Be("Treasury.Liquidity.FutureSnapshot");
    }
}
