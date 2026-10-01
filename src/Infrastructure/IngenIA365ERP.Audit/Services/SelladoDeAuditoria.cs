using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Audit.Configuration;
using IngenIA365ERP.Audit.Integrity;
using IngenIA365ERP.Audit.Models;
using IngenIA365ERP.Domain.Entities.Audit;
using IngenIA365ERP.Domain.Enums.Audit;
using IngenIA365ERP.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace IngenIA365ERP.Audit.Services;

/// <summary>
/// El trabajo de <see cref="AuditOutboxForwarder"/> dentro de <b>una</b> cooperativa (feature 012, T37, T38;
/// T065): sellar, reenviar y anclar. Corre en el ámbito que arma <c>IEjecutorEnCooperativa</c>, con el
/// arrendamiento <c>audit.forward</c> ya tomado. Separado del servicio de fondo para que éste sólo recorra
/// cooperativas y tiempos.
///
/// <list type="number">
/// <item><b>Sellar</b>: las filas de <c>COR_AuditOutbox</c> sin <c>Seq</c>, en orden de <c>Id</c> (nunca desde un
/// «último Id»: una transacción puede confirmar tarde y entra en la siguiente pasada), reciben <c>Seq</c>,
/// <c>PrevHash</c> y <c>Hash</c> de <see cref="SelloDeIntegridad"/>, y la cabeza de su flujo avanza, todo en un
/// <c>SaveChanges</c>. La cabeza lleva <c>RowVersion</c>: si otra réplica selló a la vez, choca, se descarta y
/// se relee; la cadena no se bifurca. El primer sellado de un flujo crea la cabeza y el ancla génesis.</item>
/// <item><b>Reenviar</b>: las selladas y no reenviadas, en orden de <c>Seq</c>, se insertan en la base Mongo de la
/// cooperativa con <c>_id = EventId</c> y el bloque <c>chain</c>; una clave duplicada es que ya estaba. Después
/// <c>UPDATE</c>: <c>Forwarded</c>, <c>ForwardedAt</c> y <c>PayloadJson = NULL</c> (queda la fila delgada; nunca
/// DELETE, Principio VII). Un fallo suma <c>ForwardAttempts</c> y deja <c>LastForwardError</c>.</item>
/// <item><b>Anclar</b>: cada 1.000 eventos al sellar, y una vez al día sobre la cabeza si avanzó.</item>
/// </list>
///
/// <para>
/// Sellar antes de reenviar es a propósito: si se reenviara primero y el sellado chocara con otra réplica,
/// Mongo tendría un evento con una posición que la cadena no le dio.
/// </para>
/// </summary>
public sealed class SelladoDeAuditoria(
    IMongoClient mongo,
    IOptions<MongoDbSettings> opciones,
    SelloDeIntegridad sello,
    ILogger<SelladoDeAuditoria> logger)
{
    /// <summary>Filas por tanda de sellado y de reenvío.</summary>
    public const int Tanda = 200;

    /// <summary>Tandas por pasada, renovando el arrendamiento entre una y otra.</summary>
    public const int TandasPorPasada = 20;

    /// <summary>Un ancla <see cref="AuditAnchorKind.EveryN"/> cada tantos eventos.</summary>
    public const long AnclaCadaEventos = 1000;

    private const int IntentosDeSellado = 3;

    /// <summary>Una pasada sobre la cooperativa del ámbito. Devuelve cuántos eventos llevó a Mongo.</summary>
    public async Task<int> ReenviarAsync(IServiceProvider servicios, Guid tenantPublicId, CancellationToken ct)
    {
        var db = servicios.GetRequiredService<IApplicationDbContext>();
        var reloj = servicios.GetRequiredService<IDateTimeService>();
        var arrendamientos = servicios.GetService<IArrendamientos>();
        var coleccion = mongo.GetDatabase(AuditDatabaseNames.Para(opciones.Value.DatabaseName, tenantPublicId.ToString("N")))
            .GetCollection<BsonDocument>(AuditDatabaseNames.Coleccion);

        var enviados = 0;
        for (var tanda = 0; tanda < TandasPorPasada; tanda++)
        {
            if (tanda > 0 && arrendamientos is not null
                && !await arrendamientos.RenovarAsync(NombresDeArrendamiento.ReenvioDeAuditoria, null, ct))
                break; // otra réplica tomó el arrendamiento: ella sigue.

            var sellados = await SellarAsync(db, reloj, ct);
            var enviadosEnTanda = await EnviarAsync(db, coleccion, reloj, ct);
            enviados += enviadosEnTanda;
            if (sellados == 0 && enviadosEnTanda == 0) break;
        }

        await AnclarElDiaAsync(db, reloj, ct);
        return enviados;
    }

    // -------------------------------------------------------------- sellar --

    private async Task<int> SellarAsync(IApplicationDbContext db, IDateTimeService reloj, CancellationToken ct)
    {
        for (var intento = 1; intento <= IntentosDeSellado; intento++)
        {
            db.DescartarCambios();
            var pendientes = await db.AuditOutbox
                .Where(e => !e.Forwarded && e.Seq == null)
                .OrderBy(e => e.Id)
                .Take(Tanda)
                .ToListAsync(ct);
            if (pendientes.Count == 0) return 0;

            var ahora = AuditoriaEncadenada.AMilisegundos(reloj.UtcNow);
            var sellados = 0;
            foreach (var flujo in pendientes.GroupBy(e => e.Stream))
            {
                var cabeza = await db.AuditChainHeads.FirstOrDefaultAsync(h => h.Stream == flujo.Key, ct);
                if (cabeza is null)
                {
                    cabeza = new AuditChainHead { Stream = flujo.Key, LastSeq = 0, LastHash = SelloDeIntegridad.HashInicial };
                    db.AuditChainHeads.Add(cabeza);
                    Anclar(db, flujo.Key, AuditAnchorKind.Genesis, 0, SelloDeIntegridad.HashInicial, null, ahora);
                }

                foreach (var entrada in flujo)
                {
                    BsonDocument documento;
                    try
                    {
                        documento = Documento(entrada, cabeza.LastSeq + 1, cabeza.LastHash);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Una carga ilegible no se sella (no se puede verificar) ni detiene a las demás.
                        entrada.ForwardAttempts++;
                        entrada.LastForwardError = Recortar($"Carga ilegible: {ex.Message}");
                        logger.LogError(ex, "[Auditoria.CargaIlegible] El evento {EventId} de {Flujo} no se pudo leer; no se sella.",
                            entrada.EventId, entrada.Stream);
                        continue;
                    }

                    var seq = cabeza.LastSeq + 1;
                    var hash = SelloDeIntegridad.Hash(cabeza.LastHash, SelloDeIntegridad.Canonico(documento));
                    entrada.Seq = seq;
                    entrada.PrevHash = cabeza.LastHash;
                    entrada.Hash = hash;
                    cabeza.LastSeq = seq;
                    cabeza.LastHash = hash;
                    sellados++;

                    if (seq % AnclaCadaEventos == 0)
                        Anclar(db, flujo.Key, AuditAnchorKind.EveryN, seq, hash, null, ahora);
                }
            }

            try
            {
                await db.SaveChangesAsync(ct);
                return sellados;
            }
            catch (Exception ex) when (ex is ConcurrencyConflictException or DbUpdateException)
            {
                // Otra réplica selló a la vez (la cabeza chocó por RowVersion, o el único de Seq o del ancla).
                logger.LogWarning(ex, "[Auditoria.SelladoConcurrente] El sellado chocó con otro (intento {Intento}); se relee la cabeza.", intento);
            }
        }

        db.DescartarCambios();
        return 0;
    }

    // ------------------------------------------------------------- reenviar --

    private async Task<int> EnviarAsync(IApplicationDbContext db, IMongoCollection<BsonDocument> coleccion, IDateTimeService reloj, CancellationToken ct)
    {
        db.DescartarCambios();
        var sellados = await db.AuditOutbox
            .Where(e => !e.Forwarded && e.Seq != null)
            .OrderBy(e => e.Seq)
            .Take(Tanda)
            .ToListAsync(ct);
        if (sellados.Count == 0) return 0;

        var enviados = 0;
        foreach (var entrada in sellados)
        {
            try
            {
                var documento = Documento(entrada, entrada.Seq!.Value, entrada.PrevHash!);
                documento[SelloDeIntegridad.Cadena.Campo].AsBsonDocument[SelloDeIntegridad.Cadena.Hash] = entrada.Hash;
                try
                {
                    await coleccion.InsertOneAsync(documento, cancellationToken: ct);
                }
                catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
                {
                    // Ya estaba: una pasada anterior lo insertó y no alcanzó a marcarlo.
                }

                entrada.Forwarded = true;
                entrada.ForwardedAt = reloj.UtcNow;
                entrada.PayloadJson = null;
                entrada.LastForwardError = null;
                enviados++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                entrada.ForwardAttempts++;
                entrada.LastForwardError = Recortar(ex.Message);
                logger.LogError(ex, "[Auditoria.ReenvioFallido] El evento {EventId} (seq {Seq}) no llegó a Mongo; se reintenta en la próxima pasada.",
                    entrada.EventId, entrada.Seq);
                break; // en orden de Seq: no se salta uno para mandar el siguiente.
            }
        }

        await db.SaveChangesAsync(ct);
        return enviados;
    }

    // ---------------------------------------------------------------- anclas --

    private async Task AnclarElDiaAsync(IApplicationDbContext db, IDateTimeService reloj, CancellationToken ct)
    {
        db.DescartarCambios();
        var hoy = reloj.HoyLocal;
        var ahora = AuditoriaEncadenada.AMilisegundos(reloj.UtcNow);
        var cabezas = await db.AuditChainHeads.AsNoTracking().Where(h => h.LastSeq > 0).ToListAsync(ct);
        foreach (var cabeza in cabezas)
        {
            var yaEsta = await db.AuditAnchors.AnyAsync(a => a.Stream == cabeza.Stream && a.Kind == AuditAnchorKind.Daily
                && (a.AnchorDate == hoy || a.Seq == cabeza.LastSeq), ct);
            if (!yaEsta) Anclar(db, cabeza.Stream, AuditAnchorKind.Daily, cabeza.LastSeq, cabeza.LastHash, hoy, ahora);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            db.DescartarCambios();
            logger.LogInformation(ex, "[Auditoria.AnclaDiariaConcurrente] Otra réplica ancló el día primero.");
        }
    }

    private void Anclar(IApplicationDbContext db, string flujo, AuditAnchorKind tipo, long seq, string hash, DateOnly? dia, DateTime ahora)
    {
        try
        {
            var (hmac, version) = sello.FirmarAncla(flujo, seq, hash, ahora);
            db.AuditAnchors.Add(new AuditAnchor
            {
                Stream = flujo, Kind = tipo, Seq = seq, Hash = hash, AnchorDate = dia, Hmac = hmac, KeyVersion = version, AnchoredAt = ahora,
            });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "[Auditoria.AnclaSinClave] No se ancló {Tipo} en {Flujo} (seq {Seq}): falta la clave de anclaje.", tipo, flujo, seq);
        }
    }

    // ------------------------------------------------------------- documento --

    /// <summary>
    /// El documento de Mongo de una fila, con su bloque <c>chain</c> sin <c>hash</c>: exactamente lo que se
    /// canonicaliza al sellar y lo que la verificación reconstruye al leerlo de vuelta.
    /// </summary>
    public static BsonDocument Documento(AuditOutboxEntry entrada, long seq, string prevHash)
    {
        if (string.IsNullOrWhiteSpace(entrada.PayloadJson))
            throw new InvalidOperationException("La fila ya no tiene carga (se reenvió o se vació).");

        var evento = AuditoriaEncadenada.LeerCarga(entrada.PayloadJson);
        // La fecha de la fila (a milisegundos, UTC): la base la devuelve sin Kind, y Mongo tomaría eso por hora local.
        var documento = AuditDocumentSchema.ToDocument(evento with { OccurredAt = DateTime.SpecifyKind(entrada.OccurredAt, DateTimeKind.Utc) });
        documento.InsertAt(0, new BsonElement("_id", entrada.EventId.ToString("D")));
        documento[SelloDeIntegridad.Cadena.Campo] = new BsonDocument
        {
            { SelloDeIntegridad.Cadena.Stream, entrada.Stream },
            { SelloDeIntegridad.Cadena.Seq, seq },
            { SelloDeIntegridad.Cadena.PrevHash, prevHash },
            { SelloDeIntegridad.Cadena.Alg, SelloDeIntegridad.Algoritmo },
            { SelloDeIntegridad.Cadena.V, SelloDeIntegridad.Version },
        };
        return documento;
    }

    private static string Recortar(string texto) => texto.Length > 500 ? texto[..500] : texto;
}
