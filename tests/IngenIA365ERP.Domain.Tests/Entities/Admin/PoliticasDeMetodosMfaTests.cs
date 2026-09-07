using FluentAssertions;
using IngenIA365ERP.Domain.Entities.Admin;
using Xunit;

namespace IngenIA365ERP.Domain.Tests.Entities.Admin;

/// <summary>
/// La invariante que sostiene todo el diseño de métodos: <b>no se puede exigir
/// segundo factor sin aceptar ningún método</b>.
///
/// <para>
/// Si se pudiera, la única salida que ofrece el sistema —«inscribí uno de estos»—
/// sería mentira, porque no habría ninguno que inscribir, y una cooperativa
/// entera quedaría encerrada con una sola llamada a la API. Vive en Domain y no
/// en el handler para que ninguna ruta futura pueda saltársela.
/// </para>
/// </summary>
public class PoliticasDeMetodosMfaTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly DateTime Ahora = new(2026, 8, 28, 12, 0, 0, DateTimeKind.Utc);

    // ---------- Cooperativa ----------

    [Fact]
    public void Una_politica_nueva_acepta_todos_los_metodos()
    {
        var politica = TenantMfaPolicy.CreateForTenant(Guid.NewGuid());

        politica.AllowedMethodsMask.Should().Be(ConversionDeMetodosMfa.Todos,
            "encender el segundo factor sin decir más significa «que tengan algo», " +
            "no «que tengan justo esto»");
        politica.IsRequired.Should().BeFalse();
    }

    [Fact]
    public void No_se_puede_exigir_MFA_con_la_mascara_vacia()
    {
        var politica = TenantMfaPolicy.CreateForTenant(Guid.NewGuid());
        politica.PermitirMetodos(MetodosMfa.Ninguno);

        var acto = () => politica.Enable(Actor, Ahora);

        acto.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void No_se_puede_vaciar_la_mascara_de_una_politica_que_ya_exige()
    {
        var politica = TenantMfaPolicy.CreateForTenant(Guid.NewGuid());
        politica.Enable(Actor, Ahora);

        var acto = () => politica.PermitirMetodos(MetodosMfa.Ninguno);

        acto.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Las dos puertas se comprueban por separado a propósito: el orden de las
    /// llamadas no está garantizado, y comprobar sólo una dejaría llegar al mismo
    /// estado imposible por la otra.
    /// </summary>
    [Fact]
    public void Vaciar_y_luego_exigir_tampoco_cuela()
    {
        var politica = TenantMfaPolicy.CreateForTenant(Guid.NewGuid());

        politica.PermitirMetodos(MetodosMfa.Ninguno);  // legal: todavía no exige
        var acto = () => politica.Enable(Actor, Ahora);

        acto.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Restringir_a_un_solo_metodo_es_legal()
    {
        var politica = TenantMfaPolicy.CreateForTenant(Guid.NewGuid());
        politica.PermitirMetodos(MetodosMfa.WebAuthn);
        politica.Enable(Actor, Ahora);

        politica.AllowedMethodsMask.Should().Be(MetodosMfa.WebAuthn);
        politica.IsRequired.Should().BeTrue();
    }

    /// <summary>
    /// Cambiar los métodos no toca las fechas de activación: describen cuándo se
    /// encendió o apagó la exigencia, no cuándo cambió el juego de métodos.
    /// Reutilizarlas haría que la auditoría contara una historia falsa.
    /// </summary>
    [Fact]
    public void Cambiar_los_metodos_no_reescribe_cuando_se_activo()
    {
        var politica = TenantMfaPolicy.CreateForTenant(Guid.NewGuid());
        politica.Enable(Actor, Ahora);

        politica.PermitirMetodos(MetodosMfa.Totp);

        politica.ActivatedAt.Should().Be(Ahora);
    }

    // ---------- Plataforma ----------

    [Fact]
    public void La_politica_de_la_plataforma_nace_aceptando_todo()
    {
        PlatformMfaPolicy.Inicial().AllowedMethodsMask
            .Should().Be(ConversionDeMetodosMfa.Todos);
    }

    /// <summary>
    /// Aquí no hay matiz posible: el segundo factor del maestro es obligatorio
    /// siempre, así que una máscara vacía lo deja fuera de su propio sistema — y es
    /// la única cuenta que nadie más puede rescatar.
    /// </summary>
    [Fact]
    public void La_politica_de_la_plataforma_nunca_puede_quedarse_sin_metodos()
    {
        var politica = PlatformMfaPolicy.Inicial();

        var acto = () => politica.PermitirMetodos(MetodosMfa.Ninguno, Actor, Ahora);

        acto.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void La_politica_de_la_plataforma_sella_quien_y_cuando()
    {
        var politica = PlatformMfaPolicy.Inicial();

        politica.PermitirMetodos(MetodosMfa.WebAuthn, Actor, Ahora);

        politica.AllowedMethodsMask.Should().Be(MetodosMfa.WebAuthn);
        politica.ChangedAt.Should().Be(Ahora);
        politica.ChangedByUserId.Should().Be(Actor);
    }
}
