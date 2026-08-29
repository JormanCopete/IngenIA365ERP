using FluentAssertions;
using IngenIA365ERP.Identity.CentralIdentity;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Identity.Auth;

/// <summary>
/// El canje de un código de respaldo compara ordinal. Estas pruebas fijan qué
/// diferencias se perdonan —las de tecleo— y cuáles no.
///
/// <para>
/// Importa más de lo que parece: un código de respaldo se usa el día que ya salió
/// todo mal, normalmente desde un teclado ajeno y con prisa. Y cada fallo suma al
/// contador de bloqueo del segundo factor, que sólo baja al acertar.
/// </para>
/// </summary>
public class NormalizacionDelCodigoDeRespaldoTests
{
    // Formato de UserManager.CreateTwoFactorRecoveryCode: cinco, guion, cinco,
    // sobre el alfabeto 23456789BCDFGHJKMNPQRTVWXY.
    private const string Emitido = "BWJRF-P88VC";

    [Theory]
    [InlineData("BWJRF-P88VC")]        // tal cual salió
    [InlineData("bwjrf-p88vc")]        // todo en minúsculas
    [InlineData("BwJrF-p88Vc")]        // mezclado
    [InlineData("  BWJRF-P88VC  ")]    // con espacios alrededor
    [InlineData("BWJRF - P88VC")]      // con espacios adentro, como lo pega un copiado
    [InlineData("BWJRFP88VC")]         // tecleado de corrido, sin el guion
    [InlineData("bwjrfp88vc")]         // las dos cosas a la vez
    public void Se_perdona_lo_que_es_solo_una_diferencia_de_tecleo(string tecleado)
    {
        NormalizacionDelCodigoDeRespaldo.Normalizar(tecleado)
            .Should().Be(Emitido);
    }

    [Theory]
    [InlineData("BWJRF-P88VX")]        // un carácter distinto
    [InlineData("BWJRF-P88V")]         // le falta uno
    [InlineData("BWJRF-P88VCC")]       // le sobra uno
    public void Un_codigo_realmente_distinto_sigue_siendo_distinto(string otro)
    {
        NormalizacionDelCodigoDeRespaldo.Normalizar(otro)
            .Should().NotBe(Emitido);
    }

    [Fact]
    public void El_guion_solo_se_inserta_cuando_hay_exactamente_diez_caracteres()
    {
        // Nueve: no se toca. Insertarlo aquí inventaría un formato que Identity
        // nunca emitió, y convertiría un código incompleto en uno con aspecto de
        // válido — que es peor que rechazarlo.
        NormalizacionDelCodigoDeRespaldo.Normalizar("BWJRFP88V")
            .Should().Be("BWJRFP88V");

        // Once sin guion: tampoco.
        NormalizacionDelCodigoDeRespaldo.Normalizar("BWJRFP88VCX")
            .Should().Be("BWJRFP88VCX");
    }

    [Fact]
    public void Un_codigo_que_ya_trae_guion_no_recibe_otro()
    {
        NormalizacionDelCodigoDeRespaldo.Normalizar("BWJRF-P88VC")
            .Should().Be("BWJRF-P88VC");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Vacio_o_nulo_no_revienta(string? nada)
    {
        NormalizacionDelCodigoDeRespaldo.Normalizar(nada)
            .Should().BeEmpty();
    }
}
