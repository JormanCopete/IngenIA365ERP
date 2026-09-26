using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
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

        var lotes = await LotesAsync(db, filas.Select(f => f.Entrega.BatchId), ct);

        var resultado = new List<MensajeEntrante>(filas.Count);
        foreach (var fila in filas)
        {
            if (!HuellaCoincide(fila.Mensaje))
                return Result.Failure<IReadOnlyList<MensajeEntrante>>(ErroresDeIntegracion.ContenidoAlterado(fila.Mensaje.PublicId));

            var contenido = Deserializar(fila.Mensaje);
            resultado.Add(new MensajeEntrante(
                Sobre(fila.Mensaje, contenido),
                contenido,
                fila.Mensaje.PayloadJson,
                fila.Mensaje.PayloadSha256,
                fila.Mensaje.PrevalidationOutcome,
                Entrega(fila.Entrega, lotes)));
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
        var tipo = CatalogoDeMensajesV1.Todos.FirstOrDefault(t => t.Type == mensaje.Type && t.Version == mensaje.Version);
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
