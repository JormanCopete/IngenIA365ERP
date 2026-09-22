using FluentAssertions;
using IngenIA365ERP.Domain.Payroll.Settlements.Calendar;

namespace IngenIA365ERP.Domain.Tests.Payroll.Settlements.Calendar;

/// <summary>
/// FR-015 y research R6: el caso fijo de la spec. Del 28-10-2026 al 10-11-2026 hay 14
/// días calendario; con semana de lunes a sábado son 11 hábiles (se saltan los domingos
/// 01-11 y 08-11 y el festivo del lunes 02-11) y con lunes a viernes son 9 (se saltan
/// además los sábados 31-10 y 07-11). El festivo llega como dato: aquí no hay Ley 51.
/// </summary>
public class DiasHabilesTests
{
    private static readonly DateTime Desde = new(2026, 10, 28);
    private static readonly DateTime Hasta = new(2026, 11, 10);
    private static readonly DateTime Festivo = new(2026, 11, 2);

    [Fact]
    public void Lunes_a_sabado_cuenta_once_habiles_y_catorce_calendario()
    {
        var conteo = DiasHabiles.Contar(Desde, Hasta, SemanaLaboral.LunesASabado, [Festivo]);

        conteo.Habiles.Should().Be(11);
        conteo.Calendario.Should().Be(14);
        conteo.Saltados.Select(s => s.Fecha).Should().BeEquivalentTo(
        [
            new DateTime(2026, 11, 1), new DateTime(2026, 11, 2), new DateTime(2026, 11, 8),
        ]);
        conteo.Saltados.Single(s => s.Fecha == Festivo).Motivo.Should().Be("festivo");
        conteo.Saltados.Where(s => s.Fecha != Festivo).Should().OnlyContain(s => s.Motivo == "domingo");
    }

    [Fact]
    public void Lunes_a_viernes_cuenta_nueve_habiles_y_salta_los_sabados()
    {
        var conteo = DiasHabiles.Contar(Desde, Hasta, SemanaLaboral.LunesAViernes, [Festivo]);

        conteo.Habiles.Should().Be(9);
        conteo.Calendario.Should().Be(14);
        conteo.Saltados.Should().HaveCount(5);
        conteo.Saltados.Where(s => s.Motivo.StartsWith("sábado", StringComparison.Ordinal)).Select(s => s.Fecha)
            .Should().BeEquivalentTo([new DateTime(2026, 10, 31), new DateTime(2026, 11, 7)]);
    }

    [Fact]
    public void El_nombre_del_festivo_va_en_el_motivo_cuando_se_conoce()
    {
        var festivos = new Dictionary<DateTime, string> { [Festivo] = "Día de Todos los Santos" };

        var conteo = DiasHabiles.Contar(Desde, Hasta, SemanaLaboral.LunesASabado, festivos);

        conteo.Habiles.Should().Be(11);
        conteo.Saltados.Single(s => s.Fecha == Festivo).Motivo.Should().Be("festivo: Día de Todos los Santos");
    }

    [Fact]
    public void Un_festivo_en_domingo_se_salta_una_sola_vez_como_domingo()
    {
        // Si la cooperativa registra a mano un festivo que cae en domingo, no se cuenta dos veces.
        var conteo = DiasHabiles.Contar(Desde, Hasta, SemanaLaboral.LunesASabado, [new DateTime(2026, 11, 1)]);

        conteo.Habiles.Should().Be(12);
        conteo.Saltados.Should().HaveCount(2);
    }

    [Fact]
    public void Un_rango_invertido_no_cuenta_nada()
    {
        DiasHabiles.Contar(Hasta, Desde, SemanaLaboral.LunesASabado, []).Should().Be(new ConteoDeDiasHabiles(0, 0, []));
    }

    [Fact]
    public void Fin_de_habiles_devuelve_la_fecha_de_regreso()
    {
        // 11 hábiles desde el 28-10 con lunes a sábado terminan el 10-11; 9 con lunes a viernes, también.
        DiasHabiles.FinDeHabiles(Desde, 11, SemanaLaboral.LunesASabado, [Festivo]).Should().Be(Hasta);
        DiasHabiles.FinDeHabiles(Desde, 9, SemanaLaboral.LunesAViernes, [Festivo]).Should().Be(Hasta);
        DiasHabiles.FinDeHabiles(Desde, 0, SemanaLaboral.LunesASabado, []).Should().Be(Desde.AddDays(-1));
    }
}
