using IngenIA365ERP.Application.Audit.VerifyIntegrity;
using IngenIA365ERP.Audit.Configuration;
using IngenIA365ERP.Audit.Models;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;

namespace IngenIA365ERP.Audit.Integrity;

/// <summary>
/// <see cref="ILectorDeCadenaDeAuditoria"/> sobre la base Mongo de la cooperativa (feature 012, T38; T066).
/// Lee por <c>chain.stream</c> y <c>chain.seq</c> —no por fecha: un evento con la fecha cambiada no se escapa
/// del rango— y recalcula cada hash con <see cref="SelloDeIntegridad"/> a partir del documento tal como está.
/// Sólo lee: la credencial de la API no puede modificar la colección (rol de sólo inserción).
/// </summary>
public sealed class LectorDeCadenaDeAuditoria(
    IMongoClient mongo,
    IOptions<MongoDbSettings> opciones,
    SelloDeIntegridad sello) : ILectorDeCadenaDeAuditoria
{
    private static readonly string CampoStream = $"{SelloDeIntegridad.Cadena.Campo}.{SelloDeIntegridad.Cadena.Stream}";
    private static readonly string CampoSeq = $"{SelloDeIntegridad.Cadena.Campo}.{SelloDeIntegridad.Cadena.Seq}";

    public async Task<IReadOnlyList<EventoDeCadena>> LeerAsync(string tenantId, string stream, long desdeSeq, long hastaSeq, CancellationToken ct)
    {
        var coleccion = mongo.GetDatabase(AuditDatabaseNames.Para(opciones.Value.DatabaseName, tenantId))
            .GetCollection<BsonDocument>(AuditDatabaseNames.Coleccion);
        var f = Builders<BsonDocument>.Filter;
        var filtro = f.And(f.Eq(CampoStream, stream), f.Gte(CampoSeq, desdeSeq), f.Lte(CampoSeq, hastaSeq));

        var documentos = await coleccion.Find(filtro).Sort(Builders<BsonDocument>.Sort.Ascending(CampoSeq)).ToListAsync(ct);
        return [.. documentos.Select(Evento)];
    }

    public bool AnclaValida(string stream, long seq, string hash, DateTime anchoredAt, string hmac, string keyVersion) =>
        sello.AnclaValida(stream, seq, hash, anchoredAt, hmac, keyVersion);

    private static EventoDeCadena Evento(BsonDocument documento)
    {
        var cadena = documento[SelloDeIntegridad.Cadena.Campo].AsBsonDocument;
        var seq = cadena[SelloDeIntegridad.Cadena.Seq].ToInt64();
        var prevHash = Texto(cadena, SelloDeIntegridad.Cadena.PrevHash);
        var hash = Texto(cadena, SelloDeIntegridad.Cadena.Hash);
        var eventId = documento.TryGetValue("_id", out var id) ? id.ToString() ?? string.Empty : string.Empty;
        DateTime? ocurrido = documento.TryGetValue(AuditDocumentSchema.OccurredAt, out var fecha) && fecha.IsValidDateTime
            ? fecha.ToUniversalTime()
            : null;

        string recalculado;
        try
        {
            recalculado = prevHash is null ? string.Empty : SelloDeIntegridad.Hash(prevHash, SelloDeIntegridad.Canonico(documento));
        }
        catch (ArgumentException)
        {
            recalculado = string.Empty; // un tipo que la canonicalización no conoce: alguien metió algo que no se escribió así.
        }

        return new EventoDeCadena(seq, eventId, ocurrido, prevHash, hash, recalculado);
    }

    private static string? Texto(BsonDocument documento, string campo) =>
        documento.TryGetValue(campo, out var valor) && valor.IsString ? valor.AsString : null;
}
