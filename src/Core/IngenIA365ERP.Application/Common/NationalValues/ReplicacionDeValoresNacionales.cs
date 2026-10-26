using System.Text.Json;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Core.NationalValues;
using IngenIA365ERP.Domain.Enums.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Common.NationalValues;

/// <summary>Lo que dejó una pasada de la réplica. (nuevo)</summary>
/// <param name="CentralDisponible">Falso si la central no respondió (entonces <paramref name="Motivo"/> dice por qué).</param>
/// <param name="Motivo">Por qué no copió: la central no respondió o un efecto falló. Nulo si todo anduvo.</param>
/// <param name="Valores">Filas copiadas a <c>COR_NationalValues</c>.</param>
/// <param name="Documentos">Filas copiadas a <c>COR_RegulatoryDocuments</c>.</param>
/// <param name="DesdeSequence">Primera <c>Sequence</c> central copiada.</param>
/// <param name="HastaSequence">Última <c>Sequence</c> central copiada.</param>
/// <param name="ReplicacionPublicId">La fila de <c>COR_NationalValueReplications</c>; nula si no copió nada.</param>
/// <param name="Efectos">Lo que hizo cada <see cref="IEfectoDeReplicacion"/>, por nombre.</param>
public sealed record ResultadoDeReplicacion(
    bool CentralDisponible,
    string? Motivo,
    int Valores,
    int Documentos,
    long? DesdeSequence,
    long? HastaSequence,
    Guid? ReplicacionPublicId,
    IReadOnlyDictionary<string, EfectoAplicado> Efectos) : Behaviors.IResultadoConEfecto
{
    public bool Copio => Valores + Documentos > 0;

    /// <summary>
    /// La tarea <c>valores-nacionales.replicar</c> envía el comando en cada pasada del programador (cada minuto): una pasada
    /// <see cref="SinNovedad"/> no escribe nada y no se audita (incidente del 2026-10-26). Lo que copió se audita —además del evento
    /// <c>NationalValues.Replicated</c>—, y también una pasada en que la central no respondió o un efecto falló: eso es un hecho.
    /// </summary>
    public bool HuboEfecto => Copio || !CentralDisponible || Motivo is not null;

    public static ResultadoDeReplicacion SinNovedad { get; } = new(true, null, 0, 0, null, null, null, new Dictionary<string, EfectoAplicado>());

    public static ResultadoDeReplicacion Fallida(bool centralDisponible, string motivo) =>
        new(centralDisponible, motivo, 0, 0, null, null, null, new Dictionary<string, EfectoAplicado>());
}

/// <summary>
/// El <b>núcleo único</b> de la réplica de valores nacionales (feature 013, T39, R4; data-model parte 3 §4.1–§4.3). Las tres
/// entradas —la tarea <c>valores-nacionales.replicar</c>, la semilla del alta <c>NationalValuesReplicaSeeder</c> y, desde sus
/// fases, el primer paso del ciclo diario y del cierre de Cartera— llaman a <see cref="AplicarAsync"/>:
///
/// <list type="number">
/// <item>la marca de agua es <c>MAX(SourceSequence)</c> de las dos copias; se lee de la central lo de <c>Sequence</c> mayor, en
/// orden (la central numera con un contador en la misma transacción del registro, así el orden de numeración es el de
/// confirmación y no hay fila que confirme tarde con un número ya pasado);</item>
/// <item><c>SourcePublicId</c> ya copiado se salta (guarda de idempotencia): repetir no duplica;</item>
/// <item>cada copia lleva <c>KnownAt</c> = ahora (R en la cooperativa) y la réplica que la trajo;</item>
/// <item>corren los <see cref="IEfectoDeReplicacion"/> registrados sobre el mismo contexto; si uno lanza, no se copia nada;</item>
/// <item>la bitácora <c>COR_NationalValueReplications</c> y el evento <c>NationalValues.Replicated</c> se escriben sólo si se copió
/// algo, y todo va en <b>un solo</b> <c>SaveChanges</c>;</item>
/// <item>después de guardar se levantan las alertas que pidieron los efectos.</item>
/// </list>
/// Si la central no responde devuelve el motivo sin lanzar. Es el único escritor de las copias. (nuevo)
/// </summary>
public sealed class ReplicacionDeValoresNacionales
{
    /// <summary>El evento de auditoría de una réplica que copió algo (módulo <c>NationalValues</c>).</summary>
    public const string EventoReplicada = "NationalValues.Replicated";

