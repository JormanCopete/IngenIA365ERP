using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Treasury;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Core;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// Feature 022, T049 (FR-046, FR-047, Q16): las cuentas propias de las transferencias y consignaciones de una venta. Sin cuentas habilitadas el
/// pago sigue como antes; con una sola, se propone; con varias, se pide; una no habilitada se rechaza. La habilitación exige medio de clase 6 o
/// 7, ofrecido en el punto, y una cuenta propia activa y de recaudo (por el puerto de Tesorería).
/// </summary>
public class CuentasPropiasDeVentaTests
{
    private static readonly Guid CuentaA = Guid.NewGuid();
    private static readonly Guid CuentaB = Guid.NewGuid();

    private static CuentaPropiaElegible Opcion(int medio, Guid cuenta, string rotulo = "BAN-01") => new(medio, Guid.NewGuid(), cuenta, rotulo);

    // ------------------------------------------------------------------------------------------- resolución --

    [Fact]
    public void Sin_cuentas_habilitadas_para_el_medio_el_pago_sigue_sin_cuenta()
    {
        var r = CuentasPropiasDeVenta.Resolver(7, "TRANSF", PaymentMeansClass.Transfer, null, [Opcion(8, CuentaA)], 0);

        r.IsSuccess.Should().BeTrue();
        r.Value.Should().BeNull("sin habilitadas contabiliza por el medio, como antes");
    }

    [Fact]
    public void Con_una_sola_cuenta_habilitada_se_propone()
    {
        var r = CuentasPropiasDeVenta.Resolver(7, "TRANSF", PaymentMeansClass.Transfer, null, [Opcion(7, CuentaA)], 0);

        r.Value!.OwnBankAccountPublicId.Should().Be(CuentaA);
    }

    [Fact]
    public void Con_varias_y_sin_eleccion_la_pide_con_las_opciones()
    {
        var r = CuentasPropiasDeVenta.Resolver(6, "CONSIG", PaymentMeansClass.BankDeposit, null, [Opcion(6, CuentaA), Opcion(6, CuentaB)], 2);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(CuentasPropiasDeVenta.CodigoRequerida);
    }

    [Fact]
    public void La_elegida_tiene_que_estar_habilitada_para_ese_medio()
    {
        var opciones = new[] { Opcion(7, CuentaA), Opcion(6, CuentaB) };

        CuentasPropiasDeVenta.Resolver(7, "TRANSF", PaymentMeansClass.Transfer, CuentaA, opciones, 0).Value!.OwnBankAccountPublicId.Should().Be(CuentaA);
        CuentasPropiasDeVenta.Resolver(7, "TRANSF", PaymentMeansClass.Transfer, CuentaB, opciones, 0).Error.Code
            .Should().Be(CuentasPropiasDeVenta.CodigoNoHabilitada, "la cuenta B está habilitada para la consignación, no para la transferencia");
        CuentasPropiasDeVenta.Resolver(7, "TRANSF", PaymentMeansClass.Transfer, Guid.NewGuid(), [], 0).Error.Code
            .Should().Be(CuentasPropiasDeVenta.CodigoNoHabilitada, "sin punto o sin habilitadas, una cuenta elegida no está habilitada");
    }

    [Fact]
    public void Los_demas_medios_no_llevan_cuenta()
    {
        CuentasPropiasDeVenta.Resolver(1, "EFECTIVO", PaymentMeansClass.Cash, CuentaA, [Opcion(1, CuentaA)], 0).Value.Should().BeNull();
    }

    [Fact]
    public void Las_opciones_del_punto_son_las_de_todas_las_cajas_y_las_de_su_caja_activas_y_vivas()
    {
        using var db = TestDbContextFactory.Create();
        var medio = new PaymentMeans { Code = "TRANSF", Name = "Transferencia", Class = PaymentMeansClass.Transfer, DianPaymentMeansCode = "47" };
        db.PaymentMeans.Add(medio);
        db.SaveChanges();
        db.SalesTransferAccounts.AddRange(
            new SalesTransferAccount { PointOfSaleId = 1, PaymentMeansId = medio.Id, OwnBankAccountPublicId = CuentaA, AccountLabel = "A · todas" },
            new SalesTransferAccount { PointOfSaleId = 1, CashRegisterId = 10, PaymentMeansId = medio.Id, OwnBankAccountPublicId = CuentaB, AccountLabel = "B · caja 10" },
            new SalesTransferAccount { PointOfSaleId = 1, CashRegisterId = 11, PaymentMeansId = medio.Id, OwnBankAccountPublicId = Guid.NewGuid(), AccountLabel = "C · caja 11" },
            new SalesTransferAccount { PointOfSaleId = 1, PaymentMeansId = medio.Id, OwnBankAccountPublicId = Guid.NewGuid(), AccountLabel = "D · retirada", IsDeleted = true },
            new SalesTransferAccount { PointOfSaleId = 2, PaymentMeansId = medio.Id, OwnBankAccountPublicId = Guid.NewGuid(), AccountLabel = "E · otro punto" });
        db.SaveChanges();

        var opciones = CuentasPropiasDeVenta.OpcionesAsync(db, 1, 10, default).GetAwaiter().GetResult();

        opciones.Select(o => o.AccountLabel).Should().Equal("A · todas", "B · caja 10");
        opciones.Should().OnlyContain(o => o.PaymentMeansPublicId == medio.PublicId);
    }

    // ---------------------------------------------------------------------------------------- habilitación --

