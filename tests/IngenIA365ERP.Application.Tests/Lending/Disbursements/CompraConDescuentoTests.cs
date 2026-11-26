using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Tests.Lending.Origination;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Configuration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Disbursements;

/// <summary>
/// Pedido del dueño del 2026-11-23, el caso del SOAT: una línea con opciones de descuento condicionado. El asociado compra un bien a un proveedor;
/// con la opción del 50 % el crédito es la mitad del bien y un fondo pone la otra mitad. La solicitud lleva el bien, el proveedor y la opción; el
/// desembolso gira al proveedor el valor completo, sella la compra en las condiciones y el hecho lleva la parte del fondo para Contabilidad.
/// </summary>
public sealed class CompraConDescuentoTests : IAsyncLifetime
{
    private DesembolsoDePrueba _d = null!;

    public async Task InitializeAsync() => _d = await DesembolsoDePrueba.CrearAsync(OriginacionDePrueba.VersionDeLinea() with
    {
        DiscountOptions =
        [
            new DiscountOptionDto("D0", "Sin descuento", 0m, null),
            new DiscountOptionDto("D50", "Descuento del 50 %", 0.5m, "FONDOSOAT"),
            new DiscountOptionDto("SOAT1", "Tarifa 1", 0m, "FONDOSOAT", 74_413m),
        ],
    });

    public Task DisposeAsync()
    {
        _d.Dispose();
        return Task.CompletedTask;
    }

    private CreditApplicationInput Compra(decimal monto = 3_000_000m, decimal? bien = 6_000_000m, string? opcion = "D50") =>
        _d.O.Solicitud(monto) with { AssetValue = bien, SupplierPersonPublicId = bien is null ? null : _d.O.Beto.PublicId, DiscountOptionCode = opcion };

    private async Task<IngenIA365ERP.Application.Common.Models.Error> FallaAlCrearAsync(CreditApplicationInput entrada)
    {
        var r = await _d.O.Crear().Handle(new CreateCreditApplicationCommand(entrada) { OperationKey = Guid.NewGuid() }, default);
        _d.Db.ChangeTracker.Clear();
        r.IsFailure.Should().BeTrue();
        return r.Error;
    }

    [Fact]
    public void El_fondo_paga_la_fraccion_del_bien_al_peso_y_las_opciones_se_validan()
    {
        OpcionesDeDescuento.Subsidio(500_000m, 0.5m).Should().Be(250_000m);
        OpcionesDeDescuento.Subsidio(500_001m, 0.35m).Should().Be(175_000m);
        OpcionesDeDescuento.Problema([new OpcionDeDescuento("D20", "20 %", 0.2m, null)]).Should().NotBeNull("con descuento nombra el fondo");
        OpcionesDeDescuento.Problema([new OpcionDeDescuento("D100", "Todo", 1m, "F")]).Should().NotBeNull("el fondo no paga el bien entero");
        OpcionesDeDescuento.Problema([new OpcionDeDescuento("D0", "Sin descuento", 0m, null)]).Should().BeNull();
    }

    /// <summary>2026-11-26 (pedido del dueño): el fondo puede pagar un valor fijo en pesos, sea cual sea el valor del bien (las tarifas del SOAT).</summary>
    [Fact]
    public void El_fondo_puede_pagar_un_valor_fijo_y_nunca_mas_que_el_bien()
    {
        var fija = new OpcionDeDescuento("SOAT1", "Tarifa 1", 0m, "FONDOSOAT", 74_413m);
        OpcionesDeDescuento.Subsidio(600_000m, fija).Should().Be(74_413m);
        OpcionesDeDescuento.Subsidio(50_000m, fija).Should().Be(50_000m, "el fondo paga a lo sumo el bien");
        OpcionesDeDescuento.Subsidio(500_000m, new OpcionDeDescuento("D50", "50 %", 0.5m, "F")).Should().Be(250_000m);
        OpcionesDeDescuento.Problema([fija]).Should().BeNull();
        OpcionesDeDescuento.Problema([fija with { FundCode = null }]).Should().NotBeNull("con descuento nombra el fondo");
        OpcionesDeDescuento.Problema([fija with { Fraction = 0.2m }]).Should().NotBeNull("es un porcentaje o un valor fijo, no los dos");
        OpcionesDeDescuento.Problema([fija with { Amount = -1m }]).Should().NotBeNull();

        var json = OpcionesDeDescuento.Escribir([fija, new OpcionDeDescuento("D0", "Sin descuento", 0m, null)])!;
        json.Should().Contain("\"amount\":74413").And.NotContain("\"amount\":null", "las opciones de porcentaje se escriben como antes");
        OpcionesDeDescuento.Leer(json).Single(o => o.Code == "SOAT1").Amount.Should().Be(74_413m);
    }

