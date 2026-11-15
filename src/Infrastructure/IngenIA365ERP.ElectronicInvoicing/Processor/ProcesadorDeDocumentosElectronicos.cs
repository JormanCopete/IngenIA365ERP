using System.Diagnostics;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Mensajeria;
using IngenIA365ERP.Application.ElectronicInvoicing.Documents;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;
using IngenIA365ERP.ElectronicInvoicing.Channels;
using IngenIA365ERP.ElectronicInvoicing.Circuit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.ElectronicInvoicing.Processor;

/// <summary>
/// Qué documentos le tocan al procesador (feature 012, I4, T731; contracts/dian.md §6.4): <c>NextAttemptAt ≤ ahora</c>, sin arrendamiento
/// vigente, en <c>Pending</c>, <c>Sent</c>, <c>DianContingency</c> o <c>IssuerContingency</c> con su evento cerrado; los que esperan a otro
/// documento (<c>WaitsForDocumentId</c>) sólo cuando ése quedó validado; en orden de prefijo y consecutivo. Lee, no escribe. (nuevo)
/// </summary>
public static class DocumentosPorProcesar
{
    public static async Task<IReadOnlyList<Guid>> ElegiblesAsync(IApplicationDbContext db, DateTime ahora, int tanda, CancellationToken ct) =>
        await db.ElectronicDocuments.AsNoTracking()
            .Where(d => d.NextAttemptAt != null && d.NextAttemptAt <= ahora
                        && d.DeadAt == null // feature 023: el muerto espera a una persona
                        && (d.LeaseUntil == null || d.LeaseUntil < ahora)
                        && (d.Status == ElectronicDocumentStatus.Pending
                            || d.Status == ElectronicDocumentStatus.Sent
                            || d.Status == ElectronicDocumentStatus.DianContingency
                            || (d.Status == ElectronicDocumentStatus.IssuerContingency
                                && d.ContingencyEvent != null && d.ContingencyEvent.Status == ContingencyEventStatus.Closed))
                        && (d.WaitsForDocumentId == null
                            || d.WaitsForDocument!.Status == ElectronicDocumentStatus.Validated
                            || d.WaitsForDocument!.Status == ElectronicDocumentStatus.ValidatedWithNotices))
            .OrderBy(d => d.Prefix)
            .ThenBy(d => d.Consecutive)
            .Select(d => d.PublicId)
            .Take(Math.Max(1, tanda))
            .ToListAsync(ct);
}

/// <summary>
/// Feature 023 (research R7): los documentos con seguimiento vencido —canónico, representación, correo—, sin morir y sin arrendamiento
/// vigente, los más viejos primero. Lee, no escribe. (nuevo)
/// </summary>
public static class SeguimientosPorProcesar
{
    public static async Task<IReadOnlyList<Guid>> ElegiblesAsync(IApplicationDbContext db, DateTime ahora, int tanda, CancellationToken ct) =>
        await db.ElectronicDocuments.AsNoTracking()
            .Where(d => d.FollowUpDueAt != null && d.FollowUpDueAt <= ahora && d.FollowUpDeadAt == null
                        && (d.LeaseUntil == null || d.LeaseUntil < ahora))
            .OrderBy(d => d.FollowUpDueAt)
            .Select(d => d.PublicId)
            .Take(Math.Max(1, tanda))
            .ToListAsync(ct);
}

/// <summary>
/// Feature 023 (research R8): lo que la base dice de la cola de emisión de una cooperativa —pendientes, edad del más viejo, muertos—, para las
/// métricas. Lo calcula el barrido; no es la profundidad del broker (ésa la da el propio RabbitMQ). (nuevo)
/// </summary>
public static class EstadoDeLaColaDeEmision
{
    public static async Task<(int Pendientes, double EdadSegundos, int Muertos)> LeerAsync(IApplicationDbContext db, DateTime ahora, CancellationToken ct)
    {
        var pendientes = db.ElectronicDocuments.AsNoTracking()
            .Where(d => d.DeadAt == null && d.NextAttemptAt != null
                        && (d.Status == ElectronicDocumentStatus.Pending || d.Status == ElectronicDocumentStatus.Sent
                            || d.Status == ElectronicDocumentStatus.DianContingency || d.Status == ElectronicDocumentStatus.IssuerContingency));
        var cuantos = await pendientes.CountAsync(ct);
        var masViejo = cuantos == 0 ? (DateTime?)null : await pendientes.MinAsync(d => (DateTime?)d.IssuedAt, ct);
        var muertos = await db.ElectronicDocuments.AsNoTracking().CountAsync(d => d.DeadAt != null || d.FollowUpDeadAt != null, ct);
        return (cuantos, masViejo is { } v ? Math.Max(0, (ahora - v).TotalSeconds) : 0, muertos);
    }
}

