using FluentAssertions;
using IngenIA365ERP.Shared.Services.Ventas;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Ventas;

/// <summary>
/// T639 (FR-056, FR-097, contracts/api.md §22.4): el panel de cobro. La tecla rápida de un medio agrega un pago por lo que falta;
/// muestra falta, sobra y vueltas; el efectivo recibido de más son vueltas, nunca un pago de más; de la tarjeta sólo viajan los
/// últimos cuatro dígitos (<c>LosPagosNoGuardanElNumeroDeTarjeta</c>); el datáfono propuesto es el de la caja. (nuevo)
/// </summary>
public class CobroDelPosTests
{
    private static readonly Guid Sesion = Guid.NewGuid();

    private static MedioDelPosDto Medio(int clase, string code, bool vueltas = false, bool referencia = false, IReadOnlyList<DatafonoDelPosDto>? datafonos = null,
        Guid? porDefecto = null, CreditoPropuestoDto? credito = null) =>
        new(Guid.NewGuid(), code, code, clase, null, referencia, referencia ? 1 : null, vueltas, true, 1, datafonos ?? [], porDefecto, credito);

    [Fact]
    public void La_tecla_del_medio_agrega_un_pago_por_lo_que_falta()
    {
        var cobro = new CobroDelPos(43000m);
        var efectivo = Medio(TextosDeVentas.MedioEfectivo, "EFE", vueltas: true);

        cobro.AgregarPorLoQueFalta(efectivo);

        cobro.Pagos.Should().ContainSingle().Which.Monto.Should().Be(43000m);
        cobro.Falta.Should().Be(0m);
        cobro.AgregarPorLoQueFalta(efectivo).Should().BeNull("sin nada que falte no se agrega otro pago");
    }

    [Fact]
    public void Un_pago_parcial_deja_lo_que_falta_y_el_siguiente_medio_lo_completa()
    {
        var cobro = new CobroDelPos(100000m);
        var datafono = new DatafonoDelPosDto(Guid.NewGuid(), "DAT1");
        var tarjeta = Medio(TextosDeVentas.MedioTarjetaCredito, "VISA", referencia: true, datafonos: [datafono], porDefecto: datafono.PublicId);

        var efectivo = cobro.AgregarPorLoQueFalta(Medio(TextosDeVentas.MedioEfectivo, "EFE", vueltas: true))!;
        efectivo.Monto = 30000m;
        var pagoConTarjeta = cobro.AgregarPorLoQueFalta(tarjeta)!;

        pagoConTarjeta.Monto.Should().Be(70000m);
        pagoConTarjeta.Datafono.Should().Be(datafono.PublicId, "se propone el datáfono de la caja");
        cobro.Falta.Should().Be(0m);
    }

    [Fact]
    public void El_efectivo_recibido_de_mas_son_vueltas()
    {
        var cobro = new CobroDelPos(43000m);
        var pago = cobro.AgregarPorLoQueFalta(Medio(TextosDeVentas.MedioEfectivo, "EFE", vueltas: true))!;

        pago.Entregado = 50000m;

        cobro.Vueltas.Should().Be(7000m);
        cobro.Sobra.Should().Be(0m);
        cobro.Pedido(Sesion).Single().Tendered.Should().Be(50000m);
    }

    [Fact]
    public void Un_pago_de_mas_en_un_medio_sin_vueltas_es_sobra_y_no_deja_cobrar()
    {
        var cobro = new CobroDelPos(43000m);
        var pago = cobro.AgregarPorLoQueFalta(Medio(TextosDeVentas.MedioBono, "BONO", referencia: true))!;
        pago.Monto = 50000m;
        pago.Referencia = "B-1";

        cobro.Sobra.Should().Be(7000m);
        cobro.Problema().Should().NotBeNull();
    }

    [Fact]
    public void Sin_la_referencia_que_el_medio_exige_no_se_cobra()
    {
        var cobro = new CobroDelPos(43000m);
        cobro.AgregarPorLoQueFalta(Medio(TextosDeVentas.MedioBono, "BONO", referencia: true));

        cobro.Problema().Should().Contain("BONO");
    }

    [Theory]
    [InlineData("4111111111111111")]
    [InlineData("12a4")]
    [InlineData("123")]
    public void De_la_tarjeta_solo_se_aceptan_los_ultimos_cuatro_digitos(string digitado)
    {
        var cobro = new CobroDelPos(43000m);
        var pago = cobro.AgregarPorLoQueFalta(Medio(TextosDeVentas.MedioTarjetaDebito, "DEB"))!;

        pago.Last4 = digitado;

        cobro.Problema().Should().Contain("cuatro");
        cobro.Pedido(Sesion).Single().Last4.Should().BeNull("un número que no son cuatro dígitos no viaja");
    }

    [Fact]
    public void El_pedido_lleva_la_sesion_al_efectivo_y_el_credito_propuesto()
    {
        var cobro = new CobroDelPos(90000m);
        var credito = Medio(TextosDeVentas.MedioCreditoAsociado, "CRA", credito: new CreditoPropuestoDto(90, 3, 30, "L01"));
        cobro.AgregarPorLoQueFalta(credito);

        var pago = cobro.Pedido(Sesion).Single();

        pago.CashSessionPublicId.Should().Be(Sesion);
        pago.Credit.Should().NotBeNull();
        pago.Credit!.Installments.Should().Be(3);
        pago.Credit.TermDays.Should().Be(90);
        pago.Credit.SuggestedLineCode.Should().Be("L01");
    }

    [Fact]
    public void Quitar_un_pago_vuelve_a_dejar_lo_que_falta()
    {
        var cobro = new CobroDelPos(43000m);
        var pago = cobro.AgregarPorLoQueFalta(Medio(TextosDeVentas.MedioEfectivo, "EFE", vueltas: true))!;

        cobro.Quitar(pago);

        cobro.Falta.Should().Be(43000m);
        cobro.Problema().Should().NotBeNull();
    }
}
