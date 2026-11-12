using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Common.Mensajeria;

/// <summary>Qué pasó con un aviso (feature 023; contracts/mensajeria.md §4). Decide el ack del adaptador.</summary>
public enum ResultadoDeConsumo
{
    /// <summary>El manejador terminó (con o sin trabajo hecho): se confirma.</summary>
    Atendido = 1,

    /// <summary>La cooperativa no está activa o no existe: se confirma y se registra.</summary>
    CooperativaInactiva = 2,

    /// <summary>La base de la cooperativa tiene migraciones pendientes: se confirma; la fila sigue y el barrido la retoma.</summary>
    Omitido = 3,

    /// <summary>Versión o tipo que no se entiende: va a la cola de muertos de la mensajería.</summary>
    Ilegible = 4,
}

/// <summary>
/// El único camino de un aviso hasta su manejador (feature 023; contracts/mensajeria.md §4): lo usan el consumidor de RabbitMQ y la cola
/// en proceso. Valida versión y tipo, busca la cooperativa en el directorio (cacheado 30 s) y ejecuta el manejador dentro de
/// <see cref="IEjecutorEnCooperativa"/> con el actor «Proceso de integración» y el origen <c>Cola:{cola}</c>. Si el manejador lanza, la
/// excepción sube: el adaptador reencola. Singleton. (nuevo)
/// </summary>
public sealed class ConsumoDeAvisos(
    IServiceScopeFactory ambitos,
    IEjecutorEnCooperativa ejecutor,
    IEnumerable<IManejadorDeAviso> manejadores,
    IDateTimeService reloj,
    ILogger<ConsumoDeAvisos> logger)
{
    private static readonly TimeSpan VidaDelDirectorio = TimeSpan.FromSeconds(30);

    private readonly IReadOnlyDictionary<string, IManejadorDeAviso> _porTipo =
        manejadores.GroupBy(m => m.Tipo, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);

    private readonly SemaphoreSlim _cerrojo = new(1, 1);
    private IReadOnlyList<TenantDirectoryEntry> _directorio = [];
    private DateTime _directorioHasta = DateTime.MinValue;

    /// <summary>¿Hay quien atienda este tipo?</summary>
    public bool Atiende(string tipo) => _porTipo.ContainsKey(tipo);

    public async Task<ResultadoDeConsumo> ConsumirAsync(Aviso aviso, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(aviso);
        if (aviso.V != Aviso.VersionActual || !_porTipo.TryGetValue(aviso.Tipo, out var manejador))
        {
            logger.LogWarning("[Mensajeria.AvisoIlegible] v{Version} {Tipo} {TenantPublicId} {Entidad}",
                aviso.V, aviso.Tipo, aviso.Cooperativa, aviso.Entidad);
            return ResultadoDeConsumo.Ilegible;
        }

        var cooperativa = await CooperativaAsync(aviso.Cooperativa, ct);
        if (cooperativa is null)
        {
            logger.LogWarning("[Mensajeria.CooperativaInactiva] {Tipo} {TenantPublicId} {Entidad}: se descarta.",
                aviso.Tipo, aviso.Cooperativa, aviso.Entidad);
            return ResultadoDeConsumo.CooperativaInactiva;
        }

        var cola = TiposDeAviso.ColaDe(aviso.Tipo);
        if (!manejador.PorCooperativa)
        {
            await using var ambito = ambitos.CreateAsyncScope();
            await manejador.ManejarAsync(aviso, ambito.ServiceProvider, ct);
            return ResultadoDeConsumo.Atendido;
        }
        var r = await ejecutor.EjecutarAsync(cooperativa, Actor.ProcesoDeIntegracion($"Cola:{cola}"), $"Cola:{cola}",
            (servicios, c) => manejador.ManejarAsync(aviso, servicios, c), ct);
        return r == ResultadoEnCooperativa.Omitida ? ResultadoDeConsumo.Omitido : ResultadoDeConsumo.Atendido;
    }

    private async Task<TenantDirectoryEntry?> CooperativaAsync(Guid publicId, CancellationToken ct)
    {
        if (reloj.UtcNow < _directorioHasta)
        {
            var enCache = _directorio.FirstOrDefault(c => c.PublicId == publicId);
            if (enCache is not null) return enCache;
        }

        await _cerrojo.WaitAsync(ct);
        try
        {
            await using var ambito = ambitos.CreateAsyncScope();
            _directorio = await ambito.ServiceProvider.GetRequiredService<ITenantDirectory>().ListActiveAsync(ct);
            _directorioHasta = reloj.UtcNow + VidaDelDirectorio;
            return _directorio.FirstOrDefault(c => c.PublicId == publicId);
        }
        finally
        {
            _cerrojo.Release();
        }
    }
}
