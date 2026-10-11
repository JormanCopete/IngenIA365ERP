using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;

namespace IngenIA365ERP.Domain.Tests.Lending.Attributions;

/// <summary>
/// Feature 013, T0241 (T63, R132, FR-037, FR-038, FR-040, FR-094; quickstart §1.4 y §2.7; SC-015): <see cref="EnrutadorDeAtribuciones"/>
/// y <see cref="NoExceptuables"/> coinciden con los cálculos hechos a mano de <c>Casos/</c>. El nivel efectivo es el mayor por rango
/// entre el techo de monto consolidado, las reglas que casan, el consejo forzado por un vinculado y el de excepción por puntaje bajo
/// o alertas; los niveles no se acumulan (la solicitud va sola a ese nivel) y la falta de capacidad, de autorización, la coincidencia
/// en listas y la usura nunca se exceptúan.
/// </summary>
public class CasosDoradosDeAtribucionesTests
{
    public static IEnumerable<object[]> Casos() =>
        CasoDoradoDeAtribuciones.Archivos().Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_casos_de_K1()
    {
        CasoDoradoDeAtribuciones.Archivos().Select(Path.GetFileNameWithoutExtension).Should().BeEquivalentTo(
        [
            "01-techo-de-monto-consolidado", "02-vinculado-va-al-consejo", "03-excepcion-por-puntaje-o-alertas", "04-reglas-por-condicion",
            "05-otros-sujetos-con-su-regla", "06-no-exceptuables",
            // US2 (T0365): los casos de aceptación de la originación.
            "us2-tres-montos", "us2-5-consolidado-sobre-gerencia", "us2-7-vinculado", "us2-puntaje-bajo", "us2-no-exceptuables",
        ]);
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_caso_coincide_con_el_calculo_a_mano(string archivo)
    {
        var caso = CasoDoradoDeAtribuciones.Cargar(Path.Combine(CasoDoradoDeAtribuciones.DirectorioDeCasos, archivo));
        caso.Escenarios.Should().NotBeEmpty($"{archivo} no trae escenarios");
        var matriz = caso.MatrizDelMotor();

        foreach (var escenario in caso.Escenarios)
        {
            var r = EnrutadorDeAtribuciones.Enrutar(escenario.Sujeto.DelMotor(), matriz);
            var e = escenario.Esperado;

            using (new AssertionScope($"{caso.Nombre} · {escenario.Nombre}"))
            {
                r.NivelEfectivo.Codigo.Should().Be(e.Nivel, "nivel efectivo");
                r.Razones.Select(x => (x.Clase, x.Nivel.Codigo)).Should()
                    .Equal(e.Razones.Select(x => (x.Clase, x.Nivel)), "razones, en el orden techo → reglas → vinculado → excepción");
                r.Razones.Should().OnlyContain(x => !string.IsNullOrWhiteSpace(x.Detalle), "cada razón se explica");
                r.NoExceptuables.Should().Equal(e.NoExceptuables, "motivos no exceptuables");
                r.EsExceptuable.Should().Be(e.NoExceptuables.Count == 0);
                // La razón ganadora es la del nivel efectivo: no hay un nivel por encima de él.
                r.Razones.Max(x => x.Nivel.Rango).Should().Be(r.NivelEfectivo.Rango);
            }
        }
    }

    [Fact]
    public void Los_cuatro_motivos_no_exceptuables_y_su_codigo_de_error()
    {
        NoExceptuables.CodigoDeError.Should().Be("Lending.Attribution.NotExceptionable");
        NoExceptuables.Evaluar([MotivoNoExceptuable.Usury, MotivoNoExceptuable.PaymentCapacity, MotivoNoExceptuable.Usury])
            .Should().Equal(MotivoNoExceptuable.PaymentCapacity, MotivoNoExceptuable.Usury);
        NoExceptuables.Evaluar([]).Should().BeEmpty();
        Enum.GetNames<MotivoNoExceptuable>().Should().Equal("PaymentCapacity", "HolderAuthorization", "RestrictiveListMatch", "Usury");
    }

    [Fact]
    public void Una_regla_que_nombra_un_nivel_inexistente_es_un_error_de_la_matriz()
    {
        var matriz = new MatrizDeAtribuciones(
            [new NivelDeAtribucion("CONSEJO", "Consejo", 10, AttributionLevelKind.Collegiate, null, true, true, null, "CONSEJO")],
            [new ReglaDeAtribucion(1, LendingDecisionSubjects.CreditApplication, "NOEXISTE")]);

        var accion = () => EnrutadorDeAtribuciones.Enrutar(new SujetoAEnrutar(LendingDecisionSubjects.CreditApplication, 1m), matriz);
        accion.Should().Throw<ArgumentException>().WithMessage("*NOEXISTE*");
    }

    [Fact]
    public void Una_matriz_sin_nivel_que_cubra_el_monto_es_un_error_de_la_matriz()
    {
        var matriz = new MatrizDeAtribuciones(
            [new NivelDeAtribucion("ANALISTA", "Analista", 10, AttributionLevelKind.Individual, 100m, true, true, "Lending.Decisions.Analyst", null)],
            []);

        var accion = () => EnrutadorDeAtribuciones.Enrutar(new SujetoAEnrutar(LendingDecisionSubjects.CreditApplication, 200m), matriz);
        accion.Should().Throw<InvalidOperationException>().WithMessage("*Lending.Attribution.MatrixWithoutTopLevel*");
    }

    [Fact]
    public void Un_sujeto_desconocido_se_rechaza()
    {
        var accion = () => EnrutadorDeAtribuciones.Enrutar(new SujetoAEnrutar("Prestamo", 1m), new MatrizDeAtribuciones([], []));
        accion.Should().Throw<ArgumentException>().WithMessage("*Prestamo*");
    }
}
