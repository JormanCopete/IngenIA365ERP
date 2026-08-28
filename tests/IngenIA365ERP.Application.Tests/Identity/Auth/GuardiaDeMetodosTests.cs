using FluentAssertions;
using IngenIA365ERP.Application.Identity.Auth.Common;
using IngenIA365ERP.Domain.Entities.Admin;
using Xunit;
using static IngenIA365ERP.Application.Identity.Auth.Common.GuardiaDeMetodos;

namespace IngenIA365ERP.Application.Tests.Identity.Auth;

/// <summary>
/// La regla que decide si el método con el que alguien entró le sirve para una
/// cooperativa. Es pura, así que se puede probar entera — y conviene, porque las
/// siete puertas del sistema la comparten: un fallo aquí no es un fallo, son siete.
/// </summary>
public class GuardiaDeMetodosTests
{
    private const MetodosMfa Todos = ConversionDeMetodosMfa.Todos;

    // ---------- El caso que da nombre a la etapa ----------

    [Fact]
    public void Con_TOTP_en_una_cooperativa_que_solo_acepta_passkeys_falta_inscribir()
    {
        Evaluar(cooperativaExigeMfa: true, MetodosMfa.WebAuthn, MetodosMfa.Totp)
            .Should().Be(Veredicto.FaltaInscribir);
    }

    [Fact]
    public void Con_passkey_en_una_cooperativa_que_solo_acepta_passkeys_entra()
    {
        Evaluar(cooperativaExigeMfa: true, MetodosMfa.WebAuthn, MetodosMfa.WebAuthn)
            .Should().Be(Veredicto.Admite);
    }

    // ---------- La máscara sólo muerde cuando se exige ----------

    /// <summary>
    /// Sin esta condición, una cooperativa que no exige segundo factor rechazaría a
    /// TODOS sus miembros: ninguno tiene método sellado, y Ninguno no está en
    /// ninguna máscara. Sería el fallo más grande posible y el más fácil de escribir.
    /// </summary>
    [Theory]
    [InlineData(MetodosMfa.Ninguno)]
    [InlineData(MetodosMfa.Totp)]
    [InlineData(MetodosMfa.WebAuthn)]
    public void Si_la_cooperativa_no_exige_MFA_entra_con_lo_que_sea(MetodosMfa demostrado)
    {
        Evaluar(cooperativaExigeMfa: false, MetodosMfa.WebAuthn, demostrado)
            .Should().Be(Veredicto.Admite);
    }

    // ---------- El día del despliegue ----------

    /// <summary>
    /// Toda sesión viva el día del despliegue trae «no consta», porque se emitió
    /// antes de que el dato existiera. Si una máscara que lo acepta todo las
    /// rechazara, el despliegue expulsaría a la vez a todo el mundo con segundo
    /// factor — sin que ninguna cooperativa hubiera cambiado nada.
    /// </summary>
    [Fact]
    public void Una_mascara_que_acepta_todo_admite_a_quien_no_trae_el_dato()
    {
        Evaluar(cooperativaExigeMfa: true, Todos, MetodosMfa.Ninguno)
            .Should().Be(Veredicto.Admite);
    }

    [Fact]
    public void Pero_una_mascara_restrictiva_no_admite_a_quien_no_trae_el_dato()
    {
        Evaluar(cooperativaExigeMfa: true, MetodosMfa.Totp, MetodosMfa.Ninguno)
            .Should().Be(Veredicto.FaltaInscribir);
    }

    // ---------- La trampa de las máscaras de bits ----------

    /// <summary>
    /// <c>HasFlag(Ninguno)</c> es SIEMPRE true: todo número contiene el cero.
    /// Escrito con HasFlag, quien no tiene método pasaría todas las puertas y el
    /// sistema entero quedaría abierto sin que fallara ninguna prueba de las de
    /// arriba — todas usan métodos reales.
    /// </summary>
    [Fact]
    public void Ninguno_no_satisface_ninguna_mascara_restrictiva()
    {
        foreach (var mascara in new[] { MetodosMfa.Totp, MetodosMfa.WebAuthn })
        {
            mascara.HasFlag(MetodosMfa.Ninguno)
                .Should().BeTrue("es la trampa: HasFlag diría que sí");

            Evaluar(cooperativaExigeMfa: true, mascara, MetodosMfa.Ninguno)
                .Should().Be(Veredicto.FaltaInscribir, "y la regla tiene que decir que no");
        }
    }

