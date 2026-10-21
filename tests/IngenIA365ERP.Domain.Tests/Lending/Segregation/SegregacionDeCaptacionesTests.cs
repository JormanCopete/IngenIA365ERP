using FluentAssertions;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.Segregation;
using static IngenIA365ERP.Domain.Lending.Segregation.AccionesDeSegregacion;

namespace IngenIA365ERP.Domain.Tests.Lending.Segregation;

/// <summary>
/// Feature 014, US16 (T163; FR-005; api.md §1.7): el catálogo de la segregación de captaciones. Cada expediente de FR-005 tiene su par —quien registra no
/// aprueba ni levanta—, la comparación es por persona y la raíz de los expedientes que cuelgan de un recurso ajeno es derivada, así que abrir una cuenta no
/// impide condonar sus cargos. (nuevo)
/// </summary>
public class SegregacionDeCaptacionesTests
{
    private static readonly Guid Cuenta = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTime Ayer = new(2026, 10, 18, 9, 0, 0);

    [Fact]
    public void FR_005_cubre_los_siete_actos_de_la_spec_y_los_dos_de_la_014()
    {
        SegregacionDeCaptaciones.Expedientes.Should().BeEquivalentTo(
        [
            "MemberAccount", "ContributionRequest", "MembershipWithdrawal", "MembershipWithdrawalRefund", "AccountCharge", "Garnishment", "NegotiatedRate",
            "DeceasedHolderCase", "AssemblyDistribution",
        ]);
        SegregacionDeCaptaciones.Pares.Should().OnlyContain(p => p.Registra == Radicar && p.Aprueba == Decidir,
            "todos los pares de FR-005 son la regla fija de la 013: no se apagan con el parámetro");
        SegregacionDeCaptaciones.Pares.Select(p => p.Expediente).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Cada_par_es_la_regla_fija_que_no_se_apaga()
    {
        foreach (var par in SegregacionDeCaptaciones.Pares)
            ReglasDeSegregacion.ReglaFija.Should().Contain(p => p.Junta(par.Registra, par.Aprueba), par.Expediente);
    }

    [Theory]
    [InlineData(SegregacionDeCaptaciones.Embargo)]
    [InlineData(SegregacionDeCaptaciones.EntregaPorFallecimiento)]
    [InlineData(SegregacionDeCaptaciones.SolicitudDeAportes)]
    [InlineData(SegregacionDeCaptaciones.DecisionDeAsamblea)]
    [InlineData(SegregacionDeCaptaciones.TasaNegociada)]
    [InlineData(SegregacionDeCaptaciones.AperturaDeCuenta)]
    public void Quien_registra_no_aprueba_aunque_entre_con_otro_usuario_de_la_misma_persona(string expediente)
    {
        var raiz = SegregacionDeCaptaciones.Raiz(expediente, Cuenta);
        var previa = new IntervencionPrevia(raiz, Radicar, UsuarioId: 7, PersonaId: 70, Ayer);

        var mismoUsuario = SegregacionDeCaptaciones.Evaluar(expediente, new ActoDeSegregacion(raiz, Decidir, 7, 70), [previa], []);
        var otroUsuarioMismaPersona = SegregacionDeCaptaciones.Evaluar(expediente, new ActoDeSegregacion(raiz, Decidir, 8, 70), [previa], []);
        var otraPersona = SegregacionDeCaptaciones.Evaluar(expediente, new ActoDeSegregacion(raiz, Decidir, 9, 90), [previa], []);

        mismoUsuario.Should().NotBeNull();
        otroUsuarioMismaPersona.Should().NotBeNull("la comparación es por persona: dos usuarios de una persona no se escapan");
        otroUsuarioMismaPersona!.PorPersona.Should().BeTrue();
        otraPersona.Should().BeNull();
    }

    [Fact]
    public void Abrir_la_cuenta_no_impide_condonar_sus_cargos_porque_la_raiz_de_los_cargos_es_derivada()
    {
        var apertura = SegregacionDeCaptaciones.Raiz(SegregacionDeCaptaciones.AperturaDeCuenta, Cuenta);
        var cargos = SegregacionDeCaptaciones.Raiz(SegregacionDeCaptaciones.CargosDeCuenta, Cuenta);

        apertura.Should().Be(Cuenta, "la apertura es dueña de la cuenta");
        cargos.Should().NotBe(Cuenta);
        cargos.Should().Be(SegregacionDeCaptaciones.Raiz(SegregacionDeCaptaciones.CargosDeCuenta, Cuenta), "la raíz derivada es fija");

        var abrio = new IntervencionPrevia(apertura, Radicar, 7, 70, Ayer);
        SegregacionDeCaptaciones.Evaluar(SegregacionDeCaptaciones.CargosDeCuenta, new ActoDeSegregacion(cargos, Decidir, 7, 70), [abrio], [])
            .Should().BeNull("quien abrió la cuenta sí condona un cargo que registró otra persona");
        SegregacionDeCaptaciones.Evaluar(SegregacionDeCaptaciones.CargosDeCuenta, new ActoDeSegregacion(cargos, Decidir, 7, 70),
                [new IntervencionPrevia(cargos, Radicar, 7, 70, Ayer)], [])
            .Should().NotBeNull("quien registró un cargo de la cuenta no lo condona");
    }

    [Fact]
    public void La_raiz_de_la_devolucion_del_retiro_es_la_de_siempre()
    {
        var retiro = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var esperada = new Guid(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"MembershipWithdrawalRefund:{retiro:N}")).AsSpan(0, 16));

        SegregacionDeCaptaciones.Raiz(SegregacionDeCaptaciones.DevolucionDelRetiro, retiro).Should().Be(esperada,
            "las intervenciones ya registradas desde US10 siguen en la misma raíz");
        SegregacionDeCaptaciones.Raiz(SegregacionDeCaptaciones.RetiroDeAsociado, retiro).Should().Be(retiro);
    }

    [Fact]
    public void Un_expediente_o_un_acto_ajeno_al_catalogo_se_rechaza()
    {
        var acto = new ActoDeSegregacion(Cuenta, Votar, 1, 1);

        FluentActions.Invoking(() => SegregacionDeCaptaciones.Raiz("Loan", Cuenta)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => SegregacionDeCaptaciones.Evaluar(SegregacionDeCaptaciones.Embargo, acto, [], [])).Should().Throw<ArgumentException>();
        SegregacionDeCaptaciones.Existe("MemberAccount").Should().BeTrue();
        SegregacionDeCaptaciones.Existe("Loan").Should().BeFalse();
    }

    [Fact]
    public void Las_entregas_de_los_expedientes_futuros_son_las_de_sus_historias()
    {
        SegregacionDeCaptaciones.ParDe(SegregacionDeCaptaciones.Embargo).Entrega.Should().Be(EntregaDeCartera.K8);
        SegregacionDeCaptaciones.ParDe(SegregacionDeCaptaciones.TasaNegociada).Entrega.Should().Be(EntregaDeCartera.K9);
        SegregacionDeCaptaciones.ParDe(SegregacionDeCaptaciones.EntregaPorFallecimiento).Entrega.Should().Be(EntregaDeCartera.K10);
        SegregacionDeCaptaciones.ParDe(SegregacionDeCaptaciones.DecisionDeAsamblea).Entrega.Should().Be(EntregaDeCartera.K10);
    }
}
