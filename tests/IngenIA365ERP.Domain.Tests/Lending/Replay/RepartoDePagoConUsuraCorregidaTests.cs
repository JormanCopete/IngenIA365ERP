using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Replay;

namespace IngenIA365ERP.Domain.Tests.Lending.Replay;

/// <summary>
/// Feature 013, decisión del dueño del 2026-10-18 (pregunta (a) de la nota del 2026-10-17): un <b>pago aplicado en un día cuya usura se corrige
/// después</b> se trata con el mismo molde que el de la UVR. Lo recibido no cambia, pero con la usura más baja el interés del pago es menor y le
/// queda más al capital: <see cref="LibroTeorico.DiferenciasSeparandoLaCorreccionMonetaria"/> con <c>separarPagos</c> separa ese reparto
/// (<see cref="DiferenciaDeLibro.DePago"/>), que cuadra solo (<see cref="LibroTeorico.DescuadreDelReparto"/> = 0) y viaja en
/// <c>PagoRedistribuidoPorValorNacional</c>; sin separar, el capital quedaba como ajuste de causación, sin rol en Contabilidad. Sobre el caso dorado 14
/// (la usura de marzo de 2027 corregida de 27 % a 24 % EA el 20-03) con un pago de la cuota 1 el 15-03.
/// </summary>
public class RepartoDePagoConUsuraCorregidaTests
{
    private static readonly DateOnly DiaDelPago = new(2027, 3, 15);
    private static readonly DateOnly Marca = new(2027, 3, 19);
    private static readonly DateOnly Desembolso = new(2027, 2, 10);
    private static readonly DateTime AntesDeCorregir = DateTime.Parse("2027-03-19T23:00:00Z").ToUniversalTime();
    private static readonly DateTime DespuesDeCorregir = DateTime.Parse("2027-03-25T23:00:00Z").ToUniversalTime();

    /// <summary>El caso 14 con un pago de <paramref name="valor"/> el 15-03, leído con la información hasta <paramref name="r"/>.</summary>
    private static EntradaDeReconstruccion Entrada(DateTime r, decimal valor)
    {
        var caso = CasoDoradoDeCartera.Cargar(Path.Combine(CasoDoradoDeCartera.DirectorioDeCasos, "14-correccion-de-usura.json"));
        var operaciones = caso.Operaciones.Append(new CasoDoradoDeCartera.OperacionJson
        {
            Id = "P1", Tipo = nameof(LoanOperationKind.Payment), Modalidad = PaymentModality.FreeAmount, Valor = valor, FechaValor = DiaDelPago,
            RegistradoEn = DateTime.Parse("2027-03-15T16:00:00Z").ToUniversalTime(),
        }).ToList();
        return caso.Entrada(caso.Condiciones, operaciones, r);
    }

    /// <summary>Lo que la corrida registraría: el reparto como reaplicación y lo demás como ajuste de causación.</summary>
    private static IEnumerable<MovimientoDeLibro> Registrar(IEnumerable<DiferenciaDeLibro> diferencias) => diferencias.Select(d => new MovimientoDeLibro(d.Dia,
        d.DePago ? LoanTransactionKind.PaymentReallocation : LoanTransactionKind.AccrualAdjustment, d.Concepto, d.Cuota, d.Diferencia));

    [Theory]
    [InlineData(100_000, "cubre sólo parte de la cuota 1")]
    [InlineData(400_000, "cubre la cuota 1 y deja anticipo o abono")]
    public void El_reparto_del_pago_va_aparte_cuadra_solo_y_el_libro_vuelve_a_cuadrar(int valor, string como)
    {
        var libro = MotorDeCartera.Reconstruir(Entrada(AntesDeCorregir, valor), Marca, Desembolso).Libro.Movimientos;
        var corregida = Entrada(DespuesDeCorregir, valor);

        var diferencias = MotorDeCartera.VerificarSeparandoLaCorreccionMonetaria(corregida, Marca, libro, separarPagos: true);

        var reparto = diferencias.Where(d => d.DePago).ToList();
        reparto.Should().NotBeEmpty($"el pago {como} y la usura de su día bajó");
        reparto.Should().OnlyContain(d => d.Dia == DiaDelPago && !d.DeCorreccionMonetaria, "el único pago es el del 15-03");
        LibroTeorico.DescuadreDelReparto(reparto).Should().Be(0m, "lo recibido no cambia: sólo se redistribuye");
        reparto.Where(d => d.Concepto == LoanConcept.CurrentInterest).Sum(d => d.Diferencia)
            .Should().BePositive("con la usura más baja el pago cubre menos interés (fila menos negativa)");
        diferencias.Where(d => !d.DePago).Should().NotContain(d => d.Concepto == LoanConcept.Principal,
            "el capital del pago ya no queda como ajuste de causación, que no tiene con qué contabilizarse");
        diferencias.Where(d => !d.DePago).Should().NotContain(d => d.Concepto == LoanConcept.CreditBalance,
            "un saldo a favor que deje el pago es parte de su reparto: no lo reconoce aparte la corrida (C93)");

        MotorDeCartera.Verificar(corregida, Marca, libro.Concat(Registrar(diferencias))).Should().BeEmpty(
            "registrado el reparto como reaplicación y el resto como ajuste, el libro cuadra con la reconstrucción");
    }

    [Fact]
    public void Sin_separar_pagos_el_capital_del_pago_queda_como_ajuste_de_causacion()
    {
        var libro = MotorDeCartera.Reconstruir(Entrada(AntesDeCorregir, 100_000m), Marca, Desembolso).Libro.Movimientos;

        var mezcladas = MotorDeCartera.VerificarSeparandoLaCorreccionMonetaria(Entrada(DespuesDeCorregir, 100_000m), Marca, libro);

        mezcladas.Should().NotContain(d => d.DePago);
        mezcladas.Should().Contain(d => d.Concepto == LoanConcept.Principal && d.Dia == DiaDelPago,
            "es el defecto que corrige la decisión del 2026-10-18: el capital del pago iba al ajuste de causación");
    }
}
