using FluentAssertions;
using IngenIA365ERP.Application.Payroll.Vacations;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Payroll;

namespace IngenIA365ERP.Application.Tests.Payroll.Vacations;

/// <summary>
/// Feature 010 US4 (T056): el saldo derivado de vacaciones. Ana ingresó el 15-01-2025; al 14-07-2026
/// lleva 540 días comerciales → 540 × 15 / 360 = 22,5 hábiles causados (el 15 sale del parámetro
/// VACACIONES_DIAS_ANIO de la semilla, no del código). Con saldo inicial se cuenta desde el día
/// siguiente al corte digitado; una suspensión del contrato descuenta sus días (CST art. 53).
/// </summary>
public class VacationBalanceCalculatorTests
{
    [Fact]
    public async Task Quinientos_cuarenta_dias_dan_22_5_habiles_causados_y_pendientes()
    {
        var d = VacacionesDePrueba.Escenario();

        var saldo = await VacacionesDePrueba.Calculador(d).CalcularAsync(d.Ana, VacacionesDePrueba.CorteDe540Dias, CancellationToken.None);

        saldo.IsSuccess.Should().BeTrue(saldo.Error.Message);
        saldo.Value.WorkedDays.Should().Be(540);
        saldo.Value.AccruedDays.Should().Be(22.5m);
        saldo.Value.PendingDays.Should().Be(22.5m);
        saldo.Value.OpeningDays.Should().Be(0m);
        saldo.Value.Explanation.Should().Contain(s => s.Label.Contains("540") && s.Label.Contains("VACACIONES_DIAS_ANIO"));
    }

    [Fact]
    public async Task El_saldo_inicial_entra_y_la_causacion_sigue_desde_el_dia_siguiente_al_corte_digitado()
    {
        var d = VacacionesDePrueba.Escenario();
        d.SaldoInicial(d.Ana, new DateOnly(2025, 12, 31), diasVacaciones: 5m);

        var saldo = await VacacionesDePrueba.Calculador(d).CalcularAsync(d.Ana, VacacionesDePrueba.CorteDe540Dias, CancellationToken.None);

        saldo.IsSuccess.Should().BeTrue(saldo.Error.Message);
        // 01-01-2026 a 14-07-2026 = 194 días comerciales → 194 × 15 / 360 = 8,0833.
        saldo.Value.WorkedDays.Should().Be(194);
        saldo.Value.AccruedDays.Should().BeApproximately(8.0833m, 0.001m);
        saldo.Value.OpeningDays.Should().Be(5m);
        saldo.Value.PendingDays.Should().Be(saldo.Value.AccruedDays + 5m);
        saldo.Value.Explanation.Should().Contain(s => s.Label == "Saldo inicial" && s.Text!.Contains("contadora@demo"));
    }

    [Fact]
    public async Task Una_suspension_del_contrato_descuenta_sus_dias_y_los_movimientos_restan_por_tipo()
    {
        var d = VacacionesDePrueba.Escenario();
        VacacionesDePrueba.Suspension(d, d.Ana, d.Marzo, new DateTime(2026, 3, 1), new DateTime(2026, 3, 30));
        d.Db.VacationMovements.AddRange(
            new VacationMovement { EmployeeId = d.Ana.Id, Kind = VacationMovementKind.Enjoyment, StartDate = new DateOnly(2026, 2, 2), EndDate = new DateOnly(2026, 2, 7), BusinessDays = 6m, CalendarDays = 6, Status = VacationMovementStatus.Liquidated, CreatedBy = "test" },
            new VacationMovement { EmployeeId = d.Ana.Id, Kind = VacationMovementKind.Compensation, StartDate = new DateOnly(2026, 4, 1), BusinessDays = 2m, Status = VacationMovementStatus.Registered, CreatedBy = "test" },
            new VacationMovement { EmployeeId = d.Ana.Id, Kind = VacationMovementKind.Adjustment, StartDate = new DateOnly(2026, 5, 1), BusinessDays = 1.5m, Status = VacationMovementStatus.Registered, Notes = "Acuerdo", CreatedBy = "test" },
            new VacationMovement { EmployeeId = d.Ana.Id, Kind = VacationMovementKind.Enjoyment, StartDate = new DateOnly(2026, 6, 1), EndDate = new DateOnly(2026, 6, 6), BusinessDays = 6m, CalendarDays = 6, Status = VacationMovementStatus.Cancelled, CancelReason = "error", CreatedBy = "test" });
        await d.Db.SaveChangesAsync();

        var saldo = await VacacionesDePrueba.Calculador(d).CalcularAsync(d.Ana, VacacionesDePrueba.CorteDe540Dias, CancellationToken.None);

        saldo.IsSuccess.Should().BeTrue(saldo.Error.Message);
        saldo.Value.SuspensionDays.Should().Be(30);
        saldo.Value.WorkedDays.Should().Be(510);
        saldo.Value.AccruedDays.Should().Be(510m * 15m / 360m);
        saldo.Value.EnjoyedDays.Should().Be(6m, "el disfrute anulado no cuenta");
        saldo.Value.CompensatedDays.Should().Be(2m);
        saldo.Value.AdjustedDays.Should().Be(1.5m);
        saldo.Value.PendingDays.Should().Be(saldo.Value.AccruedDays - 6m - 2m + 1.5m);
        saldo.Value.LastEnjoymentTo.Should().Be(new DateOnly(2026, 2, 7));
    }

    [Fact]
    public async Task Sin_vigencia_del_parametro_de_dias_por_anio_responde_ParametersMissing_en_vez_de_inventar()
    {
        var d = VacacionesDePrueba.Escenario();
        foreach (var p in d.Db.PayrollLegalParameters.Where(p => p.Code == "VACACIONES_DIAS_ANIO")) p.ValidTo = new DateTime(2020, 1, 1);
        await d.Db.SaveChangesAsync();

        var saldo = await VacacionesDePrueba.Calculador(d).CalcularAsync(d.Ana, VacacionesDePrueba.CorteDe540Dias, CancellationToken.None);

        saldo.IsFailure.Should().BeTrue();
        saldo.Error.Code.Should().Be("Payroll.Settlement.ParametersMissing");
        saldo.Error.Message.Should().Contain("VACACIONES_DIAS_ANIO");
    }

    [Fact]
    public async Task El_maximo_compensable_es_el_porcentaje_del_parametro_sobre_lo_causado()
    {
        var d = VacacionesDePrueba.Escenario();
        var calculador = VacacionesDePrueba.Calculador(d);
        var saldo = await calculador.CalcularAsync(d.Ana, VacacionesDePrueba.CorteDe540Dias, CancellationToken.None);

        var maximo = await calculador.MaximoCompensableAsync(saldo.Value, CancellationToken.None);

        maximo.IsSuccess.Should().BeTrue(maximo.Error.Message);
        maximo.Value.MaxDays.Should().Be(11.25m, "50 % de 22,5 (VACACIONES_COMPENSABLE_PCT de la semilla)");
    }
}
