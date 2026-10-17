using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Housing;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Replay;

namespace IngenIA365ERP.Domain.Tests.Lending.Housing;

/// <summary>
/// Feature 013, K6b, decisión del dueño del 2026-10-17: un <b>pago aplicado en un día cuya UVR se corrige después</b>. Lo recibido en pesos no cambia,
/// pero a la UVR nueva el pago es otra cantidad de UVR y cubre en pesos otra mezcla de conceptos: con la UVR más alta, el interés (fijo en UVR) vale
/// más pesos y le queda menos al capital; con la más baja, al revés. <see cref="LibroTeorico.DiferenciasSeparandoLaCorreccionMonetaria"/> con
/// <c>separarPagos</c> separa ese reparto (<see cref="DiferenciaDeLibro.DePago"/>) de la causación y de la corrección monetaria, cuadra solo
/// (<see cref="LibroTeorico.DescuadreDelReparto"/> = 0) y, registrado con lo demás <b>hasta la marca</b> (la UVR de un día cambia también la
/// corrección monetaria del día siguiente y, por el capital en UVR, la causación de los días que siguen), deja el libro igual a la reconstrucción.
/// Sobre el caso dorado 63 (P1 de 900.000 pesos el 03-12-2026; serie UVR de prueba).
/// </summary>
public class RepartoDePagoConUvrCorregidaTests
{
    private static readonly DateOnly DiaDelPago = new(2026, 12, 3);
    private static readonly DateOnly Marca = new(2026, 12, 20);
    private static readonly DateOnly Desembolso = new(2026, 11, 3);

    /// <summary>La entrada del caso 63 con la información del 05-01-2027 (P1 vigente; P2 todavía no rige a la marca).</summary>
    private static EntradaDeReconstruccion Entrada(decimal? valorDeP1 = null)
    {
        var caso = CasoDoradoDeCartera.Cargar(Path.Combine(CasoDoradoDeCartera.DirectorioDeCasos, "63-vivienda-uvr-pago-y-reverso.json"));
        var entrada = caso.Entrada(caso.Condiciones, caso.Operaciones, DateTime.Parse("2027-01-05T23:00:00Z").ToUniversalTime());
        if (valorDeP1 is not { } valor) return entrada;
        return entrada with
        {
            Operaciones = entrada.Operaciones.Select(o => o.Id == "P1" && o.Contenido is ContenidoDePago p ? o with { Contenido = p with { Valor = valor } } : o)
                .ToList(),
        };
    }

    /// <summary>La misma entrada con la UVR del día del pago corregida en <paramref name="delta"/> pesos por UVR.</summary>
    private static EntradaDeReconstruccion ConUvrCorregida(EntradaDeReconstruccion e, decimal delta) => e with
    {
        Uvr = new SerieUvr(e.Uvr!.Valores.Select(v => v.Fecha == DiaDelPago ? v with { Valor = v.Valor + delta } : v)),
    };

    /// <summary>El libro tal como quedó registrado con la UVR publicada al causar.</summary>
    private static IReadOnlyList<MovimientoDeLibro> LibroRegistrado(EntradaDeReconstruccion e) =>
        MotorDeCartera.Reconstruir(e, Marca, Desembolso).Libro.Movimientos;

    /// <summary>Lo que la corrida registraría: el reparto como reaplicación, la corrección monetaria con su clase y lo demás como ajuste.</summary>
    private static IEnumerable<MovimientoDeLibro> Registrar(IEnumerable<DiferenciaDeLibro> diferencias) => diferencias.Select(d => new MovimientoDeLibro(d.Dia,
        d.DePago ? LoanTransactionKind.PaymentReallocation
            : d.DeCorreccionMonetaria ? LoanTransactionKind.MonetaryCorrection : LoanTransactionKind.AccrualAdjustment,
        d.Concepto, d.Cuota, d.Diferencia));

