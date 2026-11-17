using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tablero;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Tablero;

/// <summary>2026-11-17 (pedido del dueño): el tablero de inicio por usuario y por rol.</summary>
public class TableroTests
{
    // ------------------------------------------------------------------------------------------------- guarda --

    /// <summary>
    /// Todo indicador y pendiente registrado (los módulos los agregan como clases) tiene código único y bien formado —es lo que queda
    /// guardado en las preferencias y en el tablero de cada rol—, un permiso <c>Modulo.Recurso.Accion</c> y una ruta interna.
    /// </summary>
    [Fact]
    public void Cada_indicador_y_pendiente_tiene_codigo_unico_permiso_y_ruta_interna()
    {
        var definiciones = Instancias<IIndicadorDelTablero>().Select(i => (Codigo: i.Definicion.Codigo, Permiso: i.Definicion.Permiso, Ruta: i.Definicion.Ruta, Name: i.GetType().Name))
            .Concat(Instancias<IPendienteDelTablero>().Select(p => (Codigo: p.Definicion.Codigo, Permiso: p.Definicion.Permiso, Ruta: (string?)p.Definicion.Ruta, Name: p.GetType().Name)))
            .ToList();

        definiciones.Should().NotBeEmpty();
        definiciones.Where(d => !CodigosDelTablero.EsValido(d.Codigo)).Select(d => d.Name).Should().BeEmpty("el código va en minúsculas con puntos: modulo.cifra");
        definiciones.GroupBy(d => d.Codigo).Where(g => g.Count() > 1).Select(g => g.Key).Should().BeEmpty("un código identifica una sola cifra");
        definiciones.Where(d => d.Permiso.Count(c => c == '.') < 2).Select(d => d.Name).Should().BeEmpty("el permiso es el de la pantalla de la que sale la cifra");
        definiciones.Where(d => d.Ruta is not null && (!d.Ruta.StartsWith('/') || d.Ruta.StartsWith("//"))).Select(d => d.Name).Should().BeEmpty();
    }

    /// <summary>Crea cada implementación con dobles en sus dependencias: sólo se lee su definición.</summary>
    private static IEnumerable<T> Instancias<T>() where T : class =>
        typeof(T).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(T).IsAssignableFrom(t))
            .Select(t =>
            {
                var ctor = t.GetConstructors().OrderByDescending(c => c.GetParameters().Length).First();
                var args = ctor.GetParameters().Select(p => p.ParameterType.IsInterface ? Substitute.For([p.ParameterType], []) : null).ToArray();
                return (T)ctor.Invoke(args);
            });

    // ------------------------------------------------------------------------------------------ lo guardado --

    [Fact]
    public void Un_tablero_guardado_se_lee_sin_repetidos_ni_codigos_mal_formados()
    {
        var leidas = TarjetasDelTablero.Leer("""[{"codigo":"ventas.del-dia","soloLoMio":true},{"codigo":"ventas.del-dia"},{"codigo":"MAL"},{"codigo":"caja.mi-caja"}]""");

        leidas.Should().Equal(new TarjetaDelTablero("ventas.del-dia", true), new TarjetaDelTablero("caja.mi-caja"));
        TarjetasDelTablero.Leer("no es json").Should().BeNull("se usa el siguiente defecto");
        TarjetasDelTablero.Leer(null).Should().BeNull();
    }

    [Fact]
    public void Las_preferencias_solo_aceptan_un_tablero_bien_formado()
    {
        TarjetasDelTablero.EsJsonValido("""[{"codigo":"ventas.del-dia","soloLoMio":true}]""").Should().BeTrue();
        TarjetasDelTablero.EsJsonValido("[]").Should().BeTrue("vacío es un tablero sin tarjetas");
        TarjetasDelTablero.EsJsonValido("""[{"codigo":"ventas.del-dia"},{"codigo":"ventas.del-dia"}]""").Should().BeFalse("repetido");
        TarjetasDelTablero.EsJsonValido("""[{"codigo":"<script>"}]""").Should().BeFalse();
        var demasiadas = string.Join(",", Enumerable.Range(1, CodigosDelTablero.MaximoDeTarjetas + 1).Select(i => $$"""{"codigo":"a.b{{i}}"}"""));
        TarjetasDelTablero.EsJsonValido($"[{demasiadas}]").Should().BeFalse();
    }

    [Fact]
    public void El_sugerido_pone_primero_los_marcados_por_defecto_y_llega_a_ocho()
    {
        var visibles = Enumerable.Range(1, 10).Select(i => Indicador($"m.i{i}", porDefecto: i is 9 or 10)).ToList();

        var sugerido = GetMyDashboardQueryHandler.Sugerido(visibles);

        sugerido.Should().HaveCount(TableroDelRol.TarjetasSugeridas);
        sugerido.Take(2).Select(t => t.Codigo).Should().BeEquivalentTo("m.i9", "m.i10");
        sugerido.Should().OnlyContain(t => !t.SoloLoMio);
    }

    private static IIndicadorDelTablero Indicador(string codigo, bool porDefecto)
    {
        var i = Substitute.For<IIndicadorDelTablero>();
        i.Definicion.Returns(new DefinicionDeIndicador(codigo, codigo, "Pruebas", "Core.People.View", true, TipoDeIndicador.Cifra,
            FormatoDeIndicador.Numero, null, "", porDefecto));
        i.CalcularAsync(Arg.Any<ContextoDeIndicador>(), Arg.Any<CancellationToken>()).Returns(Result.Success(new ResultadoDeIndicador(1m)));
        return i;
    }
}
