using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Sales.Cash;

namespace IngenIA365ERP.Domain.Tests.Sales.Cash;

/// <summary>
/// Feature 012, I3, T547 (T50; data-model §15 <c>INV_CashSessions.ShortageTreatment</c>): las reglas sueltas del arqueo que no
/// caben en un caso dorado —el valor sellado del faltante, un medio contado que no está en el esperado y el sobrante que es
/// siempre <c>Surplus</c> aunque el faltante vaya al gasto—.
/// </summary>
public class EvaluadorDeArqueoTests
{
    private static readonly MedioDeArqueo Efectivo = new(1, "EF", PaymentMeansClass.Cash, CashCountMethod.PhysicalCount, 0m);

    private static EsperadoDeLaSesion Esperado(decimal valor) =>
        CalculadoraDeEsperado.Calcular(new PedidoDeEsperado(1, [Efectivo], new BaseDeApertura(1, valor), [], []));

    [Theory]
    [InlineData("Gasto", CashDifferenceTreatment.ShortageToExpense)]
    [InlineData("CargoAlCajero", CashDifferenceTreatment.ShortageToCashier)]
    public void El_tratamiento_sellado_en_la_sesion_decide_el_del_faltante(string sellado, CashDifferenceTreatment tratamiento) =>
        EvaluadorDeArqueo.TratamientoDelFaltanteDesde(sellado).Should().Be(tratamiento);

    [Fact]
    public void Un_tratamiento_sellado_desconocido_es_un_error_visible()
    {
        var accion = () => EvaluadorDeArqueo.TratamientoDelFaltanteDesde("Otro");
        accion.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void El_sobrante_es_Surplus_aunque_el_faltante_vaya_al_gasto()
    {
        var r = EvaluadorDeArqueo.Evaluar(Esperado(100m), [new ConteoDeMedio(1, 150m, "sobró")], CashDifferenceTreatment.ShortageToExpense);
        r.DifferenceLines.Single().Treatment.Should().Be(CashDifferenceTreatment.Surplus);
        r.DifferenceLines.Single().Sign.Should().Be(1);
        r.AmountToApprove.Should().Be(50m, "la tolerancia del medio es cero");
    }

    [Fact]
    public void Contar_un_medio_que_no_esta_en_el_esperado_es_un_error_de_quien_llama()
    {
        var accion = () => EvaluadorDeArqueo.Evaluar(Esperado(100m), [new ConteoDeMedio(99, 1m, null)], CashDifferenceTreatment.ShortageToExpense);
        accion.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Un_medio_sin_conteo_cuenta_cero()
    {
        var r = EvaluadorDeArqueo.Evaluar(Esperado(100m), [], CashDifferenceTreatment.ShortageToCashier);
        r.Lines.Single().Counted.Should().Be(0m);
        r.Lines.Single().Difference.Should().Be(-100m);
        r.MissingReasons.Should().Equal("EF");
    }
}
