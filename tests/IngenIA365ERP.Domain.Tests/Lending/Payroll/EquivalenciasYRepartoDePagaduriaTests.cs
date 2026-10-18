using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Payroll;

namespace IngenIA365ERP.Domain.Tests.Lending.Payroll;

/// <summary>
/// Feature 014, US3 (T090–T092; data-model §1.4.2; FR-125): las equivalencias de pagaduría —JSON canónico, un ítem en un solo código, prioridad
/// única con <c>Priority</c>, qué código cubre una línea y un concepto— y el reparto de lo descontado por un código (<c>Priority</c> llena en orden;
/// <c>Proportional</c> a prorrata al centavo con el residuo a la de mayor pendiente; lo que excede lo pendiente sobra). (nuevo)
/// </summary>
public sealed class EquivalenciasYRepartoDePagaduriaTests
{
    private static ItemDeEquivalencia I(LineNature n, string linea, string concepto, int prioridad = 1) => new(n, linea, concepto, prioridad);

    [Fact]
    public void El_json_canonico_no_depende_del_orden_y_se_lee_de_vuelta()
    {
        var a = EquivalenciasDePagaduria.Serializar([I(LineNature.Deposit, "ahperm", "Installment", 2), I(LineNature.Contribution, "*", "Installment", 1)]);
        var b = EquivalenciasDePagaduria.Serializar([I(LineNature.Contribution, "*", "Installment", 1), I(LineNature.Deposit, "AHPERM", "Installment", 2)]);

        a.Should().Be(b);
        a.Should().Be("""[{"nature":"Contribution","creditLineCode":"*","concept":"Installment","priority":1},{"nature":"Deposit","creditLineCode":"AHPERM","concept":"Installment","priority":2}]""");
        EquivalenciasDePagaduria.Leer(a).Should().HaveCount(2);
        EquivalenciasDePagaduria.Leer("no es json").Should().BeEmpty();
    }

    [Fact]
    public void Un_item_que_se_pisa_con_otro_de_otro_codigo_se_rechaza()
    {
        var problema = EquivalenciasDePagaduria.Validar(
        [
            new("101", ConceptAllocationRule.Priority, 1, [I(LineNature.Placement, "*", "Installment")]),
            new("102", ConceptAllocationRule.Priority, 2, [I(LineNature.Placement, "CONSUMO", "Principal")]),
        ]);
        problema!.Codigo.Should().Be(EquivalenciasDePagaduria.CodigoMapeadoDosVeces);

        EquivalenciasDePagaduria.Validar(
        [
            new("101", ConceptAllocationRule.Priority, 1, [I(LineNature.Placement, "CONSUMO", "Principal")]),
            new("102", ConceptAllocationRule.Priority, 2, [I(LineNature.Placement, "CONSUMO", "CurrentInterest", 1), I(LineNature.Contribution, "*", "Installment", 2)]),
        ]).Should().BeNull("conceptos distintos de la misma línea y otra naturaleza no se pisan");
    }

    [Fact]
    public void Sin_items_o_con_prioridad_repetida_se_rechaza()
    {
        EquivalenciasDePagaduria.Validar([new("101", ConceptAllocationRule.Priority, 1, [])])!.Codigo.Should().Be(EquivalenciasDePagaduria.CodigoSinItems);
        EquivalenciasDePagaduria.Validar([new("101", ConceptAllocationRule.Priority, 1,
            [I(LineNature.Placement, "A", "Installment", 1), I(LineNature.Placement, "B", "Installment", 1)])])!.Codigo
            .Should().Be(EquivalenciasDePagaduria.CodigoPrioridadRepetida);
        EquivalenciasDePagaduria.Validar([new("101", ConceptAllocationRule.Proportional, 1,
            [I(LineNature.Placement, "A", "Installment", 1), I(LineNature.Placement, "B", "Installment", 1)])]).Should().BeNull("a prorrata la prioridad no manda");
        EquivalenciasDePagaduria.Validar([new("101", (ConceptAllocationRule)9, 1, [I(LineNature.Placement, "A", "Installment")])])!.Codigo
            .Should().Be(EquivalenciasDePagaduria.CodigoSinReparto);
    }

    [Fact]
    public void Resolver_encuentra_el_codigo_que_cubre_la_linea_y_el_concepto()
    {
        EquivalenciaDePagaduria[] eq =
        [
            new("101", ConceptAllocationRule.Priority, 1, [I(LineNature.Placement, "*", "Installment")]),
            new("201", ConceptAllocationRule.Proportional, 2, [I(LineNature.Contribution, "*", "Installment")]),
        ];
        EquivalenciasDePagaduria.Resolver(eq, LineNature.Placement, "CONSUMO", "Principal")!.Codigo.Should().Be("101");
        EquivalenciasDePagaduria.Resolver(eq, LineNature.Contribution, "APORTES", "Ordinary")!.Codigo.Should().Be("201");
        EquivalenciasDePagaduria.Resolver(eq, LineNature.Deposit, "AHPERM", "Principal").Should().BeNull("lo no mapeado va a no aplicados");
    }

    [Fact]
    public void Por_prioridad_llena_en_orden_y_lo_que_excede_sobra()
    {
        var r = RepartoDePagaduria.Repartir(130_000m, ConceptAllocationRule.Priority,
            [new("ahorro", 50_000m, 2), new("aportes", 100_000m, 1)]);

        r.Asignaciones.Should().Equal(("aportes", 100_000m), ("ahorro", 30_000m));
        r.Sobrante.Should().Be(0m);

        var excede = RepartoDePagaduria.Repartir(200_000m, ConceptAllocationRule.Priority, [new("aportes", 100_000m, 1), new("ahorro", 50_000m, 2)]);
        excede.Sobrante.Should().Be(50_000m);
    }

    [Fact]
    public void A_prorrata_reparte_al_centavo_y_el_residuo_va_a_la_de_mayor_pendiente()
    {
        var r = RepartoDePagaduria.Repartir(100m, ConceptAllocationRule.Proportional, [new("a", 100m, 1), new("b", 100m, 1), new("c", 100m, 1)]);

        r.Asignaciones.Sum(a => a.Asignado).Should().Be(100m);
        r.Asignaciones.Should().Equal(("a", 33.34m), ("b", 33.33m), ("c", 33.33m));
        r.Sobrante.Should().Be(0m);

        var desigual = RepartoDePagaduria.Repartir(90m, ConceptAllocationRule.Proportional, [new("a", 60m, 1), new("b", 120m, 1)]);
        desigual.Asignaciones.Should().Equal(("a", 30m), ("b", 60m));
    }
}