/// <summary>
/// El procesador de los documentos electrónicos (feature 012, I4, T731; contracts/dian.md §6.4; FR-083, T5, T6, T10, T47). Lo registra
/// <c>AddElectronicInvoicing</c> como singleton y <b>sólo</b> <c>API/Program.cs</c> lo arranca con <c>AddHostedService</c> (T749); el
/// DbMigrator nunca. <b>No escribe nada por sí mismo</b>: por cada cooperativa activa (<see cref="ITenantDirectory.ListActiveAsync"/>) y dentro de
/// <see cref="IEjecutorEnCooperativa"/> con el actor «Proceso de integración», canal <c>Process</c> y origen <c>Tarea:einvoicing.process</c>:
/// <list type="number">
/// <item>toma el arrendamiento <c>einvoicing.process</c> de <c>COR_BackgroundLeases</c> (si lo tiene otra réplica, salta la cooperativa) y lo
/// renueva entre documentos, con un presupuesto de <c>ElectronicInvoicing:Processor:BudgetSeconds</c> por cooperativa;</item>
/// <item>sondea los canales con fallas o con una contingencia 03 abierta por el circuito (<see cref="CircuitoDeCanal.SondearAsync"/>);</item>
/// <item>elige lo que toca (<see cref="DocumentosPorProcesar"/>) y envía <see cref="EmitElectronicDocumentCommand"/> por documento, cada uno en
/// su propio ámbito: el comando decide emitir, consultar o transmitir la contingencia, toma el arrendamiento de la fila y registra el
/// resultado.</item>
/// </list>
/// Una cooperativa que falla se registra y sigue la siguiente; un documento que falla también. (nuevo)
/// </summary>
public sealed class ProcesadorDeDocumentosElectronicos(
    IServiceScopeFactory ambitos,
    IEjecutorEnCooperativa ejecutor,
    IDateTimeService reloj,
    IOptions<ElectronicInvoicingOptions> opciones,
    ILogger<ProcesadorDeDocumentosElectronicos> logger,
    Func<bool>? baseLista = null) : BackgroundService
{
    private static readonly TimeSpan EsperaDeLaBase = TimeSpan.FromSeconds(5);
    private static readonly string Origen = Actor.OrigenDeTarea(NombresDeArrendamiento.FacturacionElectronica);

    private bool BaseLista => baseLista?.Invoke() ?? true;

    private OpcionesDelProcesador Opciones => opciones.Value.Processor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromSeconds(Math.Max(1, Opciones.IntervalSeconds));
        logger.LogInformation("ProcesadorDeDocumentosElectronicos iniciado: una pasada cada {Segundos} s.", intervalo.TotalSeconds);

        while (!BaseLista)
        {
            if (!await EsperarAsync(EsperaDeLaBase, stoppingToken)) return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CorrerUnaPasadaAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[FE.Procesador.PasadaFallida] La pasada falló; se reintenta en {Segundos} s.", intervalo.TotalSeconds);
            }

            if (!await EsperarAsync(intervalo, stoppingToken)) return;
        }
    }

    /// <summary>Una pasada por todas las cooperativas activas. La usan el ciclo y las pruebas.</summary>
    public Task CorrerUnaPasadaAsync(CancellationToken ct) => CorrerAsync(null, ct);

    /// <summary>Una pasada sólo por la cooperativa indicada (las e2e, con el ciclo apagado).</summary>
    public Task CorrerUnaPasadaAsync(Guid tenantPublicId, CancellationToken ct) => CorrerAsync(tenantPublicId, ct);

    private async Task CorrerAsync(Guid? soloEsta, CancellationToken ct)
    {
        if (!BaseLista) return;

        IReadOnlyList<TenantDirectoryEntry> cooperativas;
        await using (var ambito = ambitos.CreateAsyncScope())
        {
            cooperativas = await ambito.ServiceProvider.GetRequiredService<ITenantDirectory>().ListActiveAsync(ct);
        }

        foreach (var cooperativa in cooperativas.Where(c => soloEsta is null || c.PublicId == soloEsta))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await CorrerEnAsync(cooperativa, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[FE.Procesador.CooperativaFallida] {Cooperativa} ({TenantPublicId}) falló; siguen las demás.",
                    cooperativa.Name, cooperativa.PublicId);
            }
        }
    }

    private async Task CorrerEnAsync(TenantDirectoryEntry cooperativa, CancellationToken ct)
    {
        var ttl = TimeSpan.FromSeconds(Math.Max(10, Opciones.LeaseTtlSeconds));
        var tomado = false;
        var r = await EnCooperativaAsync(cooperativa,
            async (s, c) => tomado = await s.GetRequiredService<IArrendamientos>().ArrendarAsync(NombresDeArrendamiento.FacturacionElectronica, ttl, c), ct);
        if (r == ResultadoEnCooperativa.Omitida || !tomado) return;

        var cronometro = Stopwatch.StartNew();
        var presupuesto = TimeSpan.FromSeconds(Math.Max(1, Opciones.BudgetSeconds));
        try
        {
            await SondearAsync(cooperativa, ct);

            IReadOnlyList<Guid> documentos = [];
            await EnCooperativaAsync(cooperativa, async (s, c) => documentos = await DocumentosPorProcesar.ElegiblesAsync(
                s.GetRequiredService<IApplicationDbContext>(), reloj.UtcNow, Opciones.BatchSize, c), ct);

            foreach (var documento in documentos)
            {
                if (cronometro.Elapsed >= presupuesto) break;
                var sigue = false;
                await EnCooperativaAsync(cooperativa,
                    async (s, c) => sigue = await s.GetRequiredService<IArrendamientos>().RenovarAsync(NombresDeArrendamiento.FacturacionElectronica, ttl, c), ct);
                if (!sigue)
                {
                    logger.LogInformation("[FE.Procesador.ArrendamientoPerdido] Otra réplica tomó {Cooperativa}.", cooperativa.Name);
                    return;
                }
                await EmitirAsync(cooperativa, documento, ct);
            }

            // Feature 023 (research R7): los seguimientos vencidos (canónico, representación, correo), con el mismo presupuesto.
            IReadOnlyList<Guid> seguimientos = [];
            await EnCooperativaAsync(cooperativa, async (s, c) => seguimientos = await SeguimientosPorProcesar.ElegiblesAsync(
                s.GetRequiredService<IApplicationDbContext>(), reloj.UtcNow, Opciones.BatchSize, c), ct);
            foreach (var documento in seguimientos)
            {
                if (cronometro.Elapsed >= presupuesto) break;
                await SeguirAsync(cooperativa, documento, ct);
            }

            await MedirAsync(cooperativa, ct);
        }
        finally
        {
            await EnCooperativaAsync(cooperativa,
                (s, _) => s.GetRequiredService<IArrendamientos>().SoltarAsync(NombresDeArrendamiento.FacturacionElectronica, CancellationToken.None),
                CancellationToken.None);
        }
    }

    private async Task SondearAsync(TenantDirectoryEntry cooperativa, CancellationToken ct)
    {
        try
        {
            await EnCooperativaAsync(cooperativa, async (s, c) =>
            {
                foreach (var sondeo in await s.GetRequiredService<CircuitoDeCanal>().SondearAsync(c))
                    logger.LogInformation("[FE.Procesador.Sondeo] {Cooperativa} {Canal}: {Resultado} (abrió {Abrio}, cerró {Cerro}).",
                        cooperativa.Name, sondeo.Canal, sondeo.Resultado, sondeo.AbrioContingencia, sondeo.CerroContingencia);
            }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[FE.Procesador.SondeoFallido] {Cooperativa}: el sondeo del circuito falló; sigue la emisión.", cooperativa.Name);
        }
    }

    private async Task EmitirAsync(TenantDirectoryEntry cooperativa, Guid documento, CancellationToken ct)
    {
        try
        {
            await EnCooperativaAsync(cooperativa, async (s, c) =>
            {
                // Feature 023 (research R2): con cola, el barrido sólo avisa —lo hace el consumidor de cualquier réplica— y no vuelve a avisar lo
                // que ya está en la cola salvo que lleve demasiado sin atenderse. Sin cola, o si el broker no confirma, emite él mismo, como antes.
                if (await AvisarAsync(s, TiposDeAviso.FeEmitir, documento, conEstampa: true, c)) return;
                var r = await s.GetRequiredService<ISender>().Send(new EmitElectronicDocumentCommand(documento), c);
                if (r.IsFailure && r.Error.Code != ErroresDeDocumentosElectronicos.InProgressCode)
                    logger.LogWarning("[FE.Procesador.IntentoNoHecho] {Cooperativa} {Documento}: {Codigo} {Motivo}",
                        cooperativa.Name, documento, r.Error.Code, r.Error.Message);
                else if (r.IsSuccess && r.Value.Attempted)
                    logger.LogInformation("[FE.Procesador.Intento] {Cooperativa} {Documento}: {Resultado} → {Estado}.",
                        cooperativa.Name, documento, r.Value.Outcome, r.Value.Status);
            }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[FE.Procesador.DocumentoFallido] {Cooperativa} {Documento}: falló; lo retoma la pasada siguiente.",
                cooperativa.Name, documento);
        }
    }

    private async Task SeguirAsync(TenantDirectoryEntry cooperativa, Guid documento, CancellationToken ct)
    {
        try
        {
            await EnCooperativaAsync(cooperativa, async (s, c) =>
            {
                if (await AvisarAsync(s, TiposDeAviso.FeSeguimiento, documento, conEstampa: false, c)) return;
                var r = await s.GetRequiredService<ISender>().Send(new CompleteElectronicDocumentFollowUpCommand(documento), c);
                if (r.IsSuccess && !r.Value.Hecho)
                    logger.LogInformation("[FE.Procesador.SeguimientoPendiente] {Cooperativa} {Documento}: {Detalle}", cooperativa.Name, documento, r.Value.Detalle);
            }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[FE.Procesador.SeguimientoFallido] {Cooperativa} {Documento}: lo retoma la pasada siguiente.", cooperativa.Name, documento);
        }
    }

    /// <summary>
    /// Avisa a la cola si está disponible y el documento no tiene ya un aviso reciente (<c>EnqueuedAt</c> dentro de
    /// <see cref="IColaDeTrabajos.RepublicarTras"/>). Verdadero si el trabajo quedó en manos de la cola (avisado ahora o antes).
    /// </summary>
    private async Task<bool> AvisarAsync(IServiceProvider s, string tipo, Guid documento, bool conEstampa, CancellationToken c)
    {
        var cola = s.GetRequiredService<IColaDeTrabajos>();
        if (!cola.Disponible) return false;
        if (conEstampa)
        {
            var enCola = await s.GetRequiredService<IApplicationDbContext>().ElectronicDocuments.AsNoTracking()
                .Where(d => d.PublicId == documento).Select(d => d.EnqueuedAt).FirstOrDefaultAsync(c);
            if (enCola is { } cuando && cuando > reloj.UtcNow - cola.RepublicarTras) return true;
        }
        return await s.GetRequiredService<PublicacionDeAvisos>().PublicarAhoraAsync(tipo, documento.ToString("D"), "barrido", c);
    }

    private async Task MedirAsync(TenantDirectoryEntry cooperativa, CancellationToken ct)
    {
        try
        {
            await EnCooperativaAsync(cooperativa, async (s, c) =>
            {
                var (pendientes, edad, muertos) = await EstadoDeLaColaDeEmision.LeerAsync(s.GetRequiredService<IApplicationDbContext>(), reloj.UtcNow, c);
                s.GetRequiredService<IMedidorDeTrabajos>().Estado(TiposDeAviso.ColaEmision, cooperativa.PublicId, cooperativa.Name, pendientes, edad, muertos);
            }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[FE.Procesador.MedicionFallida] {Cooperativa}: no se pudo medir la cola.", cooperativa.Name);
        }
    }

    private Task<ResultadoEnCooperativa> EnCooperativaAsync(TenantDirectoryEntry cooperativa, Func<IServiceProvider, CancellationToken, Task> trabajo,
        CancellationToken ct) =>
        ejecutor.EjecutarAsync(cooperativa, Actor.ProcesoDeIntegracion(Origen), Origen, trabajo, ct);

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
