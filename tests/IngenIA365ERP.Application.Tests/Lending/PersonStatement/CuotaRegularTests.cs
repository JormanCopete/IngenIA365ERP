using IngenIA365ERP.Application.Lending.Loans;
using IngenIA365ERP.Application.Lending.PersonStatement;

namespace IngenIA365ERP.Application.Tests.Lending.PersonStatement;

/// <summary>Feature 025, HU6 (FR-019): «Cuota» del estado de cuenta es la cuota regular, no la primera del plan.</summary>
public class CuotaRegularTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 24);

    private static ScheduleInstallmentDto Cuota(int n, DateOnly vence, decimal total, bool extra = false, bool gracia = false) =>
        new(n, vence.AddMonths(-1), vence, vence, 0m, 0m, 0m, 0m, 0m, 0m, total, 0m, extra, gracia);

    [Fact]
    public void En_un_credito_cargado_no_toma_la_primera_cuota_recortada_por_el_corte()
    {
        var plan = new[]
        {
            Cuota(15, new(2026, 11, 9), 400_000.00m),
            Cuota(16, new(2026, 12, 9), 520_000.00m),
            Cuota(17, new(2027, 1, 9), 520_000.00m),
            Cuota(18, new(2027, 2, 9), 520_000.00m),
            Cuota(19, new(2027, 3, 9), 300_000m),
        };
        Assert.Equal(520_000.00m, GetPersonAccountStatementQueryHandler.CuotaRegular(plan, Hoy));
    }

    [Fact]
    public void Ignora_las_extraordinarias_y_las_de_gracia()
    {
        var plan = new[]
        {
            Cuota(1, new(2026, 12, 9), 100m, gracia: true),
            Cuota(2, new(2027, 1, 9), 900m, extra: true),
            Cuota(3, new(2027, 1, 9), 900m, extra: true),
            Cuota(4, new(2027, 2, 9), 500m),
        };
        Assert.Equal(500m, GetPersonAccountStatementQueryHandler.CuotaRegular(plan, Hoy));
    }

    [Fact]
    public void Con_empate_gana_la_siguiente_por_vencer()
    {
        var plan = new[]
        {
            Cuota(1, new(2026, 10, 9), 400m),
            Cuota(2, new(2026, 12, 9), 450m),
        };
        Assert.Equal(450m, GetPersonAccountStatementQueryHandler.CuotaRegular(plan, Hoy));
    }

    [Fact]
    public void Con_empate_y_todas_vencidas_gana_la_ultima()
    {
        var plan = new[] { Cuota(1, new(2026, 9, 9), 400m), Cuota(2, new(2026, 10, 9), 450m) };
        Assert.Equal(450m, GetPersonAccountStatementQueryHandler.CuotaRegular(plan, Hoy));
    }

    [Fact]
    public void Sin_cuotas_regulares_es_nula()
    {
        Assert.Null(GetPersonAccountStatementQueryHandler.CuotaRegular([Cuota(1, new(2026, 12, 9), 10m, extra: true)], Hoy));
        Assert.Null(GetPersonAccountStatementQueryHandler.CuotaRegular([], Hoy));
    }
}
