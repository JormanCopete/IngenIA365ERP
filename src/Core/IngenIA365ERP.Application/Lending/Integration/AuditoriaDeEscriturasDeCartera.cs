using System.Globalization;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Lending.Integration;

/// <summary>
/// La auditoría de las escrituras de los puertos de Cartera (feature 013, US5, T0546; T68, enmienda A11; interfaces §3.13), molde de la auditoría de
/// rechazos de la 012 (T37). Cartera es un módulo encadenado: todo va a <c>COR_AuditOutbox</c> en el flujo de la cooperativa.
/// <list type="bullet">
/// <item><b>El intento</b> (<c>Lending.ObligationContract.Attempted</c>, <c>Lending.NumberReservation.Attempted</c>) se escribe por un contexto y una
/// conexión <b>aparte</b>, que sobrevive a la reversión de quien llama: el contexto lo crea la fábrica de la cooperativa resuelta,
/// <see cref="ITenantDbContextFactory.AbrirLaDelAmbito"/> —nunca una cadena de conexión armada a mano—, con <c>OperationKey</c> (cada clave del
/// lote), recaudador, método, cantidad de ítems, total, huella, usuario, oficina y dispositivo (los dos últimos los pone
/// <see cref="AuditoriaEncadenada.ContextoAsync"/>). Las lecturas del contrato (listar, simular) van por el mismo camino: no tienen nada que
/// guardar junto.</item>
/// <item><b>El hecho</b> (<c>…Applied</c>, <c>…Reversed</c>, <c>…CreditBalanceResolved</c>, <c>…Rejected</c>; <c>Lending.NumberReservation.Reserved</c>
/// / <c>.Released</c>) va en <c>COR_AuditOutbox</c> <b>dentro de la transacción</b>, sin guardar: lo guarda la unidad de trabajo del recaudador.
/// Un intento sin hecho pasado el tiempo de una transacción fue revertido con el recaudador (A11).</item>
/// </list>
/// Sin cooperativa activa va por <see cref="IAuditService"/>. Nunca lanza: un fallo queda en el log (Principio IX). (nuevo)
/// </summary>
public sealed class AuditoriaDeEscriturasDeCartera(IServiceProvider servicios, IApplicationDbContext db, ILogger<AuditoriaDeEscriturasDeCartera>? logger = null)
{
    private readonly ILogger _log = logger ?? (ILogger)NullLogger.Instance;

    /// <summary>El intento de una escritura del contrato, antes de validar (paso 1 de §3.3), por un contexto aparte.</summary>
    public Task<Guid?> IntentoAsync(string evento, string metodo, Recaudador recaudador, IReadOnlyList<string> claves, int items, decimal total, string huella,
        CancellationToken ct) =>
        AparteAsync(new AuditLogCommand
        {
            Action = evento,
            EntityType = "ObligationContract",
            EntityId = claves.FirstOrDefault(),
            Module = ModuloDeAuditoria.Lending,
            NewValues = new { method = metodo, collector = recaudador.Code, collectorClass = recaudador.Class.ToString(), keys = claves, items, total, sha256 = huella },
            Metadata = Metadatos(claves, recaudador, metodo, items, total, huella),
        }, ct);

    /// <summary>Una lectura del contrato (<c>Lending.ObligationContract.Listed</c> / <c>.Simulated</c>) con D, R y la huella, por un contexto aparte.</summary>
    public Task<Guid?> LecturaAsync(string evento, string entidad, Guid? entidadPublicId, Recaudador recaudador, object detalle, CancellationToken ct) =>
        AparteAsync(new AuditLogCommand
        {
            Action = evento,
            EntityType = entidad,
            EntityId = entidadPublicId?.ToString(),
            Module = ModuloDeAuditoria.Lending,
            NewValues = detalle,
            Metadata = new Dictionary<string, string> { ["Collector"] = recaudador.Code, ["CollectorClass"] = recaudador.Class.ToString() },
        }, ct);

