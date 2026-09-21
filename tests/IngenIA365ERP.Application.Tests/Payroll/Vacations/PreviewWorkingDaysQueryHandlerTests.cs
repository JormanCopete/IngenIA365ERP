using FluentAssertions;
using IngenIA365ERP.Application.Payroll.Vacations;
using IngenIA365ERP.Domain.Payroll.Policies;
using IngenIA365ERP.Domain.Payroll.Settlements.Calendar;

namespace IngenIA365ERP.Application.Tests.Payroll.Vacations;

/// <summary>
/// Feature 010 US4 (T056, FR-015): la vista previa obligatoria antes de guardar. El caso fijo de la
/// spec: 28-10-2026 a 10-11-2026 son 14 días calendario, 11 hábiles de lunes a sábado (saltan los
/// domingos 01-11 y 08-11 y el festivo del 02-11) y 9 de lunes a viernes (saltan además los sábados
/// 31-10 y 07-11). La semana laboral es una política con vigencia: cambiarla cambia el número.
/// </summary>
public class PreviewWorkingDaysQueryHandlerTests
{
    private static readonly DateOnly Desde = new(2026, 10, 28);
    private static readonly DateOnly Hasta = new(2026, 11, 10);

    [Fact]
    public async Task De_lunes_a_sabado_son_11_habiles_de_14_calendario_con_los_saltados_explicados()
    {
        var d = VacacionesDePrueba.Escenario();

        var r = await new PreviewWorkingDaysQueryHandler(d.Db, d.Policies).Handle(new PreviewWorkingDaysQuery(Desde, Hasta), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.WorkingDays.Should().Be(11);
        r.Value.CalendarDays.Should().Be(14);
        r.Value.WorkWeek.Should().Be(SemanaLaboral.LunesASabado);
        r.Value.Skipped.Select(s => (s.Date, s.Reason)).Should().BeEquivalentTo(
        [
            (new DateOnly(2026, 11, 1), "Sunday"),
            (new DateOnly(2026, 11, 2), "Holiday:Todos los Santos"),
            (new DateOnly(2026, 11, 8), "Sunday"),
        ]);
    }

    [Fact]
    public async Task De_lunes_a_viernes_son_9_y_la_politica_manda_por_vigencia()
    {
        var d = VacacionesDePrueba.Escenario();
        d.Politica(CompanyPolicyKeys.SemanaLaboral, CompanyPolicyKeys.SemanaLaboralValores.LunesAViernes, new DateOnly(2026, 10, 1));

        var conPolitica = await new PreviewWorkingDaysQueryHandler(d.Db, d.Policies).Handle(new PreviewWorkingDaysQuery(Desde, Hasta), CancellationToken.None);
        var antesDeLaVigencia = await new PreviewWorkingDaysQueryHandler(d.Db, d.Policies).Handle(new PreviewWorkingDaysQuery(new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 3)), CancellationToken.None);

        conPolitica.Value.WorkingDays.Should().Be(9);
        conPolitica.Value.WorkWeek.Should().Be(SemanaLaboral.LunesAViernes);
        conPolitica.Value.Skipped.Where(s => s.Reason == "Saturday").Select(s => s.Date).Should().BeEquivalentTo([new DateOnly(2026, 10, 31), new DateOnly(2026, 11, 7)]);
        antesDeLaVigencia.Value.WorkWeek.Should().Be(SemanaLaboral.LunesASabado, "la vigencia empieza el 01-10: una semana de septiembre sigue de lunes a sábado");
        antesDeLaVigencia.Value.WorkingDays.Should().Be(6, "del lunes 28-09 al sábado 03-10 son seis hábiles: el sábado cuenta porque a esa fecha la semana era de lunes a sábado");
    }

    [Fact]
    public async Task Fechas_al_reves_responden_DatesInvalid_y_todo_festivo_o_domingo_da_cero_habiles()
    {
        var d = VacacionesDePrueba.Escenario();
        var handler = new PreviewWorkingDaysQueryHandler(d.Db, d.Policies);

        var alReves = await handler.Handle(new PreviewWorkingDaysQuery(Hasta, Desde), CancellationToken.None);
        var domingoYFestivo = await handler.Handle(new PreviewWorkingDaysQuery(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 2)), CancellationToken.None);

        alReves.IsFailure.Should().BeTrue();
        alReves.Error.Code.Should().Be("Payroll.Vacation.DatesInvalid");
        domingoYFestivo.Value.WorkingDays.Should().Be(0);
        domingoYFestivo.Value.CalendarDays.Should().Be(2);
    }
}