    /// <summary>El módulo de auditoría de los valores nacionales (lo registra <c>ModuloDeAuditoria</c> en T0092).</summary>
    public const string ModuloDeAuditoria = "NationalValues";

    /// <summary>Filas centrales por pasada: una base nueva copia todo en pocas pasadas sin cargar la central entera.</summary>
    public const int MaximoPorPasada = 5000;

    private const int LargoDelResumen = 2000;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IApplicationDbContext _db;
    private readonly IRegistroCentralDeValoresNacionales _central;
    private readonly IReadOnlyList<IEfectoDeReplicacion> _efectos;
    private readonly IDateTimeService _reloj;
    private readonly ILogger _logger;
    private readonly IServiceProvider? _servicios;

    /// <param name="servicios">
    /// Los del ámbito de la cooperativa, para la auditoría y las alertas. Nulo en la semilla del alta, que corre con un contexto
    /// armado a mano: entonces la réplica no levanta alertas y su evento queda sólo en el log.
    /// </param>
    public ReplicacionDeValoresNacionales(
        IApplicationDbContext db,
        IRegistroCentralDeValoresNacionales central,
        IEnumerable<IEfectoDeReplicacion> efectos,
        IDateTimeService reloj,
        ILogger<ReplicacionDeValoresNacionales>? logger = null,
        IServiceProvider? servicios = null)
    {
        _db = db;
        _central = central;
        _efectos = efectos.ToList();
        _reloj = reloj;
        _logger = logger ?? (ILogger)NullLogger.Instance;
        _servicios = servicios;
    }