    [Fact]
    public async Task Con_valor_fijo_el_credito_es_el_bien_menos_lo_que_paga_el_fondo()
    {
        (await FallaAlCrearAsync(Compra(monto: 3_000_000m, opcion: "SOAT1"))).Code.Should().Be(ErroresDeOriginacion.CodigoMontoDeLaCompra);

        var s = await _d.O.BorradorAsync(Compra(monto: 6_000_000m - 74_413m, opcion: "SOAT1"));

        s.SubsidizedPurchase!.FundedAmount.Should().Be(74_413m);
        s.SubsidizedPurchase.LoanAmount.Should().Be(5_925_587m);
        s.SubsidizedPurchase.FixedAmount.Should().Be(74_413m);
    }

    [Fact]
    public async Task La_solicitud_lleva_el_bien_el_proveedor_y_la_opcion()
    {
        (await FallaAlCrearAsync(_d.O.Solicitud())).Code.Should().Be(ErroresDeOriginacion.CodigoCompraIncompleta);
        (await FallaAlCrearAsync(Compra(opcion: "D99"))).Code.Should().Be(ErroresDeOriginacion.CodigoOpcionDesconocida);
        var monto = await FallaAlCrearAsync(Compra(monto: 3_500_000m));
        monto.Code.Should().Be(ErroresDeOriginacion.CodigoMontoDeLaCompra);
        OriginacionDePrueba.Datos(monto).GetProperty("expectedAmount").GetDecimal().Should().Be(3_000_000m);

        var s = await _d.O.BorradorAsync(Compra());

        s.SubsidizedPurchase.Should().NotBeNull();
        s.SubsidizedPurchase!.FundedAmount.Should().Be(3_000_000m);
        s.SubsidizedPurchase.LoanAmount.Should().Be(3_000_000m);
        s.SubsidizedPurchase.FundCode.Should().Be("FONDOSOAT");
        s.SubsidizedPurchase.Supplier!.PersonPublicId.Should().Be(_d.O.Beto.PublicId);
    }

    [Fact]
    public async Task Una_linea_sin_opciones_no_admite_compra()
    {
        using var o = await OriginacionDePrueba.CrearAsync();
        var r = await o.Crear().Handle(new CreateCreditApplicationCommand(o.Solicitud() with { AssetValue = 6_000_000m, DiscountOptionCode = "D50" })
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        r.Error.Code.Should().Be(ErroresDeOriginacion.CodigoLineaSinDescuento);
    }

    [Fact]
    public async Task El_desembolso_gira_al_proveedor_el_valor_del_bien_y_el_fondo_pone_su_parte()
    {
        var s = await _d.AprobadaAsync(Compra());
        var entrada = _d.Entrada(s) with
        {
            Form = DisbursementForm.ThirdPartyPayment,
            Beneficiary = new BeneficiaryInput(_d.O.Beto.PublicId, _d.Banco.PublicId, BeneficiaryInput.Ahorros, "9876543210"),
        };

        var r = await _d.DesembolsadoAsync(entrada);

        var orden = await _d.Db.DisbursementOrders.AsNoTracking().SingleAsync();
        orden.GrossAmount.Should().Be(6_000_000m, "al proveedor se le gira el valor del bien");
        orden.SubsidyAmount.Should().Be(3_000_000m);
        orden.NetAmount.Should().Be(6_000_000m);
        r.GrossAmount.Should().Be(3_000_000m, "el crédito es la mitad");
        var condiciones = await _d.Db.LoanTermsVersions.AsNoTracking().SingleAsync();
        CompraSubsidiada.Leer(condiciones.SubsidizedPurchase)!.Subsidio.Should().Be(3_000_000m);
        var hecho = JsonDocument.Parse((await _d.Db.IntegrationMessages.AsNoTracking().SingleAsync(m => m.Type == CreditoDesembolsadoV1.Type)).PayloadJson)
            .RootElement;
        hecho.GetProperty("subsidy").GetProperty("amount").GetDecimal().Should().Be(3_000_000m);
        hecho.GetProperty("subsidy").GetProperty("fundCode").GetString().Should().Be("FONDOSOAT");
        hecho.GetProperty("netToDisburse").GetDecimal().Should().Be(6_000_000m);
    }

    [Fact]
    public async Task Girar_al_deudor_no_se_admite_en_una_compra_con_descuento()
    {
        var s = await _d.AprobadaAsync(Compra());

        var r = await _d.DesembolsarAsync(_d.Entrada(s));

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDeDesembolso.CodigoCompraConDescuento);
    }
}
