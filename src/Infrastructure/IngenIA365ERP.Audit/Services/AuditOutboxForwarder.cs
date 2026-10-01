using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Audit.Services;

/// <summary>
/// Lleva la auditoría de los módulos encadenados de <c>COR_AuditOutbox</c> a Mongo, sellada en la cadena de
/// cada cooperativa (feature 012, T37, T38; T065). Registrado <b>sólo</b> en <c>API/Program.cs</c> y
/// condicionado a <c>Integration:AuditForwarder:Enabled</c>; el DbMigrator nunca lo arranca. Queda registrado
/// aunque esté apagado, para que la fixture de pruebas conduzca una pasada a mano
/// (<see cref="ReenviarUnaPasadaAsync(Guid, CancellationToken)"/>).
///
/// <para>
/// Una pasada: recorre <see cref="ITenantDirectory.ListActiveAsync"/>; en cada cooperativa, por
/// <see cref="IEjecutorEnCooperativa"/> con el actor «Proceso de integración» y origen
/// <c>Tarea:audit.forward</c>, toma el arrendamiento <c>audit.forward</c> (si lo tiene otra réplica, la salta),
/// deja el trabajo a <see cref="SelladoDeAuditoria"/> y lo suelta. Una cooperativa que falla se registra y
/// sigue la siguiente. La exactitud no depende del arrendamiento: la cabeza de la cadena con
/// <c>RowVersion</c> y el <c>_id = EventId</c> de Mongo la sostienen aunque dos réplicas coincidan.
/// </para>
/// </summary>
public sealed class AuditOutboxForwarder : BackgroundService
{
    private static readonly TimeSpan IntervaloPorDefecto = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan EsperaDeLaBase = TimeSpan.FromSeconds(5);
    private static readonly string Origen = Actor.OrigenDeTarea(NombresDeArrendamiento.ReenvioDeAuditoria);

    private readonly IServiceScopeFactory _ambitos;
    private readonly IEjecutorEnCooperativa _ejecutor;
    private readonly SelladoDeAuditoria _sellado;
    private readonly ILogger<AuditOutboxForwarder> _logger;
    private readonly Func<bool> _baseLista;
    private readonly TimeSpan _intervalo;

    public AuditOutboxForwarder(
        IServiceScopeFactory ambitos,
        IEjecutorEnCooperativa ejecutor,
        SelladoDeAuditoria sellado,
        ILogger<AuditOutboxForwarder> logger,
        Func<bool>? baseLista = null,
        TimeSpan? intervalo = null)
    {
        _ambitos = ambitos;
        _ejecutor = ejecutor;
        _sellado = sellado;
        _logger = logger;
        _baseLista = baseLista ?? (() => true);
        _intervalo = intervalo is { } i && i > TimeSpan.Zero ? i : IntervaloPorDefecto;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AuditOutboxForwarder iniciado: una pasada cada {Segundos} s.", _intervalo.TotalSeconds);

        while (!_baseLista())
        {
            if (!await EsperarAsync(EsperaDeLaBase, stoppingToken)) return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReenviarUnaPasadaAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Auditoria.PasadaFallida] La pasada del reenviador falló; se reintenta en {Segundos} s.",
                    _intervalo.TotalSeconds);
            }

            if (!await EsperarAsync(_intervalo, stoppingToken)) return;
        }
    }

    /// <summary>Una pasada por todas las cooperativas activas. La usan el ciclo y las pruebas.</summary>
    public Task<int> ReenviarUnaPasadaAsync(CancellationToken ct) => ReenviarAsync(null, ct);

    /// <summary>Una pasada sólo por la cooperativa indicada (la fixture, que tiene el ciclo apagado).</summary>
    public Task<int> ReenviarUnaPasadaAsync(Guid tenantPublicId, CancellationToken ct) => ReenviarAsync(tenantPublicId, ct);

    private async Task<int> ReenviarAsync(Guid? soloEsta, CancellationToken ct)
    {
        if (!_baseLista()) return 0;

        IReadOnlyList<TenantDirectoryEntry> cooperativas;
        await using (var ambito = _ambitos.CreateAsyncScope())
        {
            cooperativas = await ambito.ServiceProvider.GetRequiredService<ITenantDirectory>().ListActiveAsync(ct);
        }

        var total = 0;
        foreach (var cooperativa in cooperativas.Where(c => soloEsta is null || c.PublicId == soloEsta))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await _ejecutor.EjecutarAsync(cooperativa, Actor.ProcesoDeIntegracion(Origen), Origen, async (servicios, c) =>
                {
                    var arrendamientos = servicios.GetRequiredService<IArrendamientos>();
                    if (!await arrendamientos.ArrendarAsync(NombresDeArrendamiento.ReenvioDeAuditoria, null, c)) return;
                    try
                    {
                        total += await _sellado.ReenviarAsync(servicios, cooperativa.PublicId, c);
                    }
                    finally
                    {
                        await arrendamientos.SoltarAsync(NombresDeArrendamiento.ReenvioDeAuditoria, CancellationToken.None);
                    }
                }, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Auditoria.CooperativaFallida] El reenvío de {Cooperativa} ({TenantPublicId}) falló; siguen las demás.",
                    cooperativa.Name, cooperativa.PublicId);
            }
        }

        return total;
    }

    private static async Task<bool> EsperarAsync(TimeSpan cuanto, CancellationToken ct)
    {
        try
        {
            await Task.Delay(cuanto, ct);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
    }
}