    // ---------- Varias credenciales ----------

    [Fact]
    public void Quien_tiene_los_dos_metodos_entra_por_el_que_uso()
    {
        // Sella lo que USÓ, no lo que tiene. Si sellara lo que tiene, quien
        // conserva un TOTP entraría a una cooperativa sólo-passkey usando el TOTP.
        Evaluar(true, MetodosMfa.WebAuthn, MetodosMfa.Totp).Should().Be(Veredicto.FaltaInscribir);
        Evaluar(true, MetodosMfa.WebAuthn, MetodosMfa.WebAuthn).Should().Be(Veredicto.Admite);
    }

    // ---------- Qué se le ofrece inscribir ----------

    [Fact]
    public void Se_le_ofrece_la_union_de_lo_que_aceptan_las_exigentes()
    {
        var cooperativas = new[]
        {
            (ExigeMfa: true, Aceptados: MetodosMfa.WebAuthn),
            (ExigeMfa: true, Aceptados: MetodosMfa.Totp),
        };

        LoQueLeServiria(cooperativas).Should().Be(Todos);
    }

    [Fact]
    public void Las_que_no_exigen_no_cuentan_para_lo_que_se_le_ofrece()
    {
        var cooperativas = new[]
        {
            (ExigeMfa: true, Aceptados: MetodosMfa.WebAuthn),
            (ExigeMfa: false, Aceptados: MetodosMfa.Totp),
        };

        LoQueLeServiria(cooperativas).Should().Be(MetodosMfa.WebAuthn);
    }

    /// <summary>
    /// Sin ninguna exigente no hay nada que imponer, pero la pantalla igual tiene
    /// que ofrecer algo: devolver Ninguno la dejaría sin un solo botón que pulsar.
    /// </summary>
    [Fact]
    public void Sin_cooperativas_exigentes_se_le_ofrece_todo()
    {
        LoQueLeServiria([(false, MetodosMfa.Totp)]).Should().Be(Todos);
        LoQueLeServiria([]).Should().Be(Todos);
    }

    // ---------- La conversión entre los dos vocabularios ----------

    [Fact]
    public void Los_discriminadores_de_la_tabla_se_convierten_a_metodos()
    {
        ConversionDeMetodosMfa.DesdeTipoDeCredencial(TiposDeCredencialMfa.Totp)
            .Should().Be(MetodosMfa.Totp);
        ConversionDeMetodosMfa.DesdeTipoDeCredencial(TiposDeCredencialMfa.WebAuthn)
            .Should().Be(MetodosMfa.WebAuthn);
    }

    /// <summary>
    /// Un discriminador que este binario no conoce —una fila escrita por una
    /// versión más nueva— no puede tumbar el ingreso de nadie. Da Ninguno, que es
    /// el lado seguro: no satisface ninguna política restrictiva.
    /// </summary>
    [Fact]
    public void Un_tipo_desconocido_no_lanza_y_no_satisface_nada()
    {
        ConversionDeMetodosMfa.DesdeTipoDeCredencial("Biometria2030")
            .Should().Be(MetodosMfa.Ninguno);
        ConversionDeMetodosMfa.DesdeTipoDeCredencial(null)
            .Should().Be(MetodosMfa.Ninguno);
    }

    /// <summary>
    /// «Todos» tiene que ser la unión de los sueltos. Si alguien añade un tercer
    /// método al enum y se olvida de esta constante, las máscaras existentes
    /// dejarían de contar como «no restringe» y todo el mundo caería en
    /// «te falta inscribir» — que es el fallo del despliegue, otra vez.
    /// </summary>
    [Fact]
    public void Todos_es_exactamente_la_union_de_los_metodos_sueltos()
    {
        var union = MetodosMfa.Ninguno;
        foreach (var m in ConversionDeMetodosMfa.Sueltos) union |= m;

        union.Should().Be(Todos);

        foreach (MetodosMfa valor in Enum.GetValues<MetodosMfa>())
        {
            if (valor == MetodosMfa.Ninguno) continue;
            ConversionDeMetodosMfa.Sueltos.Should().Contain(valor,
                "todo método del enum tiene que estar en Sueltos y en Todos");
        }
    }
}
