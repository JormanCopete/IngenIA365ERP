using System.Text.Json;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Domain.Entities.Audit;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.Extensions.DependencyInjection;

namespace IngenIA365ERP.Application.Common.Audit;

/// <summary>
/// La auditoría con entrega garantizada de los <b>módulos encadenados</b> (feature 012, T37, T38; T062).
///
/// <para>
/// Para estos módulos el evento no va directo a Mongo (donde una caída del proceso lo perdía): se escribe
/// en <c>COR_AuditOutbox</c> <b>en la misma transacción</b> que el cambio, y <c>AuditOutboxForwarder</c> lo
/// sella en la cadena de la cooperativa y lo lleva a Mongo. Quién escribe cada fila:
/// </para>
/// <list type="bullet">
/// <item>las diferencias de entidad, <c>AuditableEntityInterceptor</c> en <c>SavingChanges</c> (y para
/// estos módulos deja de mandarlas a Mongo: no hay doble registro);</item>
/// <item>el evento del comando, <c>AuditBehavior</c> dentro de la transacción en curso;</item>
/// <item>un rechazo, un contexto aparte (<see cref="ITenantDbContextFactory.AbrirLaDelAmbito"/>) que
/// sobrevive al rollback;</item>
/// <item>ingresar a una opción, exportar e imprimir, <see cref="RegistrarAsync"/>.</item>
/// </list>
///
/// <para>
/// Un flujo por cooperativa y clase de retención: <c>{tenantPublicId:N}:10y</c>. <c>Accounting</c> queda
/// fuera en esta feature (pregunta C1); los módulos no encadenados siguen por <c>MongoAuditService</c>.
/// </para>
/// </summary>
public static class AuditoriaEncadenada
{
    /// <summary>Los módulos cuya auditoría va por <c>COR_AuditOutbox</c> y la cadena de sellos (T38).</summary>
    public static IReadOnlyList<string> Modulos { get; } =
    [
        ModuloDeAuditoria.Inventory,
        ModuloDeAuditoria.ElectronicInvoicing,
        ModuloDeAuditoria.Integration,
        ModuloDeAuditoria.Approvals,
        ModuloDeAuditoria.Alerts,
        ModuloDeAuditoria.Parameters,
        ModuloDeAuditoria.Taxes,
        ModuloDeAuditoria.PaymentMeans,
        ModuloDeAuditoria.Navigation,
    ];

    /// <summary>Sufijo de la clase de retención de diez años.</summary>
    public const string SufijoDelFlujo = ":10y";

    /// <summary>Cuánto se conserva un evento del flujo de diez años (el TTL de <c>AuditRetention</c>).</summary>
    public static readonly TimeSpan Retencion = TimeSpan.FromDays(365 * 10);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static bool EsEncadenado(string? modulo) =>
        modulo is not null && Modulos.Contains(modulo, StringComparer.Ordinal);

    /// <summary>El flujo de diez años de la cooperativa: <c>{tenantPublicId:N}:10y</c>.</summary>
    public static string Flujo(Guid tenantPublicId) => $"{tenantPublicId:N}{SufijoDelFlujo}";

    /// <summary>El flujo a partir del <c>ICurrentTenantService.TenantId</c>; nulo si no hay cooperativa.</summary>
    public static string? Flujo(string? tenantId) =>
        Guid.TryParse(tenantId, out var cooperativa) && cooperativa != Guid.Empty ? Flujo(cooperativa) : null;

