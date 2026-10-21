using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Paging;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Common.Integration;

/// <summary>
/// Implementación de <see cref="IMensajesEntrantes"/> sobre <c>COR_Integration*</c> (feature 012, T497). Sólo lee: la
/// entrega la escribe <c>RegisterDeliveryResultCommand</c>. (nuevo)
/// </summary>
public sealed class MensajesEntrantes(IApplicationDbContext db) : IMensajesEntrantes
{
    public async Task<Result<IReadOnlyList<MensajeEntrante>>> LeerAsync(
        IReadOnlyCollection<Guid> messagePublicIds, string destino, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(messagePublicIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(destino);
        var ids = messagePublicIds.Distinct().ToList();

        var filas = await (
                from m in db.IntegrationMessages.AsNoTracking()
                join d in db.IntegrationMessageDeliveries.AsNoTracking() on m.Id equals d.MessageId
                where ids.Contains(m.PublicId) && d.Destination == destino
                orderby m.Id
                select new { Mensaje = m, Entrega = d })
            .ToListAsync(ct);

        var faltantes = ids.Where(id => filas.All(f => f.Mensaje.PublicId != id)).ToList();
        if (faltantes.Count > 0)
            return Result.Failure<IReadOnlyList<MensajeEntrante>>(ErroresDeIntegracion.MensajeNoEncontrado(faltantes[0]));

        return await ArmarAsync(filas.Select(f => (f.Mensaje, f.Entrega)).ToList(), ct);
    }

    /// <summary>
    /// Lo que espera en el destino sin consumir nada (feature 013, T19): sólo lee; no crea intentos ni cambia entregas. Los
    /// estados son los que bloquean (<see cref="EntregasElegibles.Bloqueantes"/>): lo que el destino todavía no procesó.
    /// </summary>
    public async Task<Result<PagedResult<MensajeEntrante>>> PendientesAsync(
        string destino, IReadOnlyCollection<string> tipos, DateOnly hastaFechaDeOperacion, PageRequest pagina, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destino);
        ArgumentNullException.ThrowIfNull(tipos);
        ArgumentNullException.ThrowIfNull(pagina);
        var listaDeTipos = tipos.Distinct(StringComparer.Ordinal).ToList();
        var pendientes = EntregasElegibles.Bloqueantes.ToArray();

        var consulta =
            from m in db.IntegrationMessages.AsNoTracking()
            join d in db.IntegrationMessageDeliveries.AsNoTracking() on m.Id equals d.MessageId
            where d.Destination == destino && pendientes.Contains(d.Status)
                  && (listaDeTipos.Count == 0 || listaDeTipos.Contains(m.Type))
                  && m.OperationDate <= hastaFechaDeOperacion
            select new { Mensaje = m, Entrega = d };

        var total = await consulta.CountAsync(ct);
        var filas = await consulta.OrderBy(f => f.Mensaje.Id)
            .Skip((pagina.SafePage - 1) * pagina.SafePageSize).Take(pagina.SafePageSize)
            .ToListAsync(ct);

        var armados = await ArmarAsync(filas.Select(f => (f.Mensaje, f.Entrega)).ToList(), ct);
        return armados.IsFailure
            ? Result.Failure<PagedResult<MensajeEntrante>>(armados.Error)
            : Result.Success(new PagedResult<MensajeEntrante>(armados.Value, pagina.SafePage, pagina.SafePageSize, total));
    }

    /// <summary>Verifica la huella de cada mensaje y arma lo que ve el consumidor, en el orden recibido.</summary>
    private async Task<Result<IReadOnlyList<MensajeEntrante>>> ArmarAsync(
        IReadOnlyList<(IntegrationMessage Mensaje, IntegrationMessageDelivery Entrega)> filas, CancellationToken ct)
    {
        var lotes = await LotesAsync(db, filas.Select(f => f.Entrega.BatchId), ct);

        var resultado = new List<MensajeEntrante>(filas.Count);
        foreach (var (mensaje, entrega) in filas)
        {
            if (!HuellaCoincide(mensaje))
                return Result.Failure<IReadOnlyList<MensajeEntrante>>(ErroresDeIntegracion.ContenidoAlterado(mensaje.PublicId));

            var contenido = Deserializar(mensaje);
            resultado.Add(new MensajeEntrante(
                Sobre(mensaje, contenido),
                contenido,
                mensaje.PayloadJson,
                mensaje.PayloadSha256,
                mensaje.PrevalidationOutcome,
                Entrega(entrega, lotes))
            {
                OriginDocumentClassText = mensaje.OriginDocumentClass,
                RelatedDocumentClassText = mensaje.RelatedDocumentClass,
            });
        }

        return Result.Success<IReadOnlyList<MensajeEntrante>>(resultado);
    }