    /// <summary>
    /// El hecho de un ítem (paso 13 de §3.3) en <c>COR_AuditOutbox</c> dentro de la unidad de trabajo, sin guardar: con la distribución completa por
    /// cuota y concepto o el código del rechazo.
    /// </summary>
    public async Task<Guid?> HechoAsync(string evento, string clave, Recaudador recaudador, string metodo, object detalle, CancellationToken ct)
    {
        try
        {
            var comando = new AuditLogCommand
            {
                Action = evento,
                EntityType = "ObligationContract",
                EntityId = clave,
                Module = ModuloDeAuditoria.Lending,
                NewValues = detalle,
                Metadata = Metadatos([clave], recaudador, metodo, null, null, null),
            };
            var contexto = await AuditoriaEncadenada.ContextoAsync(servicios, servicios.GetService<ICurrentUserService>(), null, ct, ModuloDeAuditoria.Lending);
            if (AuditoriaEncadenada.Flujo(contexto.TenantId) is not { } flujo)
            {
                await servicios.GetRequiredService<IAuditService>().LogAsync(AuditoriaEncadenada.ParaMongo(contexto, comando), ct);
                return null;
            }
            var entrada = AuditoriaEncadenada.Entrada(flujo, AuditoriaEncadenada.Evento(contexto, comando));
            db.AuditOutbox.Add(entrada);
            return entrada.EventId;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "[Cartera.EscrituraSinAuditar] El hecho {Evento} de la clave {Clave} no se pudo auditar.", evento, clave);
            return null;
        }
    }

    /// <summary>
    /// US6 (T0584): el intento de una escritura de un puerto que no es del contrato —la reserva y la liberación del número
    /// (<c>Lending.NumberReservation.Attempted</c>)— por un contexto aparte, antes de validar, con la clave y el detalle de la petición.
    /// </summary>
    public Task<Guid?> IntentoAsync(string evento, string entidad, string clave, object detalle, CancellationToken ct) =>
        AparteAsync(new AuditLogCommand
        {
            Action = evento,
            EntityType = entidad,
            EntityId = clave,
            Module = ModuloDeAuditoria.Lending,
            NewValues = detalle,
            Metadata = new Dictionary<string, string> { ["OperationKey"] = clave },
        }, ct);

    /// <summary>
    /// US6 (T0584): el hecho de una escritura de un puerto que no es del contrato (<c>Lending.NumberReservation.Reserved</c> / <c>.Released</c>) en
    /// <c>COR_AuditOutbox</c> dentro de la unidad de trabajo de quien llama, sin guardar.
    /// </summary>
    public async Task<Guid?> HechoAsync(string evento, string entidad, string clave, object detalle, CancellationToken ct)
    {
        try
        {
            var comando = new AuditLogCommand
            {
                Action = evento,
                EntityType = entidad,
                EntityId = clave,
                Module = ModuloDeAuditoria.Lending,
                NewValues = detalle,
                Metadata = new Dictionary<string, string> { ["OperationKey"] = clave },
            };
            var contexto = await AuditoriaEncadenada.ContextoAsync(servicios, servicios.GetService<ICurrentUserService>(), null, ct, ModuloDeAuditoria.Lending);
            if (AuditoriaEncadenada.Flujo(contexto.TenantId) is not { } flujo)
            {
                await servicios.GetRequiredService<IAuditService>().LogAsync(AuditoriaEncadenada.ParaMongo(contexto, comando), ct);
                return null;
            }
            var entrada = AuditoriaEncadenada.Entrada(flujo, AuditoriaEncadenada.Evento(contexto, comando));
            db.AuditOutbox.Add(entrada);
            return entrada.EventId;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "[Cartera.EscrituraSinAuditar] El hecho {Evento} de la clave {Clave} no se pudo auditar.", evento, clave);
            return null;
        }
    }

    private async Task<Guid?> AparteAsync(AuditLogCommand comando, CancellationToken ct)
    {
        try
        {
            var contexto = await AuditoriaEncadenada.ContextoAsync(servicios, servicios.GetService<ICurrentUserService>(), null, ct, ModuloDeAuditoria.Lending);
            if (AuditoriaEncadenada.Flujo(contexto.TenantId) is not { } flujo)
            {
                await servicios.GetRequiredService<IAuditService>().LogAsync(AuditoriaEncadenada.ParaMongo(contexto, comando), ct);
                return null;
            }
            var entrada = AuditoriaEncadenada.Entrada(flujo, AuditoriaEncadenada.Evento(contexto, comando));
            if (servicios.GetService<ITenantDbContextFactory>() is { } fabrica)
            {
                await using var propio = fabrica.AbrirLaDelAmbito();
                propio.Db.AuditOutbox.Add(entrada);
                await propio.Db.SaveChangesAsync(CancellationToken.None);
                return entrada.EventId;
            }
            // Sin fábrica (las pruebas sin contenedores): la misma unidad de trabajo, guardada ya.
            db.AuditOutbox.Add(entrada);
            await db.SaveChangesAsync(ct);
            return entrada.EventId;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogWarning(ex, "[Cartera.EscrituraSinAuditar] El evento {Evento} no se pudo auditar.", comando.Action);
            return null;
        }
    }

    private static Dictionary<string, string> Metadatos(IReadOnlyList<string> claves, Recaudador r, string metodo, int? items, decimal? total, string? huella)
    {
        var m = new Dictionary<string, string>
        {
            ["OperationKey"] = string.Join(",", claves),
            ["Collector"] = r.Code,
            ["CollectorClass"] = r.Class.ToString(),
            ["Method"] = metodo,
        };
        if (items is { } n) m["Items"] = n.ToString(CultureInfo.InvariantCulture);
        if (total is { } t) m["Total"] = t.ToString(CultureInfo.InvariantCulture);
        if (huella is not null) m["Sha256"] = huella;
        return m;
    }
}
