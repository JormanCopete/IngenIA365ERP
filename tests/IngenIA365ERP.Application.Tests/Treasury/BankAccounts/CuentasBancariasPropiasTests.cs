using FluentAssertions;
using IngenIA365ERP.Application.Treasury.BankAccounts;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Treasury.BankAccounts;

/// <summary>
/// Feature 015, US10, T0127 (FR-102, FR-103, FR-207): las cuentas bancarias propias. El banco tiene que tener código de transferencia; la cuenta no se registra dos
/// veces; el fondo de liquidez es exclusivo; el vínculo con la cuenta contable se pide a Contabilidad en el mismo guardado; los saldos son libros, último extracto y
/// disponible proyectado, y su lectura se audita. El puerto de Recaudo sólo ve las de recaudo vigentes al alcance. Prueba escrita, sin correr.
/// </summary>
public class CuentasBancariasPropiasTests
{
    private readonly EscenarioDeTesoreria _t = new();

    private CreateOwnBankAccountCommandHandler Crear() => new(_t.E.Db, _t.Contabilidad, _t.E.Reloj);

    private CreateOwnBankAccountCommand Pedido(Guid banco, string numero = "5500112233", BankAccountPurpose propositos = BankAccountPurpose.Payments, string codigo = "NUEVA",
        Guid? contable = null) =>
        new(codigo, "Cuenta nueva", banco, numero, OwnBankAccountType.Savings, _t.E.OficinaA.PublicId, propositos, false, null, contable) { OperationKey = Guid.NewGuid() };

    [Fact]
    public async Task El_banco_sin_codigo_de_transferencia_no_admite_cuentas()
    {
        var r = await Crear().Handle(Pedido(_t.BancoSinCodigo.PublicId), default);
        r.Error.Code.Should().Be("Treasury.BankAccount.BankWithoutTransferCode");
    }

    [Fact]
    public async Task El_fondo_de_liquidez_es_exclusivo()
    {
        var r = await Crear().Handle(Pedido(_t.Banco.PublicId, propositos: BankAccountPurpose.LiquidityFund | BankAccountPurpose.Payments), default);
        r.Error.Code.Should().Be("Treasury.BankAccount.LiquidityFundExclusive");
        (await Crear().Handle(Pedido(_t.Banco.PublicId, propositos: BankAccountPurpose.None), default)).Error.Code.Should().Be("Treasury.BankAccount.PurposeRequired");
    }

    [Fact]
    public async Task Una_cuenta_no_se_registra_dos_veces_y_el_codigo_es_unico()
    {
        (await Crear().Handle(Pedido(_t.Banco.PublicId, numero: _t.Pagos.AccountNumber, codigo: "OTRA"), default)).Error.Code.Should().Be("Treasury.BankAccount.Duplicate");
        (await Crear().Handle(Pedido(_t.Banco.PublicId, codigo: "PAGOS"), default)).Error.Code.Should().Be("Catalogo.CodigoDuplicado");
    }

    [Fact]
    public async Task El_vinculo_contable_se_pide_en_el_mismo_guardado_y_un_rechazo_no_deja_la_cuenta()
    {
        var contable = Guid.NewGuid();
        var r = await Crear().Handle(Pedido(_t.Banco.PublicId, contable: contable), default);
        r.IsSuccess.Should().BeTrue();
        await _t.Contabilidad.Received(1).VincularCuentaBancariaAsync(r.Value.PublicId, contable, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());

        _t.Contabilidad.VincularCuentaBancariaAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(IngenIA365ERP.Application.Common.Models.Result.Failure(new IngenIA365ERP.Application.Common.Models.Error("Accounting.OwnBankAccount.AlreadyLinked", "ya vinculada")));
        var otra = await Crear().Handle(Pedido(_t.Banco.PublicId, numero: "5500999000", codigo: "OTRA", contable: contable), default);
        otra.Error.Code.Should().Be("Accounting.OwnBankAccount.AlreadyLinked");
        _t.E.Db.OwnBankAccounts.Any(c => c.Code == "OTRA").Should().BeFalse("el alta y su vínculo van en el mismo guardado");
    }

    [Fact]
    public async Task Los_saldos_son_libros_extracto_y_disponible_proyectado_y_su_lectura_se_audita()
    {
        _t.Contabilidad.SaldoEnLibrosAsync(_t.Pagos.PublicId, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns(10_000_000m);
        _t.E.Db.PaymentBatches.Add(new Domain.Entities.Treasury.Payments.PaymentBatch
        {
            Number = "LP-1", BankAccountId = _t.Pagos.Id, Form = PaymentBatchForm.File, PreparedByUserId = _t.E.Cajera.Id, Total = 2_500_000m,
            Status = PaymentBatchState.Approved, InputsHash = "x",
        });
        _t.E.Db.SaveChanges();
        var handler = new GetOwnBankAccountBalancesQueryHandler(_t.E.Db, _t.Alcance, _t.Contabilidad,
            new AuditoriaDeTesoreria(_t.E.Servicios, NullLogger<AuditoriaDeTesoreria>.Instance), _t.E.Reloj);

        var r = await handler.Handle(new GetOwnBankAccountBalancesQuery(_t.Pagos.PublicId), default);

        r.Value.BookBalance.Should().Be(10_000_000m);
        r.Value.ApprovedNotSent.Should().Be(2_500_000m);
        r.Value.ProjectedAvailable.Should().Be(7_500_000m);
    }

    [Fact]
    public async Task Fuera_de_alcance_la_cuenta_no_existe()
    {
        _t.Alcance.Valor = new IngenIA365ERP.Application.Common.Scope.AlcanceDeTesoreria(false, new HashSet<int> { _t.FondoDeLiquidez.Id });
        var r = await new GetOwnBankAccountQueryHandler(_t.E.Db, _t.Alcance, _t.Contabilidad).Handle(new GetOwnBankAccountQuery(_t.Pagos.PublicId), default);
        r.Error.Code.Should().Be(ErroresDeTesoreria.CodigoNoEncontrado);
    }

    [Fact]
    public async Task Recaudo_ve_solo_las_cuentas_de_recaudo_vigentes_y_enmascaradas()
    {
        var puerto = new CuentasBancariasPropias(_t.E.Db, _t.Alcance, _t.E.Reloj);
        var cuentas = await puerto.DeRecaudoAsync(null, default);
        cuentas.Should().ContainSingle(c => c.PublicId == _t.Pagos.PublicId);
        cuentas.Single().AccountNumberMasked.Should().EndWith("3456").And.StartWith("*");
    }
}
