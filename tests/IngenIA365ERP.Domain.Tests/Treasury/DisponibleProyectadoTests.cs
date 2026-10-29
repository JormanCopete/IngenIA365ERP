using FluentAssertions;
using IngenIA365ERP.Domain.Treasury.Saldos;

namespace IngenIA365ERP.Domain.Tests.Treasury;

/// <summary>
/// Feature 015, segunda ronda del 2026-10-27 (D-i, T0495; FR-103): sobre el saldo en libros, la remesa de bóveda a banco suma, la de banco a bóveda resta, los lotes aprobados
/// restan y un cheque sólo resta si los libros no lo reflejan: ninguno se cuenta dos veces.
/// </summary>
public class DisponibleProyectadoTests
{
    private static readonly DateOnly Dia = new(2026, 12, 10);

    [Fact]
    public void Un_cheque_ya_contabilizado_no_resta_otra_vez_y_uno_sin_contabilizar_si()
    {
        var c = DisponibleProyectado.Calcular(10_000_000m, 0m,
            [new ChequeEnTransito(Guid.NewGuid(), 1, 2_000_000m, Dia, Dia.AddDays(30), InBooks: true),
             new ChequeEnTransito(Guid.NewGuid(), 2, 500_000m, Dia, Dia.AddDays(30), InBooks: false)], []);

        c.ChecksInTransit.Should().Be(2_500_000m, "los dos se informan");
        c.ChecksNotInBooks.Should().Be(500_000m);
        c.ProjectedAvailable.Should().Be(9_500_000m, "los libros ya descontaron el primero al emitirlo");
    }

    [Fact]
    public void Las_remesas_suman_o_restan_con_su_direccion_y_los_lotes_aprobados_restan()
    {
        var c = DisponibleProyectado.Calcular(10_000_000m, 1_000_000m, [],
            [new RemesaEnTransito(Guid.NewGuid(), "RM-1", 3_000_000m, Dia, Dia.AddDays(2)), new RemesaEnTransito(Guid.NewGuid(), "RM-2", -400_000m, Dia, Dia.AddDays(2))]);

        c.RemittancesInTransit.Should().Be(2_600_000m);
        c.ProjectedAvailable.Should().Be(11_600_000m);
    }

    [Fact]
    public void Sin_nada_en_transito_el_disponible_es_el_saldo_en_libros()
    {
        DisponibleProyectado.Calcular(7_500_000m, 0m, [], []).ProjectedAvailable.Should().Be(7_500_000m);
    }
}
