using System.Diagnostics;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Alerts.RaiseAlert;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Inventory.Parameters;
using IngenIA365ERP.Persistence.Initialization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace IngenIA365ERP.API.Integration;

/// <summary>
/// El despachador de los mensajes de integración (feature 012, I2, T527; contracts/mensajes.md §11 y §13,
/// contracts/contabilidad.md §5; decisiones-transversales T10, T11, T12; FR-076, FR-083). Registrado sólo en
/// <c>Program.cs</c>, condicionado a <c>Integration:Dispatcher:Enabled</c>; el DbMigrator nunca lo arranca.
///
/// <para>
/// <b>No escribe nada por sí mismo</b> (Principios III y X, <c>NingunTrabajoDeFondoOperaSinCooperativa</c>): lee y envía
/// comandos por <see cref="ISender"/> dentro de <see cref="IEjecutorEnCooperativa"/>, para que toda escritura pase por
/// <c>ValidationBehavior</c> y <c>AuditBehavior</c> con la base, la auditoría y el actor de la cooperativa. Una pasada por
/// cooperativa activa (<see cref="ITenantDirectory.ListActiveAsync"/>):
/// </para>
/// <list type="number">
/// <item>toma el arrendamiento <c>integration.dispatch</c> de <c>COR_BackgroundLeases</c> (si lo tiene otra réplica, la
/// salta) y lo renueva entre tandas y grupos; tiene un presupuesto de <c>Integration:Dispatcher:BudgetSeconds</c> (60 s) por
/// cooperativa, y lo que no alcanzó lo toma la pasada siguiente;</item>
/// <item>crea los lotes <c>Scheduled</c> vencidos con <c>ScheduleIntegrationBatchesCommand</c> y levanta
/// <c>Integracion.LoteNoCorrio</c> con <c>RaiseAlertCommand</c> por cada lote que ese comando devuelve atrasado más
/// <c>Integration:Dispatcher:LateToleranceMinutes</c>;</item>
/// <item>entrega lo en línea elegible (<see cref="EntregasElegibles.EnLineaAsync"/>), una unidad por vez, con el actor
/// «Proceso de integración» y origen <c>Mensaje:{id}</c>;</item>
/// <item>corre cada lote <c>Requested</c> o <c>Running</c>: lo pasa a <c>Running</c> con <c>StartIntegrationBatchCommand</c>,
/// ejecuta en pasadas sucesivas lo que ya tiene sus dependencias satisfechas (<see cref="EntregasElegibles.DelLoteAsync"/> y
/// <see cref="IDestinoDeMensajes.PlanearLote"/>: una unidad por documento o un grupo resumido) con el proceso (origen
/// <c>Lote:{número}</c>) o la persona que ordenó el lote, y lo cierra con <c>CloseIntegrationBatchCommand</c> con los totales
/// que da el destino; un lote con reintentos pendientes sigue en curso hasta una pasada posterior.</item>
/// </list>
/// <para>
/// Cada unidad o grupo se consume en su propia llamada a <see cref="IEjecutorEnCooperativa"/> (un ámbito DI nuevo) y su
/// resultado se registra con <c>RegisterDeliveryResultCommand</c> en <b>otro</b> ámbito (T11): el consumidor nunca toca las
/// tablas de la plataforma. Un destino sin <see cref="IDestinoDeMensajes"/> registrado se salta, y <c>Lending</c> además
/// mientras <c>Cartera.IntegracionHabilitadaDesde</c> esté vacío (D-02). Nada falla en silencio: una excepción del destino
/// se registra como <c>Retry</c> y una cooperativa que falla se registra y sigue la siguiente.
/// </para>
/// <para>
/// Despierta con <see cref="ISenalDeMensajes"/> (un guardado con mensajes) o cada <c>Integration:Dispatcher:IntervalSeconds</c>
/// (5 s): un aviso perdido sólo retrasa la entrega hasta el siguiente sondeo.
/// </para>
/// </summary>
public sealed class DespachadorDeMensajes(
    IServiceScopeFactory ambitos,
    IEjecutorEnCooperativa ejecutor,
    ISenalDeMensajes senal,
    DatabaseReadiness baseDeDatos,
    IDateTimeService reloj,
    IOptions<IntegrationOptions> opciones,
    ILogger<DespachadorDeMensajes> logger) : BackgroundService
{
    private static readonly TimeSpan EsperaDeLaBase = TimeSpan.FromSeconds(5);
    private static readonly string OrigenDelArrendamiento = Actor.OrigenDeTarea(NombresDeArrendamiento.Despacho);

    /// <summary>La réplica que registra cada intento (<c>COR_IntegrationDeliveryAttempts.Instance</c>).</summary>
    private readonly string _instancia = Recortar($"{Environment.MachineName}:{Environment.ProcessId}", 100);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromSeconds(opciones.Value.Dispatcher.IntervalSeconds);
        logger.LogInformation("DespachadorDeMensajes iniciado: sondeo cada {Segundos} s y al aviso de mensajes nuevos.", intervalo.TotalSeconds);

        while (!baseDeDatos.IsReady)
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
                logger.LogError(ex, "[Despacho.PasadaFallida] La pasada del despachador falló; se reintenta en {Segundos} s.", intervalo.TotalSeconds);
            }

            if (!await EsperarAvisoAsync(intervalo, stoppingToken)) return;
        }
    }

    /// <summary>Una pasada por todas las cooperativas activas. La usan el ciclo y las pruebas.</summary>
    public Task CorrerUnaPasadaAsync(CancellationToken ct) => CorrerAsync(null, ct);

    /// <summary>Una pasada sólo por la cooperativa indicada (la fixture de pruebas, que tiene el ciclo apagado).</summary>
    public Task CorrerUnaPasadaAsync(Guid tenantPublicId, CancellationToken ct) => CorrerAsync(tenantPublicId, ct);

    private async Task CorrerAsync(Guid? soloEsta, CancellationToken ct)
    {
        if (!baseDeDatos.IsReady) return;

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
                logger.LogError(ex, "[Despacho.CooperativaFallida] El despacho de {Cooperativa} ({TenantPublicId}) falló; siguen las demás.",
                    cooperativa.Name, cooperativa.PublicId);
            }
        }
    }

    private async Task CorrerEnAsync(TenantDirectoryEntry cooperativa, CancellationToken ct)
    {
        var configuracion = opciones.Value.Dispatcher;
        var ttl = TimeSpan.FromSeconds(configuracion.LeaseTtlSeconds);
        var tomado = false;
        var resultado = await ejecutor.EjecutarAsync(cooperativa, Actor.ProcesoDeIntegracion(OrigenDelArrendamiento), OrigenDelArrendamiento,
            async (servicios, c) => tomado = await servicios.GetRequiredService<IArrendamientos>().ArrendarAsync(NombresDeArrendamiento.Despacho, ttl, c), ct);
        if (resultado == ResultadoEnCooperativa.Omitida || !tomado) return;

        var pasada = new Pasada(cooperativa, Stopwatch.StartNew(), TimeSpan.FromSeconds(configuracion.BudgetSeconds), ttl);
        try
        {
            await ProgramarLotesAsync(pasada, ct);
            if (!await EntregarEnLineaAsync(pasada, ct)) return;
            await CorrerLotesAsync(pasada, ct);
        }
        finally
        {
            await ejecutor.EjecutarAsync(cooperativa, Actor.ProcesoDeIntegracion(OrigenDelArrendamiento), OrigenDelArrendamiento,
                (servicios, _) => servicios.GetRequiredService<IArrendamientos>().SoltarAsync(NombresDeArrendamiento.Despacho, CancellationToken.None),
                CancellationToken.None);
        }
    }

    /// <summary>Crea los lotes programados vencidos y levanta la alerta de los que no corrieron a su hora.</summary>
    private async Task ProgramarLotesAsync(Pasada pasada, CancellationToken ct)
    {
        LotesProgramadosDto? programados = null;
        await ejecutor.EjecutarAsync(pasada.Cooperativa, Actor.ProcesoDeIntegracion(OrigenDelArrendamiento), OrigenDelArrendamiento, async (servicios, c) =>
        {
            var r = await servicios.GetRequiredService<ISender>().Send(
                new ScheduleIntegrationBatchesCommand(opciones.Value.Dispatcher.LateToleranceMinutes), c);
            if (r.IsSuccess) programados = r.Value;
            else
                logger.LogWarning("[Despacho.LotesNoProgramados] {Cooperativa}: {Codigo} {Motivo}", pasada.Cooperativa.Name, r.Error.Code, r.Error.Message);
        }, ct);
        if (programados is null) return;

        foreach (var creado in programados.Created)
        {
            logger.LogInformation("[Despacho.LoteProgramado] {Cooperativa}: lote {Numero} ({Clave}, {Franja:yyyy-MM-dd HH:mm}) con {Mensajes} mensajes.",
                pasada.Cooperativa.Name, creado.Number, creado.ScheduleKey, creado.ScheduledFor, creado.Messages);
        }

        foreach (var atrasado in programados.Late)
        {
            var origen = Actor.OrigenDeLote(atrasado.Number.ToString(System.Globalization.CultureInfo.InvariantCulture));
            await ejecutor.EjecutarAsync(pasada.Cooperativa, Actor.ProcesoDeIntegracion(origen), origen, async (servicios, c) =>
            {
                var r = await servicios.GetRequiredService<ISender>().Send(new RaiseAlertCommand(new AlertaALevantar(
                    TiposDeAlerta.LoteNoCorrio,
                    $"Lote {atrasado.Number} sin correr",
                    $"El lote programado {atrasado.Number} ({atrasado.ScheduleKey}) debía correr a las {atrasado.ScheduledFor:yyyy-MM-dd HH:mm} (hora de Colombia) " +
                    $"y no ha corrido pasados {opciones.Value.Dispatcher.LateToleranceMinutes} minutos de tolerancia. Revise la bandeja de mensajes y los lotes de contabilización.",
                    EntityType: "IntegrationBatch",
                    EntityPublicId: atrasado.BatchPublicId,
                    DedupKey: $"{TiposDeAlerta.LoteNoCorrio}:{atrasado.BatchPublicId:N}")), c);
                if (r.IsFailure)
                    logger.LogWarning("[Despacho.AlertaNoLevantada] {Tipo} del lote {Numero}: {Codigo} {Motivo}",
                        TiposDeAlerta.LoteNoCorrio, atrasado.Number, r.Error.Code, r.Error.Message);
            }, ct);
        }
    }

    /// <summary>Lo en línea elegible de cada destino con consumidor, por tandas. Falso si se perdió el arrendamiento.</summary>
    private async Task<bool> EntregarEnLineaAsync(Pasada pasada, CancellationToken ct)
    {
        foreach (var destino in await DestinosHabilitadosAsync(pasada, ct))
        {
            var intentadas = new HashSet<Guid>();
            while (pasada.QuedaTiempo)
            {
                if (!await RenovarAsync(pasada, ct)) return false;

                IReadOnlyList<UnidadDeConsumo> unidades = [];
                await ejecutor.EjecutarAsync(pasada.Cooperativa, Actor.ProcesoDeIntegracion(OrigenDelArrendamiento), OrigenDelArrendamiento,
                    async (servicios, c) => unidades = await servicios.GetRequiredService<EntregasElegibles>()
                        .EnLineaAsync(destino, reloj.UtcNow, opciones.Value.Dispatcher.BatchSize, c), ct);

                // Lo que ya se intentó en esta pasada y volvió elegible (p. ej. un registro que otra réplica ganó) espera a la
                // siguiente: una pasada no gira sobre la misma unidad.
                var nuevas = unidades.Where(u => intentadas.Add(u.MessagePublicIds[0])).ToList();
                if (nuevas.Count == 0) break;

                foreach (var unidad in nuevas)
                {
                    if (!pasada.QuedaTiempo) break;
                    var origen = Actor.OrigenDeMensaje(unidad.MessagePublicIds[0]);
                    await ProcesarAsync(pasada, destino, TrabajoDeConsumo.DeUnaUnidad(unidad), Actor.ProcesoDeIntegracion(origen), origen, ct);
                }
            }
        }

        return true;
    }

    /// <summary>Los lotes abiertos de los destinos habilitados, del más viejo al más nuevo.</summary>
    private async Task CorrerLotesAsync(Pasada pasada, CancellationToken ct)
    {
        var destinos = await DestinosHabilitadosAsync(pasada, ct);
        if (destinos.Count == 0) return;

        IReadOnlyList<LoteAbierto> lotes = [];
        await ejecutor.EjecutarAsync(pasada.Cooperativa, Actor.ProcesoDeIntegracion(OrigenDelArrendamiento), OrigenDelArrendamiento,
            async (servicios, c) => lotes = await servicios.GetRequiredService<IApplicationDbContext>().IntegrationBatches.AsNoTracking()
                .Where(b => !b.IsDeleted && destinos.Contains(b.Destination)
                            && (b.Status == BatchStatus.Requested || b.Status == BatchStatus.Running))
                .OrderBy(b => b.Number)
                .Select(b => new LoteAbierto(b.PublicId, b.Number, b.Destination, b.Status, b.RequestedByKind, b.RequestedByUserId,
                    b.RequestedByCentralUserId, b.RequestedByName, b.RequestedByEmail, b.RequestedByIp, b.Reason))
                .ToListAsync(c), ct);

        foreach (var lote in lotes)
        {
            if (!pasada.QuedaTiempo || !await RenovarAsync(pasada, ct)) return;
            try
            {
                await CorrerLoteAsync(pasada, lote, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[Despacho.LoteFallido] El lote {Numero} de {Cooperativa} falló; sigue en curso y lo retoma la pasada siguiente.",
                    lote.Number, pasada.Cooperativa.Name);
            }
        }
    }

    private async Task CorrerLoteAsync(Pasada pasada, LoteAbierto lote, CancellationToken ct)
    {
        var origen = Actor.OrigenDeLote(lote.Number.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var actor = ActorDelLote(lote, origen);

        if (lote.Status == BatchStatus.Requested)
        {
            var iniciado = true;
            await ejecutor.EjecutarAsync(pasada.Cooperativa, actor, origen, async (servicios, c) =>
            {
                var r = await servicios.GetRequiredService<ISender>().Send(new StartIntegrationBatchCommand(lote.PublicId), c);
                if (r.IsFailure)
                {
                    iniciado = false;
                    logger.LogWarning("[Despacho.LoteNoIniciado] Lote {Numero}: {Codigo} {Motivo}", lote.Number, r.Error.Code, r.Error.Message);
                }
            }, ct);
            if (!iniciado) return;
        }

        var intentadas = new HashSet<Guid>();
        while (pasada.QuedaTiempo)
        {
            IReadOnlyList<TrabajoDeConsumo> trabajos = [];
            await ejecutor.EjecutarAsync(pasada.Cooperativa, actor, origen, async (servicios, c) =>
            {
                var unidades = await servicios.GetRequiredService<EntregasElegibles>()
                    .DelLoteAsync(lote.PublicId, reloj.UtcNow, opciones.Value.Dispatcher.BatchSize, c);
                if (unidades.Count == 0) return;

                var leidos = await servicios.GetRequiredService<IMensajesEntrantes>()
                    .LeerAsync(unidades.SelectMany(u => u.MessagePublicIds).ToList(), lote.Destination, c);
                if (leidos.IsFailure)
                {
                    logger.LogWarning("[Despacho.LoteIlegible] Lote {Numero}: {Codigo} {Motivo}", lote.Number, leidos.Error.Code, leidos.Error.Message);
                    return;
                }

                var destino = Destino(servicios, lote.Destination);
                if (destino is not null) trabajos = destino.PlanearLote(leidos.Value);
            }, ct);

            var nuevos = trabajos.Where(t => t.Unidades.Any(u => !intentadas.Contains(u.MessagePublicIds[0]))).ToList();
            if (nuevos.Count == 0) break;

            foreach (var trabajo in nuevos)
            {
                if (!pasada.QuedaTiempo || !await RenovarAsync(pasada, ct)) return;
                foreach (var unidad in trabajo.Unidades) intentadas.Add(unidad.MessagePublicIds[0]);
                await ProcesarAsync(pasada, lote.Destination, trabajo, actor, origen, ct);
            }
        }

        await CerrarLoteAsync(pasada, lote, actor, origen, ct);
    }

    /// <summary>Cierra el lote con los totales del destino. Con entregas por procesar sigue en curso (no es un error).</summary>
    private async Task CerrarLoteAsync(Pasada pasada, LoteAbierto lote, Actor actor, string origen, CancellationToken ct)
    {
        await ejecutor.EjecutarAsync(pasada.Cooperativa, actor, origen, async (servicios, c) =>
        {
            var destino = Destino(servicios, lote.Destination);
            var totales = destino is null ? TotalesDeLoteEnDestino.Cero : await destino.TotalesDelLoteAsync(lote.PublicId, c);
            var r = await servicios.GetRequiredService<ISender>().Send(
                new CloseIntegrationBatchCommand(lote.PublicId, totales.Debit, totales.Credit), c);
            if (r.IsFailure && r.Error.Code != CodigoLoteConPendientes)
                logger.LogWarning("[Despacho.LoteNoCerrado] Lote {Numero}: {Codigo} {Motivo}", lote.Number, r.Error.Code, r.Error.Message);
            else if (r.IsSuccess && r.Value)
                logger.LogInformation("[Despacho.LoteCerrado] Lote {Numero} de {Cooperativa} cerrado.", lote.Number, pasada.Cooperativa.Name);
        }, ct);
    }

    private static readonly string CodigoLoteConPendientes = ErroresDeIntegracion.LoteConEntregasPendientes(Guid.Empty, 0).Code;

    /// <summary>
    /// Consume un trabajo en su propio ámbito y registra el resultado de cada unidad en otro (T11). Una excepción del consumo es
    /// un <c>Retry</c>: el mensaje vuelve a intentarse con espera creciente, nunca se pierde.
    /// </summary>
    private async Task ProcesarAsync(Pasada pasada, string destino, TrabajoDeConsumo trabajo, Actor actor, string origen, CancellationToken ct)
    {
        var inicio = reloj.UtcNow;
        IReadOnlyList<ResultadoDeUnidad>? resultados = null;
        try
        {
            var r = await ejecutor.EjecutarAsync(pasada.Cooperativa, actor, origen, async (servicios, c) =>
            {
                var consumidor = Destino(servicios, destino);
                if (consumidor is not null) resultados = await consumidor.ConsumirAsync(trabajo, c);
            }, ct);
            if (r == ResultadoEnCooperativa.Omitida) return;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Despacho.ConsumoFallido] {Destino} no pudo consumir {Origen}: se registra como reintento.", destino, origen);
            resultados = trabajo.Unidades.Select(u => new ResultadoDeUnidad(u, new ResultadoDeConsumo.Retry(Recortar(ex.Message, 1000)))).ToList();
        }

        if (resultados is null) return;
        var fin = reloj.UtcNow;
        if (fin < inicio) fin = inicio;

        foreach (var resultado in resultados)
        {
            try
            {
                await ejecutor.EjecutarAsync(pasada.Cooperativa, actor, origen, async (servicios, c) =>
                {
                    var r = await servicios.GetRequiredService<ISender>().Send(new RegisterDeliveryResultCommand(
                        destino, resultado.Unidad.MessagePublicIds, resultado.Resultado, resultado.Unidad.IntentosLeidos, inicio, fin, _instancia), c);
                    if (r.IsFailure)
                        logger.LogWarning("[Despacho.ResultadoNoRegistrado] {Origen}: {Codigo} {Motivo}", origen, r.Error.Code, r.Error.Message);
                }, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // El consumo ya dejó su recibo: la siguiente entrega lo reconoce como AlreadyProcessed y registra entonces.
                logger.LogError(ex, "[Despacho.ResultadoNoRegistrado] {Origen}: no se pudo registrar el resultado; la pasada siguiente lo reintenta.", origen);
            }
        }
    }

    /// <summary>
    /// Los destinos que el despachador recorre en esta cooperativa: los que tienen consumidor registrado y, para Cartera,
    /// sólo desde <c>Cartera.IntegracionHabilitadaDesde</c> (vacío = todavía no).
    /// </summary>
    private async Task<IReadOnlyList<string>> DestinosHabilitadosAsync(Pasada pasada, CancellationToken ct)
    {
        if (pasada.Destinos is { } yaLeidos) return yaLeidos;

        var habilitados = new List<string>();
        await ejecutor.EjecutarAsync(pasada.Cooperativa, Actor.ProcesoDeIntegracion(OrigenDelArrendamiento), OrigenDelArrendamiento, async (servicios, c) =>
        {
            foreach (var destino in servicios.GetRequiredService<EntregasElegibles>().DestinosConConsumidor.Order(StringComparer.Ordinal))
            {
                if (destino == IntegrationDestinations.Lending && !await CarteraHabilitadaAsync(servicios, c)) continue;
                habilitados.Add(destino);
            }
        }, ct);

        pasada.Destinos = habilitados;
        return habilitados;
    }

    private async Task<bool> CarteraHabilitadaAsync(IServiceProvider servicios, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var leido = await servicios.GetRequiredService<ILectorDeParametros>()
            .LeerAsync(ParametrosDeInventario.Modulo, ParametrosDeInventario.CarteraIntegracionHabilitadaDesde, hoy, ct: ct);
        if (leido.IsFailure || string.IsNullOrWhiteSpace(leido.Value.Texto)) return false;
        return leido.Value.Valor is not DateOnly desde || desde <= hoy;
    }

    private static IDestinoDeMensajes? Destino(IServiceProvider servicios, string destino) =>
        servicios.GetServices<IDestinoDeMensajes>().FirstOrDefault(d => string.Equals(d.Destino, destino, StringComparison.Ordinal));

    /// <summary>Renueva el arrendamiento; falso si otra réplica lo tomó (ella sigue con lo que falte).</summary>
    private async Task<bool> RenovarAsync(Pasada pasada, CancellationToken ct)
    {
        var sigue = false;
        await ejecutor.EjecutarAsync(pasada.Cooperativa, Actor.ProcesoDeIntegracion(OrigenDelArrendamiento), OrigenDelArrendamiento,
            async (servicios, c) => sigue = await servicios.GetRequiredService<IArrendamientos>().RenovarAsync(NombresDeArrendamiento.Despacho, pasada.Ttl, c), ct);
        if (!sigue)
            logger.LogInformation("[Despacho.ArrendamientoPerdido] Otra réplica tomó el despacho de {Cooperativa}.", pasada.Cooperativa.Name);
        return sigue;
    }

    /// <summary>
    /// Quien procesa un lote (contracts/contabilidad.md §10): el proceso en los programados, de cierre de turno o de período; la
    /// persona que lo ordenó —con su IP y motivo de la orden— en el manual, el reproceso y el envío posterior (FR-083).
    /// </summary>
    private static Actor ActorDelLote(LoteAbierto lote, string origen) => lote.RequestedByKind == ActorKind.Person
        ? new Actor(ActorKind.Person, lote.RequestedByUserId, null, lote.RequestedByCentralUserId,
            string.IsNullOrWhiteSpace(lote.RequestedByName) ? "Persona sin nombre" : lote.RequestedByName, lote.RequestedByEmail,
            ExecutionChannel.Process, origen, lote.RequestedByIp, lote.Reason)
        : Actor.ProcesoDeIntegracion(origen);

    /// <summary>Espera el aviso de mensajes nuevos o el intervalo; falso si se pidió detener el servicio.</summary>
    private async Task<bool> EsperarAvisoAsync(TimeSpan intervalo, CancellationToken ct)
    {
        using var espera = CancellationTokenSource.CreateLinkedTokenSource(ct);
        espera.CancelAfter(intervalo);
        try
        {
            await senal.Avisos.WaitToReadAsync(espera.Token);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            // Venció el intervalo sin avisos: toca el sondeo.
            return true;
        }

        // Con un aviso basta para despertar: se descartan los que se juntaron mientras tanto.
        while (senal.Avisos.TryRead(out _))
        {
        }

        return true;
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

    private static string Recortar(string texto, int largo) => texto.Length <= largo ? texto : texto[..largo];

    /// <summary>El estado de la pasada por una cooperativa: el presupuesto, el arrendamiento y los destinos ya leídos.</summary>
    private sealed class Pasada(TenantDirectoryEntry cooperativa, Stopwatch reloj, TimeSpan presupuesto, TimeSpan ttl)
    {
        public TenantDirectoryEntry Cooperativa { get; } = cooperativa;

        public TimeSpan Ttl { get; } = ttl;

        public IReadOnlyList<string>? Destinos { get; set; }

        public bool QuedaTiempo => reloj.Elapsed < presupuesto;
    }

    private sealed record LoteAbierto(
        Guid PublicId,
        long Number,
        string Destination,
        BatchStatus Status,
        ActorKind RequestedByKind,
        int? RequestedByUserId,
        Guid? RequestedByCentralUserId,
        string? RequestedByName,
        string? RequestedByEmail,
        string? RequestedByIp,
        string? Reason);
}
