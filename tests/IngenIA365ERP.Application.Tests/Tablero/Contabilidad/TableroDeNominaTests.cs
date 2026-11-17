using FluentAssertions;
using IngenIA365ERP.Application.Tablero;
using IngenIA365ERP.Application.Tablero.Contabilidad;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Application.Tests.Tablero.Contabilidad;

/// <summary>2026-11-17 (pedido del dueño): las cifras y pendientes de Nómina en el tablero de inicio.</summary>
public class TableroDeNominaTests
{
    private readonly NominaTestData _d = new();

    private static ContextoDeIndicador Contexto() => new(false, 7, "ana@demo", new DateOnly(2026, 3, 20));

    [Fact]
    public async Task La_proxima_nomina_sin_calcular_dice_el_periodo_y_los_empleados_activos()
    {
        _d.Empleado("Carlos", 1_500_000m, new DateTime(2025, 6, 1));
        _d.Empleado("Beatriz", 1_500_000m, new DateTime(2024, 6, 1), retiro: new DateTime(2026, 1, 31)); // retirada: no cuenta

        var r = await new ProximaNomina(_d.Db).CalcularAsync(Contexto(), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        r.Value.Valor.Should().BeNull();
        r.Value.Nota.Should().Contain("01/03 a 31/03/2026").And.Contain("sin calcular").And.Contain("2 empleados activos");
    }

    [Fact]
    public async Task La_proxima_nomina_calculada_muestra_el_devengado_de_la_ultima_version()
    {
        var abril = _d.Periodo(new DateTime(2026, 4, 1), new DateTime(2026, 4, 30), PayPeriodStatus.Open);
        var v1 = _d.Borrador(_d.Marzo);
        v1.Status = PayrollRunStatus.Superseded; v1.TotalEarnings = 1_000m; v1.EmployeeCount = 1;
        var v2 = _d.Borrador(_d.Marzo, version: 2);
        v2.TotalEarnings = 2_500_000m; v2.EmployeeCount = 1;
        var deAbril = _d.Borrador(abril);
        deAbril.TotalEarnings = 9_999m;
        await _d.Db.SaveChangesAsync();

        var r = await new ProximaNomina(_d.Db).CalcularAsync(Contexto(), CancellationToken.None);

        r.Value.Valor.Should().Be(2_500_000m, "marzo empieza primero y su vigente es la versión 2");
        r.Value.Nota.Should().Contain("1 liquidados").And.Contain("1 empleado activo");
    }

    [Fact]
    public async Task Sin_periodo_abierto_la_cifra_va_vacia()
    {
        _d.Marzo.Status = PayPeriodStatus.Approved;
        await _d.Db.SaveChangesAsync();

        var r = await new ProximaNomina(_d.Db).CalcularAsync(Contexto(), CancellationToken.None);

        r.Value.Valor.Should().BeNull();
        r.Value.Nota.Should().Be("No hay un período de nómina abierto.");
    }

    [Fact]
    public async Task Por_aprobar_cuenta_las_ordinarias_en_borrador()
    {
        var abril = _d.Periodo(new DateTime(2026, 4, 1), new DateTime(2026, 4, 30), PayPeriodStatus.Open);
        var mayo = _d.Periodo(new DateTime(2026, 5, 1), new DateTime(2026, 5, 31), PayPeriodStatus.Open);
        _d.Borrador(_d.Marzo);
        _d.Borrador(abril).Status = PayrollRunStatus.Stale;   // desactualizada: se recalcula antes de aprobar
        _d.Borrador(mayo).Status = PayrollRunStatus.Approved;
        await _d.Db.SaveChangesAsync();

        (await new NominasPorAprobar(_d.Db).ContarAsync(Contexto(), CancellationToken.None)).Should().Be(1);
    }
}
