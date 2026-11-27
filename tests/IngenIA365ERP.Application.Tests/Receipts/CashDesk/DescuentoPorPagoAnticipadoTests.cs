using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.CashDesk;

/// <summary>
/// 2026-11-26 (pedido del dueño; COOFLOPAL): el descuento de intereses por pago anticipado (<see cref="DescuentoPorPagoAnticipado"/>). Al anticipar una cuota
/// completa de un período que no ha empezado, la cooperativa descuenta una fracción del interés de la cuota (PRESTOR18: 1,4/1,8 → 22,22 %):
/// <list type="bullet">
/// <item>el asociado paga la cuota menos el descuento y Cartera recibe la cuota completa, en un solo pago;</item>
/// <item>sólo la cuota completa, sin anticipo previo, de un período que no ha empezado, de una línea con descuento y con el crédito al día;</item>
/// <item>el descuento queda en la aplicación del recibo y se contabiliza en el comprobante del recibo contra su rol.</item>
/// </list>
/// </summary>
public sealed class DescuentoPorPagoAnticipadoTests
{
    private static readonly DateOnly Hoy = EscenarioDeRecaudo.Hoy;
    private static readonly ModalidadDePago[] Todas = Enum.GetValues<ModalidadDePago>();
    private const decimal Fraccion = 0.2222222222m;

    private readonly CajaConContratos _k = new();

    private static ObligacionListadaDto Obligacion(Guid id, decimal vencido) =>
        new(id, "CR-A", "PRESTOR18", "Préstamo ordinario", RolEnLaObligacion.Debtor, EstadoDeObligacion.Active, vencido > 0m ? 15 : 0, vencido, vencido + 100_000m,
            vencido, 2_000_000m, 0m, 0m, Todas, [], false, false);

    private void ConObligacion(decimal vencido = 0m) =>
        _k.Obligaciones.ListarAsync(Arg.Any<SolicitudDeListado>(), Arg.Any<CancellationToken>()).Returns(ci =>
            new RespuestaDelContratoDto<ListadoDeObligacionesDto>(ResultadoDelContrato.Answered, "OK", "",
                new ListadoDeObligacionesDto(EstadoDeCartera.Active, ci.Arg<SolicitudDeListado>().PersonPublicId, Hoy, [Obligacion(_k.CreditoA, vencido)])));

    /// <summary>
    /// El detalle: la cuota 2 es la del período en curso, la 3 y la 4 son de períodos que no han empezado (la 4 ya tiene un anticipo) y la línea descuenta
    /// <paramref name="fraccion"/> del interés.
    /// </summary>
    private void ConDetalle(decimal fraccion = Fraccion) =>
        _k.Obligaciones.DetalleAsync(Arg.Is<SolicitudDeDetalle>(s => s.ObligationPublicId == _k.CreditoA), Arg.Any<CancellationToken>()).Returns(
            new RespuestaDelContratoDto<DetalleDeObligacionDto>(ResultadoDelContrato.Answered, "OK", "", new DetalleDeObligacionDto(_k.CreditoA, "CR-A",
                "Préstamo ordinario", "Vigente", 0, 0m, 0m, 2_000_000m, 0m, 0m, 110_404m, Hoy.AddDays(20), [],
                [
                    new CuotaDeObligacionDto(2, Hoy.AddDays(20), "Upcoming", 90_000m, 18_404m, 2_000m, 0m, 110_404m, 110_404m, 0m, 110_404m) { InCurrentPeriod = true },
                    new CuotaDeObligacionDto(3, Hoy.AddDays(50), "Upcoming", 91_800m, 16_604m, 2_000m, 0m, 110_404m, 110_404m, 0m, 110_404m),
                    new CuotaDeObligacionDto(4, Hoy.AddDays(80), "Upcoming", 93_636m, 14_768m, 2_000m, 0m, 110_404m, 60_404m, 50_000m, 60_404m),
                ], Todas, true, []) { AdvanceDiscountFraction = fraccion }));

    private UpdateCashOperationItemsCommandHandler Items() => new(_k.C.E.Db, _k.C.E.Alcance, _k.Obligaciones, _k.Cuentas, parametros: _k.C.E.Parametros);

    private async Task<Guid> OperacionAsync()
    {
        await _k.AbrirAsync();
        return (await _k.Crear().Handle(new CreateCashOperationCommand(_k.Sesion.PublicId, _k.Asociado.PublicId), default)).Value.PublicId;
    }