    /// <summary>¿El SHA-256 de los bytes UTF-8 guardados es el que se selló al emitir? Nunca se reserializa.</summary>
    public static bool HuellaCoincide(IntegrationMessage mensaje) =>
        string.Equals(
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(mensaje.PayloadJson))),
            mensaje.PayloadSha256,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>El contenido con su record del catálogo (tipo y versión), o el <c>JsonElement</c> si no está.</summary>
    public static object? Deserializar(IntegrationMessage mensaje)
    {
        var tipo = CatalogoDeMensajes.Buscar(mensaje.Type, mensaje.Version);
        return tipo is null
            ? JsonSerializer.Deserialize<JsonElement>(mensaje.PayloadJson, OpcionesDeMensajes.Opciones)
            : JsonSerializer.Deserialize(mensaje.PayloadJson, tipo.Record, OpcionesDeMensajes.Opciones);
    }

    /// <summary>El sobre del contrato armado desde las columnas del mensaje.</summary>
    public static IntegrationEnvelopeV1 Sobre(IntegrationMessage m, object? contenido) => new()
    {
        MessageId = m.PublicId,
        Type = m.Type,
        Version = m.Version,
        Kind = m.Kind,
        OriginModule = m.OriginModule,
        OriginEventKey = m.OriginEventKey,
        Origin = new MessageOriginV1
        {
            Kind = m.OriginKind,
            DocumentClass = Clase(m.OriginDocumentClass),
            DocumentTypeCode = m.OriginDocumentTypeCode,
            Number = m.OriginNumber ?? string.Empty,
            PublicId = m.OriginPublicId,
            OperationDate = m.OperationDate,
            FiscalUniqueCode = m.FiscalUniqueCode,
        },
        Related = m.RelatedPublicId is { } relacionado
            ? new DocumentRefV1 { PublicId = relacionado, DocumentClass = Clase(m.RelatedDocumentClass) ?? default, Number = m.RelatedNumber ?? string.Empty }
            : null,
        ChainRootPublicId = m.ChainRootPublicId,
        BranchPublicId = m.BranchPublicId,
        CostCenterPublicId = m.CostCenterPublicId,
        WarehouseCode = m.WarehouseCode,
        PersonPublicId = m.PersonPublicId,
        Currency = m.Currency,
        ExchangeRate = m.ExchangeRate,
        OriginUser = new UserRefV1 { CentralUserId = m.OriginUserCentralId, Name = m.OriginUserName },
        EmittedAt = new DateTimeOffset(DateTime.SpecifyKind(m.EmittedAt, DateTimeKind.Utc)),
        Payload = contenido,
    };

    internal static async Task<Dictionary<int, Guid>> LotesAsync(IApplicationDbContext db, IEnumerable<int?> ids, CancellationToken ct)
    {
        var lista = ids.Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        if (lista.Count == 0) return [];
        return await db.IntegrationBatches.AsNoTracking().Where(b => lista.Contains(b.Id)).ToDictionaryAsync(b => b.Id, b => b.PublicId, ct);
    }

    private static EntregaEntrante Entrega(IntegrationMessageDelivery d, IReadOnlyDictionary<int, Guid> lotes) => new(
        d.Destination, d.Mode, d.Status, d.ScheduleKey, d.BatchScopeKey,
        d.BatchId is int lote && lotes.TryGetValue(lote, out var publico) ? publico : null,
        d.Attempts, d.NextAttemptAt);

    private static DocumentClass? Clase(string? texto) =>
        Enum.TryParse<DocumentClass>(texto, ignoreCase: false, out var clase) ? clase : null;
}