    /// <summary>
    /// UTC a milisegundos. Mongo guarda las fechas con esa precisión: con los ticks completos, el hash
    /// calculado al sellar no se podría volver a calcular desde el documento guardado.
    /// </summary>
    public static DateTime AMilisegundos(DateTime instante)
    {
        var utc = instante.Kind switch
        {
            DateTimeKind.Local => instante.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(instante, DateTimeKind.Utc),
            _ => instante,
        };
        return new DateTime(utc.Ticks - utc.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
    }

    /// <summary>El texto que la auditoría usa para cada canal (T36): <c>web</c>, <c>app</c>, <c>pos</c>, <c>proceso</c>.</summary>
    public static string Canal(ExecutionChannel canal) => canal switch
    {
        ExecutionChannel.App => "app",
        ExecutionChannel.Pos => "pos",
        ExecutionChannel.Process => "proceso",
        _ => "web",
    };

    // --------------------------------------------------------------- filas --

    /// <summary>La fila de <c>COR_AuditOutbox</c> de un evento. No la agrega a ningún contexto.</summary>
    public static AuditOutboxEntry Entrada(string flujo, AuditEventDocument evento)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flujo);
        var ocurrido = AMilisegundos(evento.OccurredAt);
        var normalizado = evento with { OccurredAt = ocurrido };
        return new AuditOutboxEntry
        {
            EventId = Guid.NewGuid(),
            Stream = flujo,
            Module = Recortar(evento.Module ?? string.Empty, 30),
            OccurredAt = ocurrido,
            PayloadJson = JsonSerializer.Serialize(normalizado, Json),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = Recortar(evento.UserName ?? "system", 100),
        };
    }

    /// <summary>El evento guardado en <see cref="AuditOutboxEntry.PayloadJson"/>.</summary>
    public static AuditEventDocument LeerCarga(string payloadJson) =>
        JsonSerializer.Deserialize<AuditEventDocument>(payloadJson, Json)
        ?? throw new InvalidOperationException("La carga del evento de auditoría está vacía.");

    /// <summary>Un valor cualquiera como JSON para <c>oldValuesJson</c>/<c>newValuesJson</c>; nunca lanza.</summary>
    public static string? ComoJson(object? valor)
    {
        if (valor is null) return null;
        if (valor is string texto) return texto;
        try
        {
            return JsonSerializer.Serialize(valor, valor.GetType(), Json);
        }
        catch (Exception ex) when (ex is NotSupportedException or InvalidOperationException or JsonException)
        {
            return JsonSerializer.Serialize(new Dictionary<string, string?> { ["_raw"] = valor.ToString() }, Json);
        }
    }

    // ------------------------------------------------------------ contexto --

    /// <summary>
    /// Quién, desde dónde y por qué (T36): el actor de <see cref="IActorActual"/> (persona o proceso), el
    /// origen de <see cref="IOrigenDeLaPeticion"/> (canal <c>pos</c> si la operación implementa
    /// <see cref="IOperacionDePuntoDeVenta"/>), el motivo de <see cref="IConMotivo"/> y la clave de
    /// <see cref="IOperacionIdempotente"/>. Nunca lanza: lo que no se puede resolver queda fuera.
    /// </summary>
    /// <param name="servicios">Los del ámbito; nulo en las pruebas que sólo tienen <paramref name="usuario"/>.</param>
    /// <param name="usuario">El usuario del token, para <c>userId</c>/<c>userName</c> (como <c>MongoAuditService</c>).</param>
    /// <param name="operacion">El comando, si lo hay.</param>
    public static async Task<ContextoDeAuditoria> ContextoAsync(
        IServiceProvider? servicios, ICurrentUserService? usuario, object? operacion, CancellationToken ct)
    {
        var cooperativa = servicios?.GetService<ICurrentTenantService>()?.TenantId;
        var origen = servicios?.GetService<IOrigenDeLaPeticion>();

        Actor? actor = null;
        // El actor se pide sólo con cooperativa (o en segundo plano): resolverlo consulta SEC_Users, y los
        // comandos de identidad (login, segundo factor) corren sin base operativa.
        if (servicios is not null && (cooperativa is not null || ContextoAmbiental.Activo))
        {
            try
            {
                var actorActual = servicios.GetService<IActorActual>();
                if (actorActual is not null) actor = await actorActual.ObtenerAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                actor = null;
            }
        }

        var canal = operacion is IOperacionDePuntoDeVenta
            ? ExecutionChannel.Pos
            : origen?.Canal ?? actor?.Channel ?? (ContextoAmbiental.Activo ? ExecutionChannel.Process : ExecutionChannel.Web);
        var endpoint = origen?.Endpoint;
        var metodo = endpoint is not null && endpoint.IndexOf(' ') is > 0 and var i ? endpoint[..i] : null;
        var origenTexto = origen?.Origen ?? ContextoAmbiental.Origen ?? endpoint;

        var metadata = new Dictionary<string, string> { ["Channel"] = Canal(canal) };
        if (!string.IsNullOrWhiteSpace(origenTexto)) metadata["Origin"] = origenTexto;
        if (actor is not null)
        {
            metadata["ActorKind"] = actor.Kind.ToString();
            metadata["Actor"] = actor.Name;
            if (actor.UserPublicId is { } publico) metadata["ActorUserPublicId"] = publico.ToString();
        }
        if (operacion is IOperacionIdempotente { OperationKey: var clave } && clave != Guid.Empty)
            metadata["OperationKey"] = clave.ToString();
        if (operacion is IConMotivo { Reason: var motivo } && !string.IsNullOrWhiteSpace(motivo))
            metadata["Reason"] = motivo;

        return new ContextoDeAuditoria(
            cooperativa,
            actor,
            usuario?.UserId?.ToString() ?? "system",
            usuario?.UserName ?? actor?.Name ?? "system",
            origen?.Ip ?? actor?.Ip,
            origen?.UserAgent,
            endpoint,
            metodo,
            metadata);
    }

    /// <summary>El evento completo, listo para <see cref="Entrada"/>.</summary>
    public static AuditEventDocument Evento(ContextoDeAuditoria contexto, AuditLogCommand comando, IReadOnlyDictionary<string, string>? metadataExtra = null)
    {
        var metadata = new Dictionary<string, string>(contexto.Metadata);
        if (comando.Metadata is not null)
            foreach (var (clave, valor) in comando.Metadata) metadata[clave] = valor;
        if (metadataExtra is not null)
            foreach (var (clave, valor) in metadataExtra) metadata[clave] = valor;

        return new AuditEventDocument(
            TenantId: contexto.TenantId ?? string.Empty,
            UserId: contexto.UserId,
            UserName: contexto.UserName,
            Action: comando.Action,
            EntityType: comando.EntityType,
            EntityPublicId: comando.EntityId,
            Module: comando.Module,
            OldValuesJson: ComoJson(comando.OldValues),
            NewValuesJson: ComoJson(comando.NewValues),
            ChangedFields: comando.ChangedFields,
            IpAddress: comando.IpAddress ?? contexto.Ip,
            UserAgent: comando.UserAgent ?? contexto.UserAgent,
            Endpoint: comando.Endpoint ?? contexto.Endpoint,
            HttpMethod: comando.HttpMethod ?? contexto.HttpMethod,
            HttpStatusCode: comando.HttpStatusCode,
            DurationMs: comando.DurationMs,
            OccurredAt: AMilisegundos(DateTime.UtcNow))
        {
            Metadata = metadata,
        };
    }

    /// <summary>
    /// El mismo evento para <c>MongoAuditService</c>, con el origen y la metadata del contexto: lo usan los
    /// módulos no encadenados y el respaldo cuando no hay a dónde escribir en SQL.
    /// </summary>
    public static AuditLogCommand ParaMongo(ContextoDeAuditoria contexto, AuditLogCommand comando, IReadOnlyDictionary<string, string>? metadataExtra = null)
    {
        var metadata = new Dictionary<string, string>(contexto.Metadata);
        if (comando.Metadata is not null)
            foreach (var (clave, valor) in comando.Metadata) metadata[clave] = valor;
        if (metadataExtra is not null)
            foreach (var (clave, valor) in metadataExtra) metadata[clave] = valor;
        return comando with
        {
            IpAddress = comando.IpAddress ?? contexto.Ip,
            UserAgent = comando.UserAgent ?? contexto.UserAgent,
            Endpoint = comando.Endpoint ?? contexto.Endpoint,
            HttpMethod = comando.HttpMethod ?? contexto.HttpMethod,
            Metadata = metadata,
        };
    }

    // ---------------------------------------------- ingresar, exportar, imprimir --

    /// <summary>
    /// El método común de los eventos que no salen de un comando que cambie datos: ingresar a una opción
    /// (<c>Navigation</c>), exportar e imprimir (T37). Si el módulo es encadenado y hay cooperativa, inserta
    /// y guarda su propia fila de <c>COR_AuditOutbox</c>; si no, va por <see cref="IAuditService"/> como
    /// antes. Guarda sólo esa fila: se llama donde el contexto no tiene otros cambios pendientes.
    /// </summary>
    public static async Task RegistrarAsync(IServiceProvider servicios, AuditLogCommand evento, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        var contexto = await ContextoAsync(servicios, servicios.GetService<ICurrentUserService>(), null, ct);
        var flujo = EsEncadenado(evento.Module) ? Flujo(contexto.TenantId) : null;
        var db = flujo is null ? null : servicios.GetService<IApplicationDbContext>();

        if (db is null)
        {
            await servicios.GetRequiredService<IAuditService>().LogAsync(ParaMongo(contexto, evento), ct);
            return;
        }

        db.AuditOutbox.Add(Entrada(flujo!, Evento(contexto, evento)));
        await db.SaveChangesAsync(ct);
    }

    private static string Recortar(string texto, int largo) => texto.Length > largo ? texto[..largo] : texto;
}

