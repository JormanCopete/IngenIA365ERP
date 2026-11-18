using FluentAssertions;
using Xunit;
using IngenIA365ERP.Shared.Services.Cartera;

namespace IngenIA365ERP.Shared.Tests.Cartera;

/// <summary>2026-11-17: la cuota del estado de cuenta por rubro, con el interés de mora aparte del corriente.</summary>
public class RubrosDeCuotaTests
{
    private static CuotaDelEstadoDto Cuota(Dictionary<string, decimal> debe) =>
        new(2, new DateOnly(2026, 10, 24), new DateOnly(2026, 10, 24), "PastDue", 20_200m, 300m, 0m, 20_500m, debe);

    [Fact]
    public void Lo_que_debe_separa_el_interes_corriente_de_la_mora_y_no_cuenta_el_anticipo()
    {
        var q = Cuota(new() { ["Principal"] = 20_200m, ["CurrentInterest"] = 0m, ["DefaultInterest"] = 70m, ["DefaultInterestInSuspense"] = 8m, ["Advance"] = 500m });

        var r = RubrosDeCuota.De(q, loQueDebe: true);

        r.Capital.Should().Be(20_200m);
        r.Corriente.Should().Be(0m);
        r.Mora.Should().Be(78m);
        r.Otros.Should().Be(0m);
        RubrosDeCuota.Debe(q).Should().Be(20_278m);
    }

    [Fact]
    public void Una_cuota_cubierta_muestra_los_valores_del_plan() =>
        RubrosDeCuota.De(Cuota(new()), loQueDebe: false).Should().Be(new RubrosDeCuota(20_200m, 300m, 0m, 0m, 0m, 0m));
}