    private sealed class Escena
    {
        public TestApplicationDbContext Db { get; } = TestDbContextFactory.Create();
        public ICuentasBancariasPropias Cuentas { get; } = Substitute.For<ICuentasBancariasPropias>();
        public IAlcanceDeInventario Alcance { get; } = Substitute.For<IAlcanceDeInventario>();
        public PointOfSale Punto { get; }
        public PaymentMeans Transferencia { get; }
        public PaymentMeans Efectivo { get; }

        public Escena(bool ofrecidaEnTodos = true)
        {
            Alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Total);
            Punto = new PointOfSale { Code = "PTO01", Name = "Punto", BranchId = 1, SalesChannelId = 1, DefaultWarehouseId = 1 };
            Transferencia = new PaymentMeans { Code = "TRANSF", Name = "Transferencia", Class = PaymentMeansClass.Transfer, DianPaymentMeansCode = "47",
                OfferedAtAllPointsOfSale = ofrecidaEnTodos };
            Efectivo = new PaymentMeans { Code = "EFECTIVO", Name = "Efectivo", Class = PaymentMeansClass.Cash, DianPaymentMeansCode = "10", OfferedAtAllPointsOfSale = true };
            Db.PointsOfSale.Add(Punto);
            Db.PaymentMeans.AddRange(Transferencia, Efectivo);
            Db.SaveChanges();
        }

        public Task<Result<SalesTransferAccountDto>> HabilitarAsync(Guid medio, Guid cuenta) =>
            new EnableSalesTransferAccountCommandHandler(Db, Alcance, Cuentas).Handle(new EnableSalesTransferAccountCommand(Punto.PublicId, medio, cuenta), default);

        public void Cuenta(Guid id, bool activa = true, bool recaudo = true) =>
            Cuentas.PorPublicIdAsync(id, Arg.Any<CancellationToken>())
                .Returns(new CuentaBancariaPropiaDto(id, "BAN-01", "Banco de ensayo", "1007", "Savings", "******1234", null, recaudo, activa));
    }

    [Fact]
    public async Task Habilitar_guarda_la_cuenta_con_su_rotulo_copiado()
    {
        var e = new Escena();
        e.Cuenta(CuentaA);

        var r = await e.HabilitarAsync(e.Transferencia.PublicId, CuentaA);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.AccountLabel.Should().Be("BAN-01 · Banco de ensayo ******1234");
        r.Value.PaymentMeansCode.Should().Be("TRANSF");
        e.Db.SalesTransferAccounts.Single().OwnBankAccountPublicId.Should().Be(CuentaA);
    }

    [Fact]
    public async Task Habilitar_dos_veces_la_misma_cuenta_se_rechaza()
    {
        var e = new Escena();
        e.Cuenta(CuentaA);
        (await e.HabilitarAsync(e.Transferencia.PublicId, CuentaA)).IsSuccess.Should().BeTrue();

        (await e.HabilitarAsync(e.Transferencia.PublicId, CuentaA)).Error.Code.Should().Be(ErroresDeCuentasDeTransferencia.AlreadyEnabledCode);
    }

    [Fact]
    public async Task Solo_transferencia_o_consignacion_entran_a_una_cuenta()
    {
        var e = new Escena();
        e.Cuenta(CuentaA);

        (await e.HabilitarAsync(e.Efectivo.PublicId, CuentaA)).Error.Code.Should().Be(ErroresDeCuentasDeTransferencia.MeansNotTransferCode);
    }

    [Fact]
    public async Task El_medio_tiene_que_ofrecerse_en_el_punto()
    {
        var e = new Escena(ofrecidaEnTodos: false);
        e.Cuenta(CuentaA);

        (await e.HabilitarAsync(e.Transferencia.PublicId, CuentaA)).Error.Code.Should().Be(ErroresDeCuentasDeTransferencia.MeansNotOfferedCode);

        e.Db.PaymentMeansPointsOfSale.Add(new PaymentMeansPointOfSale { PaymentMeansId = e.Transferencia.Id, PointOfSaleId = e.Punto.Id });
        e.Db.SaveChanges();
        (await e.HabilitarAsync(e.Transferencia.PublicId, CuentaA)).IsSuccess.Should().BeTrue("ya se ofrece en el punto");
    }

    [Fact]
    public async Task La_cuenta_tiene_que_existir_estar_activa_y_ser_de_recaudo()
    {
        var e = new Escena();
        e.Cuenta(CuentaA, recaudo: false);
        e.Cuenta(CuentaB, activa: false);

        (await e.HabilitarAsync(e.Transferencia.PublicId, CuentaA)).Error.Code.Should().Be(ErroresDeCuentasDeTransferencia.AccountNotForCollectionCode);
        (await e.HabilitarAsync(e.Transferencia.PublicId, CuentaB)).Error.Code.Should().Be(ErroresDeCuentasDeTransferencia.AccountNotForCollectionCode);
        (await e.HabilitarAsync(e.Transferencia.PublicId, Guid.NewGuid())).Error.Code.Should().Be(ErroresDeCuentasDeTransferencia.AccountNotFoundCode);
    }

    [Fact]
    public void Los_validadores_piden_punto_medio_y_cuenta()
    {
        new EnableSalesTransferAccountCommandValidator().Validate(new EnableSalesTransferAccountCommand(Guid.Empty, Guid.Empty, Guid.Empty)).Errors
            .Select(x => x.PropertyName).Should().BeEquivalentTo("PointOfSalePublicId", "PaymentMeansPublicId", "OwnBankAccountPublicId");
        new DisableSalesTransferAccountCommandValidator().Validate(new DisableSalesTransferAccountCommand(Guid.Empty, Guid.Empty)).IsValid.Should().BeFalse();
        new GetCashSessionTransferAccountsQueryValidator().Validate(new GetCashSessionTransferAccountsQuery(Guid.Empty)).IsValid.Should().BeFalse();
    }
}