/// <summary>
/// Quién hizo la operación, desde dónde y con qué datos de contexto (feature 012, T36). Lo arma
/// <see cref="AuditoriaEncadenada.ContextoAsync"/>.
/// </summary>
/// <param name="TenantId">PublicId de la cooperativa en formato <c>N</c>; nulo sin cooperativa.</param>
/// <param name="Actor">Persona o proceso; nulo si no se pudo resolver.</param>
/// <param name="UserId">El del token (<c>ICurrentUserService.UserId</c>) o <c>system</c>.</param>
/// <param name="UserName">El del token, o el nombre del actor.</param>
/// <param name="Ip">IP de la petición; nula en segundo plano.</param>
/// <param name="UserAgent">User-Agent de la petición.</param>
/// <param name="Endpoint"><c>MÉTODO /ruta</c>.</param>
/// <param name="HttpMethod">El método de <paramref name="Endpoint"/>.</param>
/// <param name="Metadata"><c>Channel</c>, <c>Origin</c>, <c>ActorKind</c>, <c>Actor</c>, <c>ActorUserPublicId</c>, <c>OperationKey</c>, <c>Reason</c>.</param>
public sealed record ContextoDeAuditoria(
    string? TenantId,
    Actor? Actor,
    string UserId,
    string UserName,
    string? Ip,
    string? UserAgent,
    string? Endpoint,
    string? HttpMethod,
    IReadOnlyDictionary<string, string> Metadata);
