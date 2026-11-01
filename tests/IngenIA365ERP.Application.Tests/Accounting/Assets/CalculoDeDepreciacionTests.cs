using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Assets;

namespace IngenIA365ERP.Application.Tests.Accounting.Assets;

/// <summary>
/// T155 — US12: la línea recta sobre el valor neto. Cuotas iguales con el residual fuera; la última se lleva el redondeo; un mes sin
/// correr se acumula en la cuota siguiente; cambiar vida útil o residual mueve sólo las cuotas que faltan; un activo que entra con
/// historia arranca desde su acumulada y sus meses consumidos; uno totalmente depreciado no tiene cuota.
/// </summary>
public class CalculoDeDepreciacionTests
{
    private static readonly Mes Enero = new(2026, 1);

    private static ActivoParaCalculo Activo(decimal costo = 6_000_000m, decimal residual = 0m, int vida = 60, decimal acumuladaInicial = 0m, int mesesIniciales = 0, Mes? inicio = null) =>
        new(costo, residual, vida, acumuladaInicial, mesesIniciales, inicio ?? Enero);

    [Fact]
    public void Cuota_mensual_es_lo_depreciable_entre_la_vida_util()
    {
        CalculoDeDepreciacion.CuotaDe(Activo(residual: 600_000m), [], Enero).Should().Be(90_000m);
    }

    [Fact]
    public void Un_mes_sin_correr_se_acumula_en_la_cuota_siguiente()
    {
        CalculoDeDepreciacion.CuotaDe(Activo(), [], new Mes(2026, 3)).Should().Be(300_000m);
    }

    [Fact]
    public void Antes_del_inicio_y_en_un_mes_ya_cubierto_no_hay_cuota()
    {
        var activo = Activo(inicio: new Mes(2026, 3));
        CalculoDeDepreciacion.CuotaDe(activo, [], Enero).Should().Be(0m);
        CalculoDeDepreciacion.CuotaDe(activo, [new CuotaContabilizada(new Mes(2026, 3), 100_000m)], new Mes(2026, 3)).Should().Be(0m);
    }

    [Fact]
    public void La_proyeccion_cierra_exacta_aunque_la_division_no_sea_redonda()
    {
        var activo = Activo(costo: 1_000m, vida: 3);
        var proyeccion = CalculoDeDepreciacion.Proyeccion(activo, []);
        proyeccion.Select(p => p.Amount).Should().Equal(333.33m, 333.34m, 333.33m);
        proyeccion[^1].NetBookValue.Should().Be(0m);
        proyeccion.Sum(p => p.Amount).Should().Be(1_000m);
    }

    [Fact]
    public void Cambiar_la_vida_util_reparte_el_neto_en_los_meses_que_quedan_sin_tocar_lo_contabilizado()
    {
        var contabilizadas = new[] { new CuotaContabilizada(Enero, 100_000m), new CuotaContabilizada(new Mes(2026, 2), 100_000m) };
        var antes = Activo(vida: 60);
        var despues = antes with { LifeMonths = 30 };
        CalculoDeDepreciacion.CuotaDe(antes, contabilizadas, new Mes(2026, 3)).Should().Be(100_000m);
        // Quedan 5.800.000 en 28 meses.
        CalculoDeDepreciacion.CuotaDe(despues, contabilizadas, new Mes(2026, 3)).Should().Be(Math.Round(5_800_000m / 28m, 2, MidpointRounding.AwayFromZero));
        CalculoDeDepreciacion.Acumulada(despues, contabilizadas).Should().Be(200_000m);
    }

    [Fact]
    public void Un_activo_que_entra_con_historia_parte_de_su_acumulada_y_sus_meses()
    {
        var activo = Activo(costo: 6_000_000m, vida: 60, acumuladaInicial: 2_400_000m, mesesIniciales: 24);
        CalculoDeDepreciacion.CuotaDe(activo, [], Enero).Should().Be(100_000m);
        CalculoDeDepreciacion.Proyeccion(activo, []).Should().HaveCount(36);
    }

    [Fact]
    public void Totalmente_depreciado_no_tiene_cuota_ni_proyeccion()
    {
        var activo = Activo(costo: 1_200m, vida: 12, acumuladaInicial: 1_200m, mesesIniciales: 12);
        CalculoDeDepreciacion.CuotaDe(activo, [], Enero).Should().Be(0m);
        CalculoDeDepreciacion.Proyeccion(activo, []).Should().BeEmpty();
    }

    [Fact]
    public void Con_la_vida_agotada_y_saldo_pendiente_la_cuota_es_todo_el_saldo()
    {
        var activo = Activo(costo: 1_000m, vida: 2, acumuladaInicial: 400m, mesesIniciales: 2);
        CalculoDeDepreciacion.CuotaDe(activo, [], Enero).Should().Be(600m);
    }
}
