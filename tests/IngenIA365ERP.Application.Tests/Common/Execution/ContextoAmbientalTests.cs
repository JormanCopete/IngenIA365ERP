using FluentAssertions;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Enums.Integration;

namespace IngenIA365ERP.Application.Tests.Common.Execution;

/// <summary>
/// El contexto de un trabajo de fondo (feature 012, T5, T6, T040). Fuera de una petición HTTP no
/// hay de dónde sacar la cooperativa ni quién actúa: se fijan en un <c>AsyncLocal</c> que los
/// accesores singleton leen cuando no hay <c>HttpContext</c>. Lo que no se negocia: se restaura
/// siempre, no se filtra a otro flujo asíncrono y el actor del proceso nunca tiene IP.
/// </summary>
public class ContextoAmbientalTests
{
    private static readonly TenantDirectoryEntry CoopA = new(7, Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"), "coop_a", "Coop A", DatabaseName: "coop_a");
    private static readonly TenantDirectoryEntry CoopB = new(8, Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002"), "coop_b", "Coop B", DatabaseName: "coop_b");

    [Fact]
    public void Sin_fijar_no_hay_contexto()
    {
        ContextoAmbiental.Activo.Should().BeFalse();
        ContextoAmbiental.Cooperativa.Should().BeNull();
        ContextoAmbiental.Actor.Should().BeNull();
        ContextoAmbiental.Origen.Should().BeNull();
    }

    [Fact]
    public void Fija_y_restaura_al_liberar()
    {
        var actor = Actor.ProcesoDeIntegracion("Tarea:reorden");
        using (ContextoAmbiental.Fijar(CoopA, actor, "Tarea:reorden"))
        {
            ContextoAmbiental.Activo.Should().BeTrue();
            ContextoAmbiental.Cooperativa.Should().Be(CoopA);
            ContextoAmbiental.Actor.Should().Be(actor);
            ContextoAmbiental.Origen.Should().Be("Tarea:reorden");
        }

        ContextoAmbiental.Activo.Should().BeFalse();
    }

    [Fact]
    public void Anidado_restaura_el_de_afuera_y_no_el_vacio()
    {
        using (ContextoAmbiental.Fijar(CoopA, Actor.ProcesoDeIntegracion("Lote:1"), "Lote:1"))
        {
            using (ContextoAmbiental.Fijar(CoopB, Actor.ProcesoDeIntegracion("Mensaje:9"), "Mensaje:9"))
            {
                ContextoAmbiental.Cooperativa.Should().Be(CoopB);
            }

            ContextoAmbiental.Cooperativa.Should().Be(CoopA);
            ContextoAmbiental.Origen.Should().Be("Lote:1");
        }
    }

    [Fact]
    public async Task Fluye_por_await_y_no_se_filtra_a_otro_flujo()
    {
        Guid? vistoAdentro = null;
        var otroFlujo = Task.Run(async () =>
        {
            await Task.Delay(20);
            return ContextoAmbiental.Cooperativa;
        });

        using (ContextoAmbiental.Fijar(CoopA, Actor.ProcesoDeIntegracion("Tarea:x"), "Tarea:x"))
        {
            await Task.Yield();
            vistoAdentro = await Task.Run(() => ContextoAmbiental.Cooperativa?.PublicId);
        }

        vistoAdentro.Should().Be(CoopA.PublicId, "el trabajo lanzado dentro del contexto lo hereda");
        (await otroFlujo).Should().BeNull("un flujo que empezó antes no ve el contexto de otro");
    }

    [Fact]
    public void Exige_cooperativa_actor_y_origen()
    {
        var actor = Actor.ProcesoDeIntegracion("Tarea:x");
        FluentActions.Invoking(() => ContextoAmbiental.Fijar(null!, actor, "Tarea:x")).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => ContextoAmbiental.Fijar(CoopA, null!, "Tarea:x")).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => ContextoAmbiental.Fijar(CoopA, actor, " ")).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Liberar_dos_veces_no_pisa_el_contexto_restaurado()
    {
        using var afuera = ContextoAmbiental.Fijar(CoopA, Actor.ProcesoDeIntegracion("Lote:1"), "Lote:1");
        var adentro = ContextoAmbiental.Fijar(CoopB, Actor.ProcesoDeIntegracion("Lote:2"), "Lote:2");
        adentro.Dispose();
        adentro.Dispose();

        ContextoAmbiental.Cooperativa.Should().Be(CoopA);
    }
}

public class ActorTests
{
    [Theory]
    [InlineData("Mensaje:12345")]
    [InlineData("Lote:LT-000017")]
    [InlineData("Tarea:reorden")]
    public void El_proceso_de_integracion_no_es_una_persona_ni_tiene_ip(string origen)
    {
        var actor = Actor.ProcesoDeIntegracion(origen);

        actor.Kind.Should().Be(ActorKind.Process);
        actor.Name.Should().Be("Proceso de integración");
        actor.Channel.Should().Be(ExecutionChannel.Process);
        actor.Origin.Should().Be(origen);
        actor.Ip.Should().BeNull();
        actor.UserId.Should().BeNull("el proceso no tiene fila en SEC_Users (no se crea usuario técnico)");
        actor.UserPublicId.Should().BeNull();
        actor.CentralUserId.Should().BeNull();
        actor.Email.Should().BeNull();
        actor.Reason.Should().BeNull();
        actor.EsProceso.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("reorden")]
    [InlineData("Tarea:")]
    [InlineData("Proceso:algo")]
    public void El_origen_del_proceso_dice_mensaje_lote_o_tarea(string origen)
    {
        FluentActions.Invoking(() => Actor.ProcesoDeIntegracion(origen)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Las_fabricas_de_origen_componen_el_texto()
    {
        Actor.OrigenDeMensaje(42).Should().Be("Mensaje:42");
        Actor.OrigenDeLote("LT-7").Should().Be("Lote:LT-7");
        Actor.OrigenDeTarea("quiebre").Should().Be("Tarea:quiebre");
    }
}
