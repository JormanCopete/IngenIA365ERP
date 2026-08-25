using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces.Identity;

namespace IngenIA365ERP.Application.Tests.Identity.Auth;

/// <summary>
/// El escalado de bloqueo por fuerza bruta (research D-11).
///
/// <para>
/// <b>El fallo que fija.</b> El candado sólo se escribía cuando el contador era
/// <i>exactamente</i> igual a un umbral. Como el último umbral es 20, a partir
/// del intento 21 ninguna coincidencia volvía a darse: <c>ShouldLock</c> era
/// falso para siempre. Y el TTL del contador se refresca en cada fallo, así que
/// tampoco bajaba. El coste real de la fuerza bruta era aguantar veinte
/// intentos, esperar la hora una vez, y luego intentar sin límite.
/// </para>
///
/// <para>
/// Nadie lo vio porque la condición vivía entre dos llamadas a Redis y las
/// pruebas del login sólo llegaban al quinto fallo. Esta tabla llega al 21.
/// </para>
/// </summary>
public class EscaladoDeBloqueoTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(6)]   // dentro de la ventana de 1 min ya activa
    [InlineData(9)]
    [InlineData(14)]
    [InlineData(19)]
    public void Entre_umbrales_no_vuelve_a_bloquear(int fallos)
    {
        // Re-bloquear en cada fallo dentro de una ventana ya activa alargaría el
        // castigo indefinidamente por seguir intentando.
        EscaladoDeBloqueo.Decidir(fallos).ShouldLock.Should().BeFalse();
    }

    [Theory]
    [InlineData(5, 60)]
    [InlineData(10, 300)]
    [InlineData(15, 900)]
    [InlineData(20, 3600)]
    public void Al_cruzar_un_umbral_bloquea_su_ventana(int fallos, int segundos)
    {
        var v = EscaladoDeBloqueo.Decidir(fallos);

        v.ShouldLock.Should().BeTrue();
        v.LockSeconds.Should().Be(segundos);
        v.FailureCount.Should().Be(fallos);
    }

    [Theory]
    [InlineData(21)]
    [InlineData(22)]
    [InlineData(50)]
    [InlineData(500)]
    [InlineData(100_000)]
    public void Por_encima_del_tope_CADA_intento_vuelve_a_bloquear(int fallos)
    {
        // ESTE es el caso que faltaba. Sin él, pasado el 20 el atacante tenía
        // intentos ilimitados y para siempre: ni el contador baja —su TTL se
        // refresca en cada fallo— ni nadie lo resetea salvo un login correcto,
        // que es justo lo que el atacante no tiene.
        var v = EscaladoDeBloqueo.Decidir(fallos);

        v.ShouldLock.Should().BeTrue(
            $"con {fallos} fallos acumulados cada intento debe costar la ventana máxima");
        v.LockSeconds.Should().Be(3600);
    }

    [Fact]
    public void El_coste_de_probar_un_millon_de_codigos_no_es_finito()
    {
        // Formulado como lo que importa: un TOTP son seis dígitos. Si a partir de
        // algún punto los intentos dejaran de costar, probarlos todos es cuestión
        // de tiempo de máquina. Que NINGUNA cuenta alta quede sin bloquear es la
        // propiedad, no el valor concreto de los umbrales.
        var sinBloquear = Enumerable.Range(EscaladoDeBloqueo.Tope + 1, 2_000)
            .Where(n => !EscaladoDeBloqueo.Decidir(n).ShouldLock)
            .ToList();

        sinBloquear.Should().BeEmpty(
            "por encima del tope no puede existir un solo intento gratis");
    }

    [Fact]
    public void Los_umbrales_estan_en_orden_ascendente()
    {
        // La función recorre la tabla quedándose con el último que aplica: si
        // alguien la desordenara, un fallo alto elegiría una ventana corta.
        EscaladoDeBloqueo.Umbrales.Select(u => u.TrasFallos)
            .Should().BeInAscendingOrder();
    }
}
