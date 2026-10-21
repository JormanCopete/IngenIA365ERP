using FluentAssertions;
using FluentAssertions.Execution;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Sessions;

namespace IngenIA365ERP.Domain.Tests.Lending.Sessions;

/// <summary>
/// Feature 013, T0240 (T64, R133, F7, C75; quickstart §1.4; SC-015): <see cref="EvaluadorDeVotacion"/> coincide con los cálculos
/// hechos a mano de <c>Casos/</c>: quórum sobre la composición vigente, mayoría ordinaria sobre los presentes elegibles o la
/// composición, la calificada de vinculados sobre la composición aunque haya excluidos (US2-7), el radicador excluido (US2-6), el
/// suplente sólo por un principal ausente (F7), el voto de reemplazo (C75) y la comparación en enteros.
/// </summary>
public class CasosDoradosDeVotacionTests
{
    public static IEnumerable<object[]> Casos() =>
        CasoDoradoDeVotacion.Archivos().Select(f => new object[] { Path.GetFileName(f) });

    [Fact]
    public void Estan_los_diez_casos_de_K1()
    {
        CasoDoradoDeVotacion.Archivos().Select(Path.GetFileNameWithoutExtension).Should().BeEquivalentTo(
        [
            "01-quorum-mayoria-absoluta", "02-mayoria-de-presentes-elegibles", "03-radicador-excluido", "04-vinculado-cuatro-quintos",
            "05-suplente-por-principal-ausente", "06-suplente-con-principal-presente", "07-voto-reemplazado", "08-sin-actividad-financiera",
            "09-fraccion-endurecida", "10-comparacion-en-enteros",
        ]);
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void El_caso_coincide_con_el_calculo_a_mano(string archivo)
    {
        var caso = CasoDoradoDeVotacion.Cargar(Path.Combine(CasoDoradoDeVotacion.DirectorioDeCasos, archivo));
        caso.Escenarios.Should().NotBeEmpty($"{archivo} no trae escenarios");

        foreach (var escenario in caso.Escenarios)
        {
            var r = EvaluadorDeVotacion.Evaluar(caso.ReglaDelMotor(), caso.MiembrosDelMotor(), escenario.AsistenciaDelMotor(),
                escenario.PuntoDelMotor());
            var e = escenario.Esperado;

            using (new AssertionScope($"{caso.Nombre} · {escenario.Nombre}"))
            {
                r.Quorum.Cumple.Should().Be(e.Quorum.Cumple, "quórum");
                r.Quorum.Composicion.Should().Be(e.Quorum.Composicion, "composición");
                r.Quorum.Requerido.Should().Be(e.Quorum.Requerido, "quórum requerido");
                r.Quorum.PresentesElegibles.Should().Be(e.Quorum.PresentesElegibles, "presentes elegibles");
                r.Elegibles.Should().Be(e.Elegibles, "elegibles del punto");
                r.Base.Should().Be(e.Base, "base de la mayoría");
                r.Requeridos.Should().Be(e.Requeridos, "votos a favor requeridos");
                r.AFavor.Should().Be(e.AFavor, "a favor");
                r.EnContra.Should().Be(e.EnContra, "en contra");
                r.Abstenciones.Should().Be(e.Abstenciones, "abstenciones");
                r.Resultado.Should().Be(e.Resultado, "resultado");
                r.Rechazados.Select(x => (x.Persona, x.Motivo)).Should()
                    .BeEquivalentTo(e.Rechazados.Select(x => (x.Persona, x.Motivo)), "votos rechazados");
                r.Explicacion.Should().NotBeEmpty();
                foreach (var frase in e.ExplicacionContiene)
                    r.TextoDeLaExplicacion.Should().Contain(frase);
            }
        }
    }

    [Fact]
    public void La_calificada_de_vinculados_sin_fraccion_sellada_es_un_error_de_quien_llama()
    {
        var regla = new ReglaDeVotacionSellada(new Fraccion(1, 2), FractionComparison.MoreThan, new Fraccion(1, 2), FractionComparison.MoreThan,
            MajorityBase.PresentEligible, Vinculados: null);
        var miembros = Enumerable.Range(101, 3).Select(p => new MiembroDelOrgano(p, true, true, null)).ToList();
        var asistencia = miembros.Select(m => new AsistenciaASesion(m.Persona, AttendanceStatus.Present)).ToList();
        var punto = new PuntoAVotar(VotingRule.RelatedPartyQualified, [], []);

        var accion = () => EvaluadorDeVotacion.Evaluar(regla, miembros, asistencia, punto);
        accion.Should().Throw<ArgumentException>().WithMessage("*vinculados*");
    }

    [Fact]
    public void Una_fraccion_invalida_se_rechaza()
    {
        var crear = () => new Fraccion(3, 0);
        crear.Should().Throw<ArgumentException>();
        var mayorQueUno = () => new Fraccion(3, 2);
        mayorQueUno.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Dos_votos_vigentes_de_la_misma_persona_son_un_error_de_quien_llama()
    {
        var regla = new ReglaDeVotacionSellada(new Fraccion(1, 2), FractionComparison.MoreThan, new Fraccion(1, 2), FractionComparison.MoreThan,
            MajorityBase.PresentEligible, null);
        var miembros = Enumerable.Range(101, 3).Select(p => new MiembroDelOrgano(p, true, true, null)).ToList();
        var asistencia = miembros.Select(m => new AsistenciaASesion(m.Persona, AttendanceStatus.Present)).ToList();
        var punto = new PuntoAVotar(VotingRule.Majority, [],
            [new VotoRegistrado(1, 101, VoteValue.For, null), new VotoRegistrado(2, 101, VoteValue.Against, null)]);

        var accion = () => EvaluadorDeVotacion.Evaluar(regla, miembros, asistencia, punto);
        accion.Should().Throw<ArgumentException>().WithMessage("*101*");
    }
}
