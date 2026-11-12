using FluentAssertions;
using IngenIA365ERP.Shared.Services.Teclado;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Teclado;

/// <summary>2026-11-12 (pedido del dueño): la tabla única de comandos que despachan por igual la tecla y el botón de la barra de atajos.</summary>
public class TablaDeComandosTests
{
    [Fact]
    public async Task La_tecla_ejecuta_su_accion_cuando_aplica()
    {
        var veces = 0;
        var tabla = new TablaDeComandos([new ComandoDeTeclado("F2", "buscar", () => { veces++; return Task.CompletedTask; })]);

        var r = await tabla.EjecutarAsync("F2");

        r.Estado.Should().Be(EstadoDeComando.Ejecutado);
        veces.Should().Be(1);
    }

    [Fact]
    public async Task Lo_que_no_aplica_no_se_ejecuta_y_dice_por_que_si_lo_sabe()
    {
        var veces = 0;
        var cobrando = true;
        var tabla = new TablaDeComandos(
        [
            new ComandoDeTeclado("F9", "recuperar", () => { veces++; return Task.CompletedTask; }, () => !cobrando,
                PorQueNo: () => cobrando ? "Está cobrando." : null),
            new ComandoDeTeclado("F3", "cantidad", () => { veces++; return Task.CompletedTask; }, () => false),
        ]);

        var f9 = await tabla.EjecutarAsync("F9");
        f9.Should().Be(new EjecucionDeComando(EstadoDeComando.NoAplica, "Está cobrando."));
        var f3 = await tabla.EjecutarAsync("F3");
        f3.Should().Be(new EjecucionDeComando(EstadoDeComando.NoAplica), "sin PorQueNo la tecla sigue muda, como antes");
        veces.Should().Be(0);

        cobrando = false;
        tabla.Buscar("F9")!.Disponible.Should().BeTrue("la condición se evalúa cada vez, con el estado de la pantalla");
        (await tabla.EjecutarAsync("F9")).Estado.Should().Be(EstadoDeComando.Ejecutado);
        veces.Should().Be(1);
    }

    [Fact]
    public async Task Una_tecla_que_no_esta_en_la_tabla_es_desconocida()
    {
        var tabla = new TablaDeComandos([new ComandoDeTeclado("F2", "buscar", () => Task.CompletedTask)]);

        (await tabla.EjecutarAsync("F5")).Estado.Should().Be(EstadoDeComando.Desconocido);
    }

    [Fact]
    public async Task Una_pista_se_muestra_pero_no_se_ejecuta()
    {
        var pista = ComandoDeTeclado.Pista("3*", "multiplica");
        var tabla = new TablaDeComandos([pista]);

        pista.EsPista.Should().BeTrue();
        pista.Disponible.Should().BeFalse();
        (await tabla.EjecutarAsync("3*")).Estado.Should().Be(EstadoDeComando.NoAplica);
    }

    [Fact]
    public void La_tecla_visible_puede_ser_otra_que_la_que_llega()
    {
        new ComandoDeTeclado("Delete", "quitar línea", () => Task.CompletedTask, TeclaVisible: "Supr").Rotulo.Should().Be("Supr");
        new ComandoDeTeclado("F10", "cobrar", () => Task.CompletedTask).Rotulo.Should().Be("F10");
    }

    [Fact]
    public void Una_tecla_repetida_es_un_error_de_la_pantalla()
    {
        var crear = () => new TablaDeComandos(
        [
            new ComandoDeTeclado("F2", "buscar", () => Task.CompletedTask),
            new ComandoDeTeclado("F2", "otra cosa", () => Task.CompletedTask),
        ]);

        crear.Should().Throw<ArgumentException>().WithMessage("*F2*dos veces*");
    }

    [Fact]
    public void La_barra_los_pinta_en_el_orden_de_la_tabla()
    {
        var tabla = new TablaDeComandos(
        [
            new ComandoDeTeclado("F2", "buscar", () => Task.CompletedTask),
            ComandoDeTeclado.Pista("3*", "multiplica"),
            new ComandoDeTeclado("Escape", "volver", () => Task.CompletedTask, TeclaVisible: "Esc"),
        ]);

        tabla.Comandos.Select(c => c.Rotulo).Should().Equal("F2", "3*", "Esc");
    }
}
