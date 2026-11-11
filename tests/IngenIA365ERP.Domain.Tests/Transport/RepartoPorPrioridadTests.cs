using FluentAssertions;
using IngenIA365ERP.Domain.Transport;

namespace IngenIA365ERP.Domain.Tests.Transport;

/// <summary>Feature 020 (T168; R13; FR-056): un recibo que paga la factura sin decir la línea se reparte por prioridad del servicio.</summary>
public class RepartoPorPrioridadTests
{
    [Fact]
    public void Paga_primero_la_menor_prioridad_y_no_pasa_del_saldo()
    {
        var (partes, sobrante) = RepartoPorPrioridad.Repartir(70_000m,
        [
            new CargoPorRepartir(Id: 1, Prioridad: 2, Orden: 1, Saldo: 30_000m),
            new CargoPorRepartir(Id: 2, Prioridad: 1, Orden: 2, Saldo: 50_000m),
        ]);

        partes.Should().Equal(new ParteDelReparto(2, 50_000m), new ParteDelReparto(1, 20_000m));
        sobrante.Should().Be(0m);
    }

    [Fact]
    public void Entre_iguales_desempata_el_orden_de_la_linea()
    {
        var (partes, _) = RepartoPorPrioridad.Repartir(10_000m,
        [
            new CargoPorRepartir(Id: 7, Prioridad: 1, Orden: 2, Saldo: 10_000m),
            new CargoPorRepartir(Id: 8, Prioridad: 1, Orden: 1, Saldo: 10_000m),
        ]);

        partes.Should().ContainSingle().Which.Should().Be(new ParteDelReparto(8, 10_000m));
    }

    [Fact]
    public void Lo_que_sobra_se_devuelve()
    {
        var (partes, sobrante) = RepartoPorPrioridad.Repartir(100_000m, [new CargoPorRepartir(1, 1, 1, 60_000m)]);

        partes.Should().ContainSingle().Which.Importe.Should().Be(60_000m);
        sobrante.Should().Be(40_000m);
    }

    [Fact]
    public void Un_cargo_ya_pagado_no_recibe_nada()
    {
        var (partes, sobrante) = RepartoPorPrioridad.Repartir(5_000m, [new CargoPorRepartir(1, 1, 1, 0m), new CargoPorRepartir(2, 2, 2, 5_000m)]);

        partes.Should().ContainSingle().Which.CargoId.Should().Be(2);
        sobrante.Should().Be(0m);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Un_importe_sin_valor_no_reparte(int importe)
    {
        var (partes, sobrante) = RepartoPorPrioridad.Repartir(importe, [new CargoPorRepartir(1, 1, 1, 5_000m)]);

        partes.Should().BeEmpty();
        sobrante.Should().Be(0m);
    }
}
