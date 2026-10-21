using FluentAssertions;
using IngenIA365ERP.Domain.Common.Calendar;

namespace IngenIA365ERP.Domain.Tests.Common.Calendar;

/// <summary>
/// Feature 013, T0019 (T27, T40, R35; quickstart §1.4; SC-023): el calendario hábil puro. No sabe qué días son festivos
/// ni si el sábado es hábil: recibe la lista de no hábiles (los <c>FESTIVO</c> nacionales replicados) y los días de
/// descanso de la semana. Para la fecha exigible del art. 829 sólo el domingo descansa (pregunta D2); para los plazos del
/// titular, sábado y domingo. <c>Contar</c> va de a excluida a b incluida, como <c>ContadorDeDias</c>.
/// Las fechas de 2026 y 2027 salen de <see cref="FestivosLey51"/> (Reyes 2027 se traslada del miércoles 6 al lunes 11;
/// Todos los Santos 2026, del domingo 1 al lunes 2 de noviembre).
/// </summary>
public class CalendarioHabilTests
{
    private static readonly DateOnly[] Festivos =
        FestivosLey51.DelAño(2026).Concat(FestivosLey51.DelAño(2027)).Select(f => f.Fecha).ToArray();

    /// <summary>Lunes a viernes (plazos del titular).</summary>
    private static readonly CalendarioHabil LunesAViernes = new(Festivos, [DayOfWeek.Saturday, DayOfWeek.Sunday]);

    /// <summary>Lunes a sábado (art. 829, pregunta D2).</summary>
    private static readonly CalendarioHabil LunesASabado = new(Festivos, [DayOfWeek.Sunday]);

    private static DateOnly F(string iso) => DateOnly.Parse(iso);

    [Fact]
    public void Sabado_y_domingo_no_son_habiles_cuando_descansan()
    {
        LunesAViernes.EsHabil(F("2026-10-09")).Should().BeTrue("viernes común");
        LunesAViernes.EsHabil(F("2026-10-10")).Should().BeFalse("sábado");
        LunesAViernes.EsHabil(F("2026-10-11")).Should().BeFalse("domingo");
        LunesAViernes.EsHabil(F("2026-10-13")).Should().BeTrue("martes común");
    }

    [Fact]
    public void El_sabado_es_habil_si_solo_descansa_el_domingo()
    {
        LunesASabado.EsHabil(F("2026-10-10")).Should().BeTrue();
        LunesASabado.EsHabil(F("2026-10-11")).Should().BeFalse();
    }

    [Fact]
    public void El_festivo_trasladado_por_la_Ley_51_no_es_habil_y_su_fecha_original_si()
    {
        LunesAViernes.EsHabil(F("2026-11-16")).Should().BeFalse("Independencia de Cartagena, del miércoles 11 al lunes 16");
        LunesAViernes.EsHabil(F("2026-11-11")).Should().BeTrue("el miércoles 11 ya no es festivo");
        LunesAViernes.EsHabil(F("2026-11-02")).Should().BeFalse("Todos los Santos, del domingo 1 al lunes 2");
        LunesAViernes.EsHabil(F("2027-01-11")).Should().BeFalse("Reyes 2027, del miércoles 6 al lunes 11");
        LunesAViernes.EsHabil(F("2027-01-06")).Should().BeTrue();
    }

    [Fact]
    public void SiguienteHabil_desde_un_viernes_festivo()
    {
        LunesAViernes.SiguienteHabil(F("2026-04-03")).Should().Be(F("2026-04-06"), "Viernes Santo → lunes");
        LunesAViernes.SiguienteHabil(F("2026-08-07")).Should().Be(F("2026-08-10"), "Batalla de Boyacá (viernes) → lunes");
        LunesAViernes.SiguienteHabil(F("2026-12-25")).Should().Be(F("2026-12-28"));
        LunesASabado.SiguienteHabil(F("2026-04-03")).Should().Be(F("2026-04-04"), "con el sábado hábil, el sábado");
    }

    [Fact]
    public void SiguienteHabil_de_un_dia_habil_es_el_mismo_dia()
    {
        LunesAViernes.SiguienteHabil(F("2026-10-13")).Should().Be(F("2026-10-13"));
    }

    [Fact]
    public void SiguienteHabil_salta_un_puente_de_fin_de_semana_y_festivo()
    {
        LunesAViernes.SiguienteHabil(F("2026-10-31")).Should().Be(F("2026-11-03"), "sábado, domingo y el lunes 2 festivo");
    }

    [Fact]
    public void Contar_va_de_a_excluida_a_b_incluida()
    {
        LunesAViernes.Contar(F("2026-10-30"), F("2026-11-06")).Should().Be(4, "el 31, el 1 y el 2 no cuentan; del 3 al 6 sí");
        LunesAViernes.Contar(F("2026-10-13"), F("2026-10-13")).Should().Be(0);
        LunesAViernes.Contar(F("2026-10-12"), F("2026-10-13")).Should().Be(1, "el 12 (festivo) queda fuera por ser a; el 13 cuenta");
    }

    [Fact]
    public void Contar_reproduce_el_caso_fijo_de_la_nomina_con_el_sabado_habil()
    {
        // 28-10 a 10-11-2026 inclusive = 11 hábiles de lunes a sábado (caso de la 010); aquí a es la víspera.
        LunesASabado.Contar(F("2026-10-27"), F("2026-11-10")).Should().Be(11);
    }

    [Fact]
    public void Contar_un_rango_que_cruza_el_anio()
    {
        // Del 25-12-2026 al 12-01-2027: no cuentan Navidad, Año Nuevo, Reyes trasladado (11-01) ni los fines de semana.
        LunesAViernes.Contar(F("2026-12-24"), F("2027-01-12")).Should().Be(10);
    }

    [Fact]
    public void Contar_con_b_antes_de_a_se_rechaza()
    {
        var acto = () => LunesAViernes.Contar(F("2026-10-13"), F("2026-10-12"));
        acto.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SumarHabiles_cuenta_desde_el_dia_siguiente()
    {
        LunesAViernes.SumarHabiles(F("2026-10-30"), 3).Should().Be(F("2026-11-05"));
        LunesAViernes.SumarHabiles(F("2026-12-24"), 10).Should().Be(F("2027-01-12"));
        LunesAViernes.SumarHabiles(F("2026-10-13"), 0).Should().Be(F("2026-10-13"));
    }

    [Fact]
    public void Una_semana_sin_dias_habiles_se_rechaza()
    {
        var acto = () => new CalendarioHabil([], Enum.GetValues<DayOfWeek>());
        acto.Should().Throw<ArgumentException>();
    }
}
