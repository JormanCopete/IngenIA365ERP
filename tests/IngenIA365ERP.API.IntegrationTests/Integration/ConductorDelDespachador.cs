using IngenIA365ERP.API.Integration;
using IngenIA365ERP.API.IntegrationTests.Identity;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.API.IntegrationTests.Integration;

/// <summary>
/// T471 (feature 012, I2; decisiones-transversales T10: «la fixture lo apaga y conduce el ciclo a mano»). En las pruebas el
/// despachador está apagado (<c>Integration:Dispatcher:Enabled = false</c> en <see cref="CentralIdentityApiFixture"/>), pero
/// está registrado como singleton: este ayudante corre a mano una pasada de <see cref="DespachadorDeMensajes"/> sobre una
/// cooperativa, envía un comando como lo enviaría el proceso (dentro de <see cref="IEjecutorEnCooperativa"/>, con el actor
/// «Proceso de integración») y mueve el reloj de la suite (<see cref="RelojDeLaSuite"/>, el <see cref="IDateTimeService"/> del
/// host) para los lotes programados y los reintentos. (nuevo)
/// </summary>
public sealed class ConductorDelDespachador(CentralIdentityApiFixture fx)
{
    /// <summary>Una pasada del despachador sobre esa cooperativa: programar, en línea y lotes.</summary>
    public Task PasadaAsync(Guid tenantPublicId, CancellationToken ct = default) =>
        fx.Factory.Services.GetRequiredService<DespachadorDeMensajes>().CorrerUnaPasadaAsync(tenantPublicId, ct);

    /// <summary>
    /// Pasadas hasta que no quede nada que mover o se agoten (<paramref name="maximo"/>): un lote con dependencias o con
    /// reintentos puede necesitar más de una.
    /// </summary>
    public async Task PasadasAsync(Guid tenantPublicId, int maximo = 3, CancellationToken ct = default)
    {
        for (var i = 0; i < maximo; i++) await PasadaAsync(tenantPublicId, ct);
    }

    /// <summary>
    /// Envía un comando en la cooperativa como lo haría el proceso (sin <c>HttpContext</c>, en un ámbito propio, actor
    /// «Proceso de integración» con ese origen), y devuelve lo que respondió. Es lo que hace el despachador con cada
    /// unidad; lo usan las pruebas que fuerzan una entrega repetida (SC-002).
    /// </summary>
    public async Task<TResultado> EnviarComoProcesoAsync<TResultado>(Guid tenantPublicId, string origen, IRequest<TResultado> comando,
        CancellationToken ct = default)
    {
        var cooperativa = await CooperativaAsync(tenantPublicId, ct);
        TResultado? respuesta = default;
        var r = await fx.Factory.Services.GetRequiredService<IEjecutorEnCooperativa>().EjecutarAsync(cooperativa,
            Actor.ProcesoDeIntegracion(origen), origen,
            async (servicios, c) => respuesta = await servicios.GetRequiredService<ISender>().Send(comando, c), ct);
        if (r != ResultadoEnCooperativa.Ejecutada) throw new InvalidOperationException($"La cooperativa {tenantPublicId} se omitió: {r}.");
        return respuesta!;
    }

    /// <summary>El reloj del host (siempre <see cref="RelojDeLaSuite"/> en las pruebas).</summary>
    public RelojDeLaSuite Reloj => (RelojDeLaSuite)fx.Factory.Services.GetRequiredService<IDateTimeService>();

    /// <summary>
    /// Adelanta el reloj del host y devuelve un objeto que lo regresa al desecharlo: <c>using var _ = conductor.Adelantar(...)</c>.
    /// Sólo mueve <see cref="IDateTimeService"/> (lotes, reintentos, fechas de operación); los tokens se validan con el reloj
    /// del sistema.
    /// </summary>
    public IDisposable Adelantar(TimeSpan cuanto)
    {
        var reloj = Reloj;
        var antes = reloj.Desfase;
        reloj.Desfase = antes + cuanto;
        return new Restaurar(() => reloj.Desfase = antes);
    }

    private async Task<TenantDirectoryEntry> CooperativaAsync(Guid tenantPublicId, CancellationToken ct)
    {
        using var alcance = fx.Factory.Services.CreateScope();
        return (await alcance.ServiceProvider.GetRequiredService<ITenantDirectory>().ListActiveAsync(ct)).Single(c => c.PublicId == tenantPublicId);
    }

    private sealed class Restaurar(Action accion) : IDisposable
    {
        public void Dispose() => accion();
    }
}

/// <summary>
/// El <see cref="IDateTimeService"/> de la suite (T471): el reloj de la API más un desfase que una prueba puede mover
/// (<see cref="ConductorDelDespachador.Adelantar"/>). Con desfase cero es exactamente el reloj real. Uno por host: cada
/// colección tiene su fixture, así que mover el de «Inventario e2e» no toca a las demás. (nuevo)
/// </summary>
public sealed class RelojDeLaSuite(IDateTimeService real) : IDateTimeService
{
    public TimeSpan Desfase { get; set; } = TimeSpan.Zero;

    public DateTime UtcNow => real.UtcNow + Desfase;
    public DateOnly TodayUtc => DateOnly.FromDateTime(UtcNow);
    public DateTimeOffset AhoraLocal => real.AhoraLocal + Desfase;
    public DateOnly HoyLocal => DateOnly.FromDateTime(AhoraLocal.DateTime);
}
