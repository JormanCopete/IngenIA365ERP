using FluentAssertions;
using IngenIA365ERP.Domain.Lending.Segregation;
using static IngenIA365ERP.Domain.Lending.Segregation.AccionesDeSegregacion;

namespace IngenIA365ERP.Domain.Tests.Lending.Segregation;

/// <summary>
/// Feature 013, T0242 (T66, FR-005, R135; quickstart §1.4): <see cref="ReglasDeSegregacion"/> compara el acto pedido con las
/// intervenciones previas del <b>mismo</b> expediente por <c>SEC_Users.Id</c> y, si los dos tienen persona, por persona. La regla
/// fija (quien radica no decide ni vota sobre lo suyo) siempre muerde; los pares configurables sólo si están activos.
/// </summary>
public class ReglasDeSegregacionTests
{
    private static readonly Guid Expediente = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtroExpediente = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTime Ayer = new(2026, 10, 7, 9, 15, 0);

    private static IntervencionPrevia Previa(string accion, int usuario, int? persona, Guid? expediente = null) =>
        new(expediente ?? Expediente, accion, usuario, persona, Ayer);

    private static ActoDeSegregacion Acto(string accion, int usuario, int? persona, Guid? expediente = null) =>
        new(expediente ?? Expediente, accion, usuario, persona);

    [Theory]
    [InlineData(nameof(Decidir))]
    [InlineData(nameof(Votar))]
    public void Quien_radica_no_decide_ni_vota_sobre_lo_suyo(string accion)
    {
        var conflicto = ReglasDeSegregacion.Evaluar(Acto(accion, 7, 70), [Previa(Radicar, 7, 70)], []);

        conflicto.Should().NotBeNull();
        conflicto!.Accion.Should().Be(accion);
        conflicto.AccionPrevia.Should().Be(Radicar);
        conflicto.InstantePrevio.Should().Be(Ayer);
        conflicto.EsReglaFija.Should().BeTrue();
        ReglasDeSegregacion.CodigoDeError.Should().Be("Lending.Segregation.Conflict");
    }

    [Fact]
    public void La_regla_fija_no_se_apaga_con_los_pares()
    {
        var pares = new[] { new ParIncompatible(Radicar, Decidir, Activo: false) };
        ReglasDeSegregacion.Evaluar(Acto(Decidir, 7, 70), [Previa(Radicar, 7, 70)], pares).Should().NotBeNull();
    }

    [Fact]
    public void La_misma_persona_con_dos_usuarios_entra_en_conflicto()
    {
        var conflicto = ReglasDeSegregacion.Evaluar(Acto(Decidir, 8, 70), [Previa(Radicar, 7, 70)], []);

        conflicto.Should().NotBeNull();
        conflicto!.PorPersona.Should().BeTrue();
        conflicto.UsuarioPrevio.Should().Be(7);
    }

    [Fact]
    public void Dos_usuarios_sin_persona_se_comparan_solo_por_usuario()
    {
        ReglasDeSegregacion.Evaluar(Acto(Decidir, 8, null), [Previa(Radicar, 7, null)], []).Should().BeNull();
        ReglasDeSegregacion.Evaluar(Acto(Decidir, 7, null), [Previa(Radicar, 7, null)], []).Should().NotBeNull();
        // Si sólo uno tiene persona, tampoco se compara por persona.
        ReglasDeSegregacion.Evaluar(Acto(Decidir, 8, 70), [Previa(Radicar, 7, null)], []).Should().BeNull();
    }

    [Fact]
    public void Personas_distintas_no_chocan()
    {
        ReglasDeSegregacion.Evaluar(Acto(Decidir, 8, 80), [Previa(Radicar, 7, 70)], ReglasDeSegregacion.ParesPorDefecto).Should().BeNull();
    }

    [Fact]
    public void El_par_Decidir_Desembolsar_esta_activo_por_defecto_y_muerde_en_los_dos_sentidos()
    {
        ReglasDeSegregacion.ParesPorDefecto.Should().ContainSingle()
            .Which.Should().Be(new ParIncompatible(Decidir, Desembolsar, Activo: true));

        var desembolsar = ReglasDeSegregacion.Evaluar(Acto(Desembolsar, 7, 70), [Previa(Decidir, 7, 70)], ReglasDeSegregacion.ParesPorDefecto);
        desembolsar.Should().NotBeNull();
        desembolsar!.AccionPrevia.Should().Be(Decidir);
        desembolsar.EsReglaFija.Should().BeFalse();

        ReglasDeSegregacion.Evaluar(Acto(Decidir, 7, 70), [Previa(Desembolsar, 7, 70)], ReglasDeSegregacion.ParesPorDefecto)
            .Should().NotBeNull();
    }

    [Fact]
    public void Un_par_inactivo_deja_pasar()
    {
        var pares = new[] { new ParIncompatible(Decidir, Desembolsar, Activo: false) };
        ReglasDeSegregacion.Evaluar(Acto(Desembolsar, 7, 70), [Previa(Decidir, 7, 70)], pares).Should().BeNull();
    }

    [Fact]
    public void Un_par_activo_no_muerde_entre_expedientes_distintos()
    {
        ReglasDeSegregacion.Evaluar(Acto(Desembolsar, 7, 70), [Previa(Decidir, 7, 70, OtroExpediente)], ReglasDeSegregacion.ParesPorDefecto)
            .Should().BeNull();
        ReglasDeSegregacion.Evaluar(Acto(Decidir, 7, 70), [Previa(Radicar, 7, 70, OtroExpediente)], [])
            .Should().BeNull("la regla fija también es por expediente");
    }

    [Fact]
    public void Repetir_un_acto_que_no_es_incompatible_consigo_mismo_pasa()
    {
        ReglasDeSegregacion.Evaluar(Acto(Analizar, 7, 70), [Previa(Analizar, 7, 70), Previa(Radicar, 9, 90)], ReglasDeSegregacion.ParesPorDefecto)
            .Should().BeNull();
    }

    [Fact]
    public void Con_varias_previas_incompatibles_se_informa_la_mas_antigua()
    {
        var primera = new IntervencionPrevia(Expediente, Radicar, 7, 70, new DateTime(2026, 10, 1, 8, 0, 0));
        var segunda = new IntervencionPrevia(Expediente, Desembolsar, 7, 70, new DateTime(2026, 10, 5, 8, 0, 0));
        var conflicto = ReglasDeSegregacion.Evaluar(Acto(Decidir, 7, 70), [segunda, primera], ReglasDeSegregacion.ParesPorDefecto);

        conflicto!.AccionPrevia.Should().Be(Radicar);
        conflicto.InstantePrevio.Should().Be(primera.En);
    }

    [Fact]
    public void Una_accion_desconocida_se_rechaza()
    {
        var accion = () => ReglasDeSegregacion.Evaluar(Acto("Firmar", 7, 70), [], []);
        accion.Should().Throw<ArgumentException>().WithMessage("*Firmar*");

        var par = () => ReglasDeSegregacion.Evaluar(Acto(Decidir, 7, 70), [], [new ParIncompatible("Firmar", Decidir, true)]);
        par.Should().Throw<ArgumentException>().WithMessage("*Firmar*");
    }
}