    private async Task<Result<CashOperationDto>> GuardarAsync(Guid op, params ItemPedidoDto[] items) =>
        await Items().Handle(new UpdateCashOperationItemsCommand(op, items) { OperationKey = Guid.NewGuid() }, default);

    private ItemPedidoDto Cuota(int numero, decimal valor, bool conDescuento = true) =>
        new(ApplicationTarget.Loan, valor, _k.CreditoA, LoanModality: ModalidadDePago.SpecificInstallment, InstallmentNumber: numero,
            ApplyAdvanceDiscount: conDescuento);

    private static string Rechazos(Result<CashOperationDto> r)
    {
        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoItemRechazado);
        return JsonSerializer.Serialize(((ErrorConDatos)r.Error).Data);
    }

    [Theory]
    [InlineData(18_404, 4_090)]
    [InlineData(16_604, 3_690)]
    [InlineData(0, 0)]
    public void Es_la_fraccion_del_interes_de_la_cuota_al_peso_como_la_formula_de_la_cooperativa(decimal interes, decimal descuento)
    {
        DescuentoPorPagoAnticipado.Calcular(interes, Fraccion).Should().Be(descuento);
        DescuentoPorPagoAnticipado.Calcular(interes, Fraccion).Should().Be(Math.Round(interes - interes * 1.4m / 1.8m, 0, MidpointRounding.AwayFromZero),
            "INTERES×1,4÷1,8−INTERES, la fórmula de la línea 65 del sistema anterior");
        DescuentoPorPagoAnticipado.Calcular(interes, 0m).Should().Be(0m);
    }

    [Fact]
    public async Task La_cuota_completa_de_un_periodo_sin_empezar_lleva_el_descuento_y_paga_menos()
    {
        ConObligacion();
        ConDetalle();
        var op = await OperacionAsync();

        var r = await GuardarAsync(op, Cuota(3, 110_404m));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        var item = r.Value.Items.Single();
        item.Amount.Should().Be(110_404m, "Cartera recibe la cuota completa");
        item.AdvanceDiscount.Should().Be(3_690m);
        item.AdvanceDiscountFraction.Should().Be(Fraccion);
        item.AmountToPay.Should().Be(106_714m);
        r.Value.TotalItems.Should().Be(106_714m, "lo que paga la persona");
        await _k.Obligaciones.Received().SimularAsync(Arg.Is<SolicitudDeSimulacion>(s => s.Amount == 110_404m), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_pedirlo_no_hay_descuento()
    {
        ConObligacion();
        ConDetalle();
        var op = await OperacionAsync();

        var r = await GuardarAsync(op, Cuota(3, 110_404m, conDescuento: false));

        r.Value.Items.Single().AdvanceDiscount.Should().Be(0m);
        r.Value.TotalItems.Should().Be(110_404m);
    }

    [Fact]
    public async Task No_se_da_a_la_cuota_del_periodo_en_curso_ni_a_una_parte_ni_a_la_ya_anticipada_ni_sin_descuento_en_la_linea()
    {
        // Sin el mínimo de una cuota, para que la parte la rechace el descuento y no esa regla.
        _k.C.E.Parametro(ParametrosDeRecaudoYTesoreria.CajaAnticipoMinimoUnaCuota, "false");
        ConObligacion();
        ConDetalle();
        var op = await OperacionAsync();

        Rechazos(await GuardarAsync(op, Cuota(2, 110_404m))).Should().Contain(ErroresDeRecaudo.CodigoDescuentoNoAdmitido).And.Contain("cuota 2");
        Rechazos(await GuardarAsync(op, Cuota(3, 60_000m))).Should().Contain(ErroresDeRecaudo.CodigoDescuentoNoAdmitido).And.Contain("cuota 3");
        Rechazos(await GuardarAsync(op, Cuota(4, 60_404m))).Should().Contain(ErroresDeRecaudo.CodigoDescuentoNoAdmitido).And.Contain("cuota 4");
        Rechazos(await GuardarAsync(op, new ItemPedidoDto(ApplicationTarget.Loan, 110_404m, _k.CreditoA, LoanModality: ModalidadDePago.Advance,
            ApplyAdvanceDiscount: true))).Should().Contain(ErroresDeRecaudo.CodigoDescuentoNoAdmitido);

        ConDetalle(fraccion: 0m);
        Rechazos(await GuardarAsync(op, Cuota(3, 110_404m))).Should().Contain(ErroresDeRecaudo.CodigoDescuentoNoAdmitido);
    }

    [Fact]
    public async Task Con_mora_solo_si_el_recibo_paga_antes_lo_vencido()
    {
        _k.C.E.Parametro(ParametrosDeRecaudoYTesoreria.CajaConMoraPermiteAnticipar, "true");
        ConObligacion(vencido: 120_000m);
        ConDetalle();
        var op = await OperacionAsync();

        Rechazos(await GuardarAsync(op, Cuota(3, 110_404m))).Should().Contain(ErroresDeRecaudo.CodigoSoloLoExigible);
        var conLoVencido = await GuardarAsync(op, new ItemPedidoDto(ApplicationTarget.Loan, 120_000m, _k.CreditoA, LoanModality: ModalidadDePago.PastDue),
            Cuota(3, 110_404m));
        conLoVencido.IsSuccess.Should().BeTrue(conLoVencido.IsFailure ? conLoVencido.Error.Message : null);
        conLoVencido.Value.TotalItems.Should().Be(120_000m + 106_714m);
    }

    [Fact]
    public async Task Al_confirmar_Cartera_recibe_la_cuota_completa_y_el_recibo_guarda_y_contabiliza_el_descuento()
    {
        ConObligacion();
        ConDetalle();
        var op = await OperacionAsync();
        (await GuardarAsync(op, Cuota(3, 110_404m))).IsSuccess.Should().BeTrue();
        (await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op, [_k.Efectivo(106_714m)]), default)).IsSuccess
            .Should().BeTrue("los medios cubren lo que paga la persona, sin el descuento");

        var r = await _k.Confirmacion().ConfirmarAsync(op, Guid.NewGuid(), null, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Code + " " + r.Error.Message : null);
        await _k.Obligaciones.Received(1).AplicarAsync(Arg.Is<SolicitudDeAplicacion>(s => s.Payments.Count == 1 && s.Payments[0].Amount == 110_404m),
            Arg.Any<CancellationToken>());
        var aplicacion = await _k.C.E.Db.ReceiptApplications.SingleAsync();
        (aplicacion.Amount, aplicacion.AdvanceDiscount, aplicacion.AdvanceDiscountFraction).Should().Be((110_404m, 3_690m, Fraccion));
        var recibo = await _k.C.E.Db.IssuedReceipts.SingleAsync();
        (recibo.TotalApplied, recibo.TotalMeans).Should().Be((110_404m, 106_714m));
        r.Value.Applications.Single().AdvanceDiscount.Should().Be(3_690m);

        var hecho = await _k.C.E.Db.IntegrationMessages.SingleAsync(m => m.Type == ReciboEmitidoV1.Type);
        var contenido = JsonSerializer.Deserialize<ReciboEmitidoV1>(hecho.PayloadJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        contenido.Lines.Single(l => l.Role == RolesDeRecaudo.DescuentoPorPagoAnticipado).Should()
            .Match<FactLineV1>(l => l.Side == RolesDeRecaudo.Debito && l.Amount == 3_690m);
        contenido.Lines.Where(l => l.Role == RolesDeRecaudo.PuenteDelModulo).Sum(l => l.Amount).Should().Be(110_404m, "el puente lleva la cuota completa");
        contenido.Lines.Where(l => l.Side == RolesDeRecaudo.Debito).Sum(l => l.Amount)
            .Should().Be(contenido.Lines.Where(l => l.Side == RolesDeRecaudo.Credito).Sum(l => l.Amount), "cuadra");
    }

    [Fact]
    public async Task Si_el_descuento_cambio_entre_simular_y_confirmar_se_vuelve_a_simular()
    {
        ConObligacion();
        ConDetalle();
        var op = await OperacionAsync();
        (await GuardarAsync(op, Cuota(3, 110_404m))).IsSuccess.Should().BeTrue();
        (await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op, [_k.Efectivo(106_714m)]), default)).IsSuccess.Should().BeTrue();
        ConDetalle(fraccion: 0.25m);

        var r = await _k.Confirmacion().ConfirmarAsync(op, Guid.NewGuid(), null, default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.CodigoItemRechazado);
        JsonSerializer.Serialize(((ErrorConDatos)r.Error).Data).Should().Contain(ErroresDeRecaudo.CodigoDescuentoCambiado);
        await _k.Obligaciones.DidNotReceive().AplicarAsync(Arg.Any<SolicitudDeAplicacion>(), Arg.Any<CancellationToken>());
    }
}
