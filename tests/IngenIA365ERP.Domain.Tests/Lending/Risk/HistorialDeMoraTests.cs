using FluentAssertions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Risk;

namespace IngenIA365ERP.Domain.Tests.Lending.Risk;

/// <summary>
/// Feature 013, US9 (T0673; T49, R57; FR-105, FR-139, FR-140): <see cref="HistorialDeMora.Construir"/>. La fuente que manda por mes
/// —cierre vigente sobre carga complementaria (la más reciente) sobre migración—, los meses posteriores al corte cubiertos por las
/// transacciones, las novaciones y refinanciaciones seguidas y la completitud con los meses que pide <c>ReglaDePerdidaEsperada</c>.
/// Las ventanas se contaron a mano en el calendario.
/// </summary>
public class HistorialDeMoraTests
{
    private static readonly DateOnly Corte = new(2026, 9, 30);

    private static EntradaDeHistorialDeMora E(int credito, int anio, int mes, int dias, ArrearsHistoryOrigin origen, long secuencia = 1, bool vigente = true) =>
        new(credito, anio, mes, dias, origen, secuencia) { Vigente = vigente };

    [Fact]
    public void Manda_el_cierre_vigente_sobre_la_carga_complementaria_mas_reciente_sobre_la_migracion()
    {
        var entradas = new[]
        {
            E(1, 2026, 7, 10, ArrearsHistoryOrigin.Migration),
            E(1, 2026, 7, 20, ArrearsHistoryOrigin.ComplementaryLoad, secuencia: 1),
            E(1, 2026, 7, 15, ArrearsHistoryOrigin.ComplementaryLoad, secuencia: 2),
            E(1, 2026, 8, 30, ArrearsHistoryOrigin.Migration),
            E(1, 2026, 8, 5, ArrearsHistoryOrigin.MonthlyClose),
            E(1, 2026, 9, 40, ArrearsHistoryOrigin.Migration),
            E(1, 2026, 9, 0, ArrearsHistoryOrigin.MonthlyClose, vigente: false),
        };

        var h = HistorialDeMora.Construir(new(1, new DateOnly(2026, 7, 1)), entradas, Corte, Corte, 3);

        h.Meses.Select(m => (m.Mes, m.MaxDiasDeMora, m.Origen)).Should().Equal(
            (7, (int?)15, (ArrearsHistoryOrigin?)ArrearsHistoryOrigin.ComplementaryLoad),
            (8, 5, ArrearsHistoryOrigin.MonthlyClose),
            (9, 40, ArrearsHistoryOrigin.Migration));
        h.Completo.Should().BeTrue();
    }

    [Fact]
    public void Los_meses_posteriores_al_corte_los_cubren_las_transacciones()
    {
        // Referencia: diciembre de 2026; se piden 6 meses (julio a diciembre). Migrado con corte en septiembre y filas de julio a
        // septiembre: octubre a diciembre los cubren las transacciones aunque no haya fila todavía.
        var entradas = new[] { 7, 8, 9 }.Select(m => E(1, 2026, m, 0, ArrearsHistoryOrigin.Migration));

        var h = HistorialDeMora.Construir(new(1, new DateOnly(2025, 1, 15)), entradas, Corte, new DateOnly(2026, 12, 31), 6);

        h.Meses.Should().HaveCount(6);
        h.Meses.Where(m => m.Mes >= 10).Should().OnlyContain(m => m.CubiertoPorTransacciones && m.MaxDiasDeMora == null);
        h.Meses.Where(m => m.Mes <= 9).Should().OnlyContain(m => !m.CubiertoPorTransacciones);
        h.Completo.Should().BeTrue();
    }

    [Fact]
    public void Lista_los_meses_que_faltan_antes_del_corte()
    {
        // 36 meses hasta septiembre de 2026 = octubre de 2023 a septiembre de 2026; el crédito trae sólo de enero a septiembre de 2026.
        var entradas = Enumerable.Range(1, 9).Select(m => E(1, 2026, m, 0, ArrearsHistoryOrigin.Migration));

        var h = HistorialDeMora.Construir(new(1, new DateOnly(2022, 3, 1)), entradas, Corte, Corte, 36);

        h.Meses.Should().HaveCount(36);
        h.Meses[0].Should().Match<MesDeHistorialDeMora>(m => m.Anio == 2023 && m.Mes == 10);
        h.Completo.Should().BeFalse();
        h.Faltantes.Should().HaveCount(27);
        h.Faltantes.First().Should().Match<MesDeHistorialDeMora>(m => m.Anio == 2023 && m.Mes == 10);
        h.Faltantes.Last().Should().Match<MesDeHistorialDeMora>(m => m.Anio == 2025 && m.Mes == 12);
        h.Explicacion.Texto.Should().Contain("Faltan 27 meses");
    }

    [Fact]
    public void Un_credito_mas_joven_que_la_ventana_se_mira_desde_su_desembolso()
    {
        var entradas = new[] { E(1, 2026, 8, 0, ArrearsHistoryOrigin.Migration), E(1, 2026, 9, 0, ArrearsHistoryOrigin.Migration) };

        var h = HistorialDeMora.Construir(new(1, new DateOnly(2026, 8, 10)), entradas, Corte, Corte, 36);

        h.Meses.Select(m => m.Mes).Should().Equal(8, 9);
        h.Completo.Should().BeTrue();
    }

    [Fact]
    public void Una_novacion_hereda_el_historial_de_su_antecesor_y_cuenta_la_mayor_mora()
    {
        // El 2 nace en agosto de 2026 por novación del 1 (desembolsado en mayo): mayo a julio vienen del 1; en agosto los dos tienen
        // fila y cuenta la mayor (25).
        var entradas = new[]
        {
            E(1, 2026, 5, 0, ArrearsHistoryOrigin.Migration), E(1, 2026, 6, 10, ArrearsHistoryOrigin.Migration),
            E(1, 2026, 7, 35, ArrearsHistoryOrigin.Migration), E(1, 2026, 8, 25, ArrearsHistoryOrigin.Migration),
            E(2, 2026, 8, 0, ArrearsHistoryOrigin.Migration), E(2, 2026, 9, 0, ArrearsHistoryOrigin.Migration),
            E(3, 2026, 4, 90, ArrearsHistoryOrigin.Migration),
        };

        var h = HistorialDeMora.Construir(new(2, new DateOnly(2026, 5, 1)) { Antecesores = [1] }, entradas, Corte, Corte, 6);

        h.Meses.Select(m => (m.Mes, m.MaxDiasDeMora, m.LoanIdDeLaFuente)).Should().Equal(
            (5, (int?)0, (int?)1), (6, 10, 1), (7, 35, 1), (8, 25, 1), (9, 0, 2));
        h.Completo.Should().BeTrue("el crédito 3 no es de la cadena y su fila no cuenta");
    }

    [Fact]
    public void Un_credito_nacido_en_Cartera_tiene_todos_sus_meses_cubiertos()
    {
        var h = HistorialDeMora.Construir(new(7, new DateOnly(2026, 1, 20)), [], null, Corte, 36);

        h.Meses.Should().HaveCount(9).And.OnlyContain(m => m.CubiertoPorTransacciones);
        h.Completo.Should().BeTrue();
    }

    [Fact]
    public void Los_meses_exigidos_son_mayores_que_cero()
    {
        var accion = () => HistorialDeMora.Construir(new(1, Corte), [], null, Corte, 0);
        accion.Should().Throw<ArgumentOutOfRangeException>();
    }
}