    public async Task<ResultadoDeReplicacion> AplicarAsync(NationalValueReplicationOrigin origen, string actor, CancellationToken ct)
    {
        var inicio = _reloj.UtcNow;
        var marca = Math.Max(
            await _db.NationalValues.IgnoreQueryFilters().Select(v => (long?)v.SourceSequence).MaxAsync(ct) ?? 0L,
            await _db.RegulatoryDocuments.IgnoreQueryFilters().Select(d => (long?)d.SourceSequence).MaxAsync(ct) ?? 0L);

        LecturaDeLaCentral lectura;
        try
        {
            lectura = await _central.LeerDesdeAsync(marca, MaximoPorPasada, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "[ValoresNacionales.CentralSinRespuesta] La réplica ({Origen}) no pudo leer la central desde la Sequence {Marca}.",
                origen, marca);
            return ResultadoDeReplicacion.Fallida(false, $"El registro central de valores nacionales no respondió: {ex.Message}");
        }

        if (lectura.EstaVacia) return ResultadoDeReplicacion.SinNovedad;

        // Guarda de idempotencia: lo ya copiado (por SourcePublicId) no se vuelve a copiar.
        var ids = lectura.Valores.Select(v => v.PublicId).Concat(lectura.Documentos.Select(d => d.PublicId)).ToList();
        var copiados = (await _db.NationalValues.IgnoreQueryFilters().Where(v => ids.Contains(v.SourcePublicId)).Select(v => v.SourcePublicId).ToListAsync(ct))
            .Concat(await _db.RegulatoryDocuments.IgnoreQueryFilters().Where(d => ids.Contains(d.SourcePublicId)).Select(d => d.SourcePublicId).ToListAsync(ct))
            .ToHashSet();
        var valores = lectura.Valores.Where(v => !copiados.Contains(v.PublicId)).OrderBy(v => v.Sequence).ToList();
        var documentos = lectura.Documentos.Where(d => !copiados.Contains(d.PublicId)).OrderBy(d => d.Sequence).ToList();
        if (valores.Count == 0 && documentos.Count == 0) return ResultadoDeReplicacion.SinNovedad;

        var ahora = _reloj.UtcNow;
        var secuencias = valores.Select(v => v.Sequence).Concat(documentos.Select(d => d.Sequence)).ToList();
        var nombreDelActor = string.IsNullOrWhiteSpace(actor) ? Execution.Actor.NombreDelProceso : actor.Trim();
        var copiasDeValores = valores.Select(v => new NationalValue
        {
            Code = v.Code, Key = v.Key, ValidFrom = v.ValidFrom, ValidTo = v.ValidTo, ReferenceDate = v.ReferenceDate,
            Value = v.Value, Unit = v.Unit, Label = v.Label, Source = v.Source, Reason = v.Reason,
            SourcePublicId = v.PublicId, SourceSequence = v.Sequence, SupersedesSourcePublicId = v.SupersedesPublicId,
            CentralRegisteredAt = v.RegisteredAt, KnownAt = ahora,
            CreatedAt = ahora, CreatedBy = Recortar(nombreDelActor, 150),
        }).ToList();
        var copiasDeDocumentos = documentos.Select(d => new RegulatoryDocument
        {
            Kind = d.Kind, Code = d.Code, ValidFrom = d.ValidFrom, ValidTo = d.ValidTo, Content = d.Content,
            ContentSha256 = d.ContentSha256, SchemaVersion = d.SchemaVersion, Verified = d.Verified, Source = d.Source, Reason = d.Reason,
            SourcePublicId = d.PublicId, SourceSequence = d.Sequence, SupersedesSourcePublicId = d.SupersedesPublicId,
            CentralRegisteredAt = d.RegisteredAt, KnownAt = ahora,
            CreatedAt = ahora, CreatedBy = Recortar(nombreDelActor, 150),
        }).ToList();

        var lote = new LoteReplicado(copiasDeValores, copiasDeDocumentos, ahora, _reloj.HoyLocal);
        var efectos = new Dictionary<string, EfectoAplicado>(StringComparer.Ordinal);
        foreach (var efecto in _efectos)
        {
            try
            {
                efectos[efecto.Nombre] = await efecto.AlReplicarAsync(_db, lote, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[ValoresNacionales.EfectoFallido] El efecto {Efecto} falló; la réplica ({Origen}) no copia nada.", efecto.Nombre, origen);
                _db.DescartarCambios();
                return ResultadoDeReplicacion.Fallida(true, $"El efecto {efecto.Nombre} falló: {ex.Message}");
            }
        }

        var replicacion = new NationalValueReplication
        {
            FromSequence = secuencias.Min(),
            ToSequence = secuencias.Max(),
            ValueCount = copiasDeValores.Count,
            DocumentCount = copiasDeDocumentos.Count,
            Origin = origen,
            StartedAt = inicio,
            FinishedAt = _reloj.UtcNow,
            EffectsSummaryJson = Resumen(efectos),
            ActorName = Recortar(nombreDelActor, 150),
            CreatedAt = ahora,
            CreatedBy = Recortar(nombreDelActor, 150),
        };
        // Las copias cuelgan de la réplica que las trajo (ReplicationId lo resuelve el guardado).
        replicacion.Values.AddRange(copiasDeValores);
        replicacion.Documents.AddRange(copiasDeDocumentos);
        _db.NationalValueReplications.Add(replicacion);

        var evento = new AuditLogCommand
        {
            Action = EventoReplicada,
            EntityType = nameof(NationalValueReplication),
            EntityId = replicacion.PublicId.ToString(),
            Module = ModuloDeAuditoria,
            NewValues = new
            {
                origin = origen.ToString(),
                fromSequence = replicacion.FromSequence,
                toSequence = replicacion.ToSequence,
                values = replicacion.ValueCount,
                documents = replicacion.DocumentCount,
                effects = efectos.ToDictionary(e => e.Key, e => e.Value.Conteos),
            },
            HttpStatusCode = 200,
        };
        var (contexto, flujo) = await ContextoDeAuditoriaAsync(ct);
        if (contexto is not null && flujo is not null)
            _db.AuditOutbox.Add(AuditoriaEncadenada.Entrada(flujo, AuditoriaEncadenada.Evento(contexto, evento)));

        await _db.SaveChangesAsync(ct);

        if (contexto is not null && flujo is null && _servicios?.GetService<IAuditService>() is { } mongo)
        {
            try
            {
                await mongo.LogAsync(AuditoriaEncadenada.ParaMongo(contexto, evento), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "[ValoresNacionales.AuditoriaFallida] No se registró el evento {Evento}.", EventoReplicada);
            }
        }
        _logger.LogInformation("[ValoresNacionales.Replicados] {Valores} valores y {Documentos} documentos (Sequence {Desde}–{Hasta}) por {Origen}.",
            replicacion.ValueCount, replicacion.DocumentCount, replicacion.FromSequence, replicacion.ToSequence, origen);

        await LevantarAlertasAsync(efectos.Values.SelectMany(e => e.Alertas), ct);

        return new ResultadoDeReplicacion(true, null, replicacion.ValueCount, replicacion.DocumentCount,
            replicacion.FromSequence, replicacion.ToSequence, replicacion.PublicId, efectos);
    }

    private async Task<(ContextoDeAuditoria? Contexto, string? Flujo)> ContextoDeAuditoriaAsync(CancellationToken ct)
    {
        if (_servicios is null) return (null, null);
        var contexto = await AuditoriaEncadenada.ContextoAsync(_servicios, _servicios.GetService<ICurrentUserService>(), null, ct);
        var flujo = AuditoriaEncadenada.EsEncadenado(ModuloDeAuditoria) ? AuditoriaEncadenada.Flujo(contexto.TenantId) : null;
        return (contexto, flujo);
    }

    private async Task LevantarAlertasAsync(IEnumerable<AlertaALevantar> alertas, CancellationToken ct)
    {
        var lista = alertas.ToList();
        if (lista.Count == 0) return;
        if (_servicios?.GetService<IAlertas>() is not { } servicio)
        {
            foreach (var a in lista)
                _logger.LogWarning("[ValoresNacionales.AlertaSinServicio] {Tipo}: {Asunto}", a.TypeCode, a.Subject);
            return;
        }
        foreach (var alerta in lista)
        {
            try
            {
                var r = await servicio.LevantarAsync(alerta, ct);
                if (r.IsFailure)
                    _logger.LogWarning("[ValoresNacionales.AlertaNoLevantada] {Tipo}: {Codigo} {Mensaje}", alerta.TypeCode, r.Error.Code, r.Error.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "[ValoresNacionales.AlertaNoLevantada] {Tipo}", alerta.TypeCode);
            }
        }
    }

    /// <summary>El resumen de los efectos (conteos y notas), recortado a 2.000: sin notas si no caben.</summary>
    internal static string? Resumen(IReadOnlyDictionary<string, EfectoAplicado> efectos)
    {
        if (efectos.Count == 0) return null;
        var completo = JsonSerializer.Serialize(efectos.ToDictionary(e => e.Key, e => new
        {
            counts = e.Value.Conteos,
            notes = e.Value.Notas,
            alerts = e.Value.Alertas.Select(a => a.TypeCode).ToList(),
        }), Json);
        if (completo.Length <= LargoDelResumen) return completo;
        var corto = JsonSerializer.Serialize(efectos.ToDictionary(e => e.Key, e => new
        {
            counts = e.Value.Conteos,
            alerts = e.Value.Alertas.Count,
        }), Json);
        return Recortar(corto, LargoDelResumen);
    }

    private static string Recortar(string texto, int largo) => texto.Length > largo ? texto[..largo] : texto;
}

// ------------------------------------------------------------------------------------------- comando sin ruta --

/// <summary>
/// Una pasada de la réplica en la cooperativa activa (feature 013, T39): sin ruta HTTP; la envían la tarea
/// <c>valores-nacionales.replicar</c> y, desde sus fases, el ciclo diario y el cierre de Cartera. Devuelve el resultado aun
/// si la central no respondió (con su motivo): no es un error del comando. (nuevo)
/// </summary>
public sealed record ReplicateNationalValuesCommand(NationalValueReplicationOrigin Origin = NationalValueReplicationOrigin.ScheduledTask)
    : IRequest<Result<ResultadoDeReplicacion>>;

public sealed class ReplicateNationalValuesCommandHandler(ReplicacionDeValoresNacionales replicacion, IActorActual actor)
    : IRequestHandler<ReplicateNationalValuesCommand, Result<ResultadoDeReplicacion>>
{
    public async Task<Result<ResultadoDeReplicacion>> Handle(ReplicateNationalValuesCommand request, CancellationToken ct)
    {
        var quien = await actor.ObtenerAsync(ct);
        return Result.Success(await replicacion.AplicarAsync(request.Origin, quien.Name, ct));
    }
}
