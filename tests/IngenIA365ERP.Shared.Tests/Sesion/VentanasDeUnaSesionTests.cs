using FluentAssertions;
using IngenIA365ERP.Shared.Services.Security;
using Xunit;

namespace IngenIA365ERP.Shared.Tests.Sesion;

/// <summary>
/// 2026-11-17 (pedido del dueño): varias ventanas de una misma sesión —una duplicada, o la que abre «Abrir en ventana nueva»— forman
/// un grupo que se comporta como una sola aplicación de escritorio: la actividad en cualquiera cuenta para todas y cerrar sesión en una
/// cierra todas. El grupo viaja en el storage de la sesión (session_group); un ingreso abre uno nuevo.
/// </summary>
public class VentanasDeUnaSesionTests
{
    private static readonly DateTime Ahora = new(2026, 11, 17, 12, 0, 0, DateTimeKind.Utc);

    private readonly RelojFijo _reloj = new(Ahora);
    private readonly ServidorFalso _servidor = new();
    private readonly GrupoEnMemoria _navegador = new();

    /// <summary>Una ventana con la sesión de <paramref name="almacen"/>; sin almacén, una que acaba de entrar.</summary>
    private async Task<(RenovadorDeSesion Ventana, AlmacenEnMemoria Almacen)> VentanaAsync(AlmacenEnMemoria? almacen = null)
    {
        var propio = almacen is null ? new AlmacenEnMemoria() : Copia(almacen);
        var r = new RenovadorDeSesion(propio, new FabricaDeUnSoloServidor(_servidor), _reloj, _navegador);
        if (almacen is null)
            await r.AdoptarAsync(Jwt.ConVencimiento(Ahora.AddMinutes(15)), Ahora.AddMinutes(15), "refresh-1", Ahora.AddHours(12));
        else
            (await r.RestaurarAsync()).Should().BeTrue();
        return (r, propio);
    }

    /// <summary>Lo que hace el navegador al abrir la ventana nueva: copia el sessionStorage de la que la abrió.</summary>
    private static AlmacenEnMemoria Copia(AlmacenEnMemoria origen)
    {
        var copia = new AlmacenEnMemoria();
        foreach (var (k, v) in origen.Datos) copia.Datos[k] = v;
        return copia;
    }

    [Fact]
    public async Task La_ventana_abierta_desde_otra_queda_en_su_mismo_grupo()
    {
        var (a, almacenA) = await VentanaAsync();
        var (b, _) = await VentanaAsync(almacenA);

        a.Grupo.Should().NotBeNullOrWhiteSpace();
        b.Grupo.Should().Be(a.Grupo);
        almacenA.Datos[RenovadorDeSesion.ClaveGrupo].Should().Be(a.Grupo, "el grupo viaja con la sesión al storage");
    }