    [Theory]
    [InlineData(1.5, "sube")]
    [InlineData(-1.5, "baja")]
    public void El_reparto_del_pago_cambia_por_concepto_cuadra_solo_y_el_libro_vuelve_a_cuadrar_hasta_la_marca(double delta, string sentido)
    {
        var original = Entrada();
        var libro = LibroRegistrado(original);
        var corregida = ConUvrCorregida(original, (decimal)delta);

        var diferencias = MotorDeCartera.VerificarSeparandoLaCorreccionMonetaria(corregida, Marca, libro, separarPagos: true);

        var reparto = diferencias.Where(d => d.DePago).ToList();
        reparto.Should().NotBeEmpty($"la UVR del día del pago {sentido}");
        reparto.Should().OnlyContain(d => d.Dia == DiaDelPago && !d.DeCorreccionMonetaria, "el único pago es el del día corregido");
        LibroTeorico.DescuadreDelReparto(reparto).Should().Be(0m, "lo recibido en pesos no cambia: sólo se redistribuye");
        var interes = reparto.Single(d => d.Concepto == LoanConcept.CurrentInterest).Diferencia;
        var capital = reparto.Single(d => d.Concepto == LoanConcept.Principal).Diferencia;
        if (delta > 0)
        {
            interes.Should().BeNegative("con la UVR más alta el interés en UVR vale más pesos: el pago cubre más interés (fila más negativa)");
            capital.Should().BePositive("y le queda menos al capital");
        }
        else
        {
            interes.Should().BePositive("con la UVR más baja el pago cubre menos interés en pesos");
            capital.Should().BeNegative("y más capital");
        }
        capital.Should().Be(-interes);

        diferencias.Should().Contain(d => d.DeCorreccionMonetaria && d.Dia == DiaDelPago);
        diferencias.Should().Contain(d => d.Dia > DiaDelPago,
            "la UVR de un día cambia también la corrección monetaria del día siguiente y, con el capital en UVR que dejó el pago, la causación que sigue: "
            + "la corrida tiene que llegar hasta la marca");

        MotorDeCartera.Verificar(corregida, Marca, libro.Concat(Registrar(diferencias))).Should().BeEmpty(
            "registrado el reparto como reaplicación, la corrección monetaria con su clase y el resto como ajuste, el libro cuadra con la reconstrucción");
    }

    [Fact]
    public void Sin_separar_pagos_el_capital_del_reparto_queda_mezclado_con_la_causacion()
    {
        var original = Entrada();
        var corregida = ConUvrCorregida(original, 1.5m);

        var mezcladas = MotorDeCartera.VerificarSeparandoLaCorreccionMonetaria(corregida, Marca, LibroRegistrado(original));

        mezcladas.Should().NotContain(d => d.DePago);
        mezcladas.Should().Contain(d => !d.DeCorreccionMonetaria && d.Concepto == LoanConcept.Principal && d.Dia == DiaDelPago,
            "como ajuste de causación, el capital no tiene rol en Contabilidad: por eso el reparto va en su propio hecho");
    }

    [Fact]
    public void Un_pago_que_deja_anticipo_redistribuye_tambien_el_anticipo_y_sigue_cuadrando()
    {
        // 3.000.000 pesos cubren la cuota 1 y dejan anticipo o abono: con la UVR más alta, la cuota vale más pesos y queda menos para lo demás.
        var original = Entrada(3_000_000m);
        var libro = LibroRegistrado(original);
        var corregida = ConUvrCorregida(original, 2m);

        var diferencias = MotorDeCartera.VerificarSeparandoLaCorreccionMonetaria(corregida, Marca, libro, separarPagos: true);

        var reparto = diferencias.Where(d => d.DePago).ToList();
        reparto.Should().HaveCountGreaterThan(1);
        LibroTeorico.DescuadreDelReparto(reparto).Should().Be(0m);
        MotorDeCartera.Verificar(corregida, Marca, libro.Concat(Registrar(diferencias))).Should().BeEmpty();
    }

    [Fact]
    public void El_descuadre_del_reparto_resta_los_pasivos_con_el_deudor()
    {
        var dia = DiaDelPago;
        LibroTeorico.DescuadreDelReparto(
        [
            new DiferenciaDeLibro(dia, LoanConcept.CurrentInterest, 1, -100m, -110m) { DePago = true },
            new DiferenciaDeLibro(dia, LoanConcept.Advance, 1, 300m, 290m) { DePago = true },
        ]).Should().Be(0m, "cubrir 10 más de interés deja 10 menos de anticipo");
        LibroTeorico.DescuadreDelReparto(
        [
            new DiferenciaDeLibro(dia, LoanConcept.CurrentInterest, 1, -100m, -110m) { DePago = true },
            new DiferenciaDeLibro(dia, LoanConcept.Principal, 1, -50m, -50m) { DePago = true },
        ]).Should().Be(-10m);
        LibroTeorico.EsDePago(LoanTransactionKind.PaymentAllocation).Should().BeTrue();
        LibroTeorico.EsDePago(LoanTransactionKind.PaymentReallocation).Should().BeTrue();
        LibroTeorico.EsDePago(LoanTransactionKind.CreditBalanceSettled).Should().BeFalse("una resolución no es un reparto");
        LibroTeorico.EsDePago(LoanTransactionKind.Accrual).Should().BeFalse();
    }
}