    [Fact]
    public async Task La_actividad_en_una_ventana_mantiene_viva_a_la_otra()
    {
        var (a, almacenA) = await VentanaAsync();
        var (b, _) = await VentanaAsync(almacenA);
        string? cerrada = null;
        b.SesionTerminada += c => cerrada = c;

        // A trabaja a los 20 minutos; B no hace nada.
        _reloj.Avanzar(TimeSpan.FromMinutes(20));
        a.RegistrarActividad();
        _reloj.Avanzar(TimeSpan.FromMinutes(15));

        await b.VigilarAsync();

        cerrada.Should().BeNull("35 minutos sin actividad propia, pero el grupo trabajó hace 15");
        b.TieneSesion.Should().BeTrue();
        b.InactividadRestante.Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task Cerrar_sesion_en_una_ventana_cierra_las_demas_del_grupo_sin_llamar_al_servidor()
    {
        var (a, almacenA) = await VentanaAsync();
        var (b, almacenB) = await VentanaAsync(almacenA);
        string? codigo = null;
        b.SesionTerminada += c => codigo = c;
        _servidor.Peticiones.Clear();

        await a.AnunciarCierreAsync();
        await b.VigilarAsync();

        codigo.Should().Be(RenovadorDeSesion.CodigoCerradaEnOtraVentana);
        b.TieneSesion.Should().BeFalse();
        almacenB.Datos.Should().NotContainKeys(RenovadorDeSesion.ClaveAccessToken, RenovadorDeSesion.ClaveRefreshToken, RenovadorDeSesion.ClaveGrupo);
        _servidor.Peticiones.Should().BeEmpty("la que cerró ya revocó el refresh");
    }

    [Fact]
    public async Task Un_ingreso_abre_un_grupo_nuevo_aunque_el_storage_guarde_uno_cerrado()
    {
        var (a, almacenA) = await VentanaAsync();
        var viejo = a.Grupo!;
        await a.AnunciarCierreAsync();
        a.Limpiar();
        // Algo dejó el grupo viejo en el storage (una salida que no pasó por Limpiar).
        almacenA.Datos[RenovadorDeSesion.ClaveGrupo] = viejo;

        await a.AdoptarAsync(Jwt.ConVencimiento(Ahora.AddMinutes(15)), Ahora.AddMinutes(15), "refresh-9", Ahora.AddHours(12));
        string? codigo = null;
        a.SesionTerminada += c => codigo = c;
        await a.VigilarAsync();

        a.Grupo.Should().NotBe(viejo);
        codigo.Should().BeNull("la sesión nueva no hereda el cierre de la anterior");
        a.TieneSesion.Should().BeTrue();
    }

    [Fact]
    public async Task Cambiar_de_cooperativa_conserva_el_grupo()
    {
        var (a, _) = await VentanaAsync();
        var grupo = a.Grupo;

        await a.AdoptarAsync(Jwt.ConVencimiento(Ahora.AddMinutes(15)), Ahora.AddMinutes(15), "refresh-otra", Ahora.AddHours(12));

        a.Grupo.Should().Be(grupo);
    }

    [Fact]
    public async Task Una_sesion_de_antes_sin_grupo_recibe_uno_al_restaurarse()
    {
        var almacen = new AlmacenEnMemoria();
        almacen.Datos[RenovadorDeSesion.ClaveAccessToken] = Jwt.ConVencimiento(Ahora.AddMinutes(15));
        almacen.Datos[RenovadorDeSesion.ClaveRefreshToken] = "refresh-1";
        var r = new RenovadorDeSesion(almacen, new FabricaDeUnSoloServidor(_servidor), _reloj, _navegador);

        (await r.RestaurarAsync()).Should().BeTrue();

        r.Grupo.Should().NotBeNullOrWhiteSpace();
        almacen.Datos[RenovadorDeSesion.ClaveGrupo].Should().Be(r.Grupo);
    }

    /// <summary>El localStorage del navegador, compartido por las ventanas: última actividad y cierre por grupo.</summary>
    private sealed class GrupoEnMemoria : ICoordinacionEntrePestanas
    {
        private readonly Dictionary<string, EstadoDelGrupo> _grupos = new(StringComparer.Ordinal);

        public Task<IAsyncDisposable> TomarTurnoAsync(CancellationToken ct = default) =>
            SinCoordinacionEntrePestanas.Instancia.TomarTurnoAsync(ct);

        public Task<SucesorDeSesion?> SucesorDeAsync(string refreshGastado) => Task.FromResult<SucesorDeSesion?>(null);

        public Task PublicarSucesorAsync(string refreshGastado, SucesorDeSesion sucesor) => Task.CompletedTask;

        public Task<EstadoDelGrupo> LeerGrupoAsync(string grupo) =>
            Task.FromResult(_grupos.TryGetValue(grupo, out var e) ? e : EstadoDelGrupo.Vacio);

        public Task AnunciarActividadAsync(string grupo, DateTime cuando)
        {
            var e = _grupos.GetValueOrDefault(grupo, EstadoDelGrupo.Vacio);
            if (e.UltimaActividad is null || cuando > e.UltimaActividad) _grupos[grupo] = e with { UltimaActividad = cuando };
            return Task.CompletedTask;
        }

        public Task AnunciarCierreAsync(string grupo)
        {
            _grupos[grupo] = _grupos.GetValueOrDefault(grupo, EstadoDelGrupo.Vacio) with { Cerrada = true };
            return Task.CompletedTask;
        }
    }
}
