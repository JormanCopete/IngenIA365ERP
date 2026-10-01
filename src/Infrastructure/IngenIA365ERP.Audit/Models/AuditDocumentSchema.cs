using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using MongoDB.Bson;
using MongoDB.Driver;

namespace IngenIA365ERP.Audit.Models;

/// <summary>
/// La forma de un documento de <c>audit_events</c>, en UN solo sitio.
///
/// <para>
/// Había dos escritores sobre la misma colección con dos formas distintas.
/// <c>AppendOnlyAuditWriter</c> (los eventos de identidad: ingresos, segundo
/// factor, invitaciones) escribía documentos crudos con nombres en camelCase
/// —<c>tenantId</c>, <c>occurredAt</c>, <c>entityPublicId</c>—, que son los que
/// llevan los cuatro índices y el TTL de cinco años (FR-023). Y
/// <c>MongoAuditService</c> (todo lo que pasa por <c>AuditBehavior</c>) insertaba
/// la clase <see cref="AuditLog"/> con el class map por defecto, o sea en
/// PascalCase: <c>TenantId</c>, <c>Timestamp</c>, <c>EntityId</c>. Dos esquemas
/// en una colección, y la consola leía con el class map: al encontrar el primer
/// documento camelCase el deserializador lanzaba
/// <c>FormatException: Element 'tenantId' does not match any field or property
/// of class AuditLog</c> y la pantalla de auditoría entera respondía 500. Pasó en
/// QA el 2026-09-04, con el primer evento de identidad de la primera cooperativa.
/// </para>
///
/// <para>
/// Decisión: <b>el camelCase es el canónico</b>, porque es el que indexa y el que
/// expira; los documentos PascalCase ya escritos no se pueden tocar (la colección
/// es append-only por rol de MongoDB) y siguen siendo rastro regulatorio, así que
/// la lectura entiende <b>las dos formas</b> y la escritura produce sólo una.
/// Nada fuera de esta clase conoce el nombre de un campo.
/// </para>
/// </summary>
public static class AuditDocumentSchema
{
    // ---------------------------------------------------------- canónico --
    public const string TenantId = "tenantId";
    public const string UserId = "userId";
    public const string UserName = "userName";
    public const string Action = "action";
    public const string EntityType = "entityType";
    public const string EntityPublicId = "entityPublicId";
    public const string Module = "module";
    public const string OldValuesJson = "oldValuesJson";
    public const string NewValuesJson = "newValuesJson";
    public const string ChangedFields = "changedFields";
    public const string IpAddress = "ipAddress";
    public const string UserAgent = "userAgent";
    public const string Endpoint = "endpoint";
    public const string HttpMethod = "httpMethod";
    public const string HttpStatusCode = "httpStatusCode";
    public const string DurationMs = "durationMs";
    public const string OccurredAt = "occurredAt";
    public const string Metadata = "metadata";
    /// <summary>
    /// Feature 009 (FR-052): momento en que el evento puede purgarse. Lo calcula
    /// <see cref="IngenIA365ERP.Audit.Indexes.AuditRetention"/> segun el modulo (contabilidad y
    /// navegacion, diez anios; el resto, cinco) y lo aplica un unico indice TTL sobre este campo.
    /// </summary>
    public const string ExpiresAt = "expiresAt";

    /// <summary>
    /// Nombres con los que el class map de <see cref="AuditLog"/> escribió hasta el
    /// 2026-09-04. Sólo se leen; no se vuelve a escribir ninguno.
    /// </summary>
    public static class Heredado
    {
        public const string TenantId = "TenantId";
        public const string UserId = "UserId";
        public const string UserName = "UserName";
        public const string Action = "Action";
        public const string EntityType = "EntityType";
        public const string EntityId = "EntityId";
        public const string Module = "Module";
        public const string OldValues = "OldValues";
        public const string NewValues = "NewValues";
        public const string ChangedFields = "ChangedFields";
        public const string IpAddress = "IpAddress";
        public const string UserAgent = "UserAgent";
        public const string Endpoint = "Endpoint";
        public const string HttpMethod = "HttpMethod";
        public const string HttpStatusCode = "HttpStatusCode";
        public const string DurationMs = "DurationMs";
        public const string Timestamp = "Timestamp";
        public const string Metadata = "Metadata";
    }

    // --------------------------------------------------------- escritura --

    public static BsonDocument ToDocument(AuditEventDocument e)
    {
        var doc = SinMetadata(e);
        // Feature 012 (T36): canal, origen, actor, clave, motivo y código de error. Los eventos de identidad
        // no la llevan y su documento queda como antes.
        if (e.Metadata is { Count: > 0 } metadata)
            doc.Add(Metadata, new BsonDocument(metadata.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => new BsonElement(p.Key, p.Value))));
        return doc;
    }

    private static BsonDocument SinMetadata(AuditEventDocument e) => new()
    {
        { TenantId, e.TenantId },
        { UserId, e.UserId },
        // Antes: `entry.UserName ?? BsonNull.Value.AsString`, que lanza
        // InvalidCastException justo cuando UserName es null. Nunca saltó porque
        // todos los llamadores mandan nombre; ahora tampoco puede saltar.
        { UserName, e.UserName ?? string.Empty },
        { Action, e.Action },
        { EntityType, e.EntityType },
        { EntityPublicId, e.EntityPublicId ?? string.Empty },
        { Module, e.Module ?? string.Empty },
        { OldValuesJson, e.OldValuesJson ?? string.Empty },
        { NewValuesJson, e.NewValuesJson ?? string.Empty },
        { ChangedFields, new BsonArray(e.ChangedFields ?? []) },
        { IpAddress, e.IpAddress ?? string.Empty },
        { UserAgent, e.UserAgent ?? string.Empty },
        { Endpoint, e.Endpoint ?? string.Empty },
        { HttpMethod, e.HttpMethod ?? string.Empty },
        { HttpStatusCode, e.HttpStatusCode ?? 0 },
        { DurationMs, e.DurationMs ?? 0L },
        { OccurredAt, e.OccurredAt },
        { ExpiresAt, IngenIA365ERP.Audit.Indexes.AuditRetention.VenceEl(e.OccurredAt, e.Module) },
    };

    /// <summary>
    /// La cola de <c>MongoAuditService</c> sigue guardando <see cref="AuditLog"/> en
    /// memoria; lo que cambia es que al volcarla ya no se serializa la clase, se
    /// produce el mismo documento que el otro escritor. Los valores viejo/nuevo van
    /// como JSON en texto, igual que allá.
    /// </summary>
    public static BsonDocument ToDocument(AuditLog e)
    {
        var doc = new BsonDocument
        {
            { TenantId, e.TenantId ?? string.Empty },
            { UserId, e.UserId ?? string.Empty },
            { UserName, e.UserName ?? string.Empty },
            { Action, e.Action ?? string.Empty },
            { EntityType, e.EntityType ?? string.Empty },
            { EntityPublicId, e.EntityId ?? string.Empty },
            { Module, e.Module ?? string.Empty },
            { OldValuesJson, e.OldValues?.ToJson() ?? string.Empty },
            { NewValuesJson, e.NewValues?.ToJson() ?? string.Empty },
            { ChangedFields, new BsonArray(e.ChangedFields ?? []) },
            { IpAddress, e.IpAddress ?? string.Empty },
            { UserAgent, e.UserAgent ?? string.Empty },
            { Endpoint, e.Endpoint ?? string.Empty },
            { HttpMethod, e.HttpMethod ?? string.Empty },
            { HttpStatusCode, e.HttpStatusCode ?? 0 },
            { DurationMs, e.DurationMs },
            { OccurredAt, e.Timestamp },
            { ExpiresAt, IngenIA365ERP.Audit.Indexes.AuditRetention.VenceEl(e.Timestamp, e.Module) },
        };
        if (e.Metadata is not null) doc.Add(Metadata, e.Metadata);
        return doc;
    }

    // ----------------------------------------------------------- lectura --

    /// <summary>
    /// Entiende las dos formas. Un elemento desconocido no es un error: se ignora,
    /// que es lo que el class map se negaba a hacer.
    /// </summary>
    public static AuditLogEntry ToEntry(BsonDocument d) => new(
        Id: Identificador(d),
        TenantId: Texto(d, TenantId, Heredado.TenantId),
        UserId: Texto(d, UserId, Heredado.UserId),
        UserName: Texto(d, UserName, Heredado.UserName),
        Action: Texto(d, Action, Heredado.Action) ?? string.Empty,
        EntityType: Texto(d, EntityType, Heredado.EntityType) ?? string.Empty,
        EntityId: Texto(d, EntityPublicId, Heredado.EntityId),
        Module: Texto(d, Module, Heredado.Module),
        OldValues: Json(d, OldValuesJson, Heredado.OldValues),
        NewValues: Json(d, NewValuesJson, Heredado.NewValues),
        ChangedFields: Lista(d, ChangedFields, Heredado.ChangedFields),
        IpAddress: Texto(d, IpAddress, Heredado.IpAddress),
        Endpoint: Texto(d, Endpoint, Heredado.Endpoint),
        DurationMs: Entero(d, DurationMs, Heredado.DurationMs),
        Timestamp: Fecha(d, OccurredAt, Heredado.Timestamp),
        Metadata: Diccionario(d, Metadata, Heredado.Metadata),
        ChainSeq: Secuencia(d));

    // ----------------------------------------------------------- filtros --

    /// <summary>
    /// Cada condición se pide sobre el nombre canónico <b>o</b> el heredado. Sin eso
    /// un filtro por usuario dejaría fuera todo lo escrito antes del 2026-09-04,
    /// que sigue siendo rastro regulatorio.
    /// </summary>
    public static FilterDefinition<BsonDocument> Filtro(AuditQueryParameters q)
    {
        var f = Builders<BsonDocument>.Filter;
        var partes = new List<FilterDefinition<BsonDocument>>();

        if (!string.IsNullOrEmpty(q.UserId)) partes.Add(Igual(UserId, Heredado.UserId, q.UserId));
        if (!string.IsNullOrEmpty(q.EntityType)) partes.Add(Igual(EntityType, Heredado.EntityType, q.EntityType));
        if (!string.IsNullOrEmpty(q.EntityId)) partes.Add(Igual(EntityPublicId, Heredado.EntityId, q.EntityId));
        if (!string.IsNullOrEmpty(q.Module)) partes.Add(Igual(Module, Heredado.Module, q.Module));
        if (!string.IsNullOrEmpty(q.Action)) partes.Add(Igual(Action, Heredado.Action, q.Action));
        if (q.From.HasValue || q.To.HasValue) partes.Add(Rango(q.From, q.To));
        // Feature 012 (T423): varios módulos (los encadenados) y sólo los rechazos o sólo lo aceptado.
        if (q.Modules is { Count: > 0 } modulos)
            partes.Add(f.Or(f.In(Module, modulos), f.In(Heredado.Module, modulos)));
        if (string.Equals(q.Outcome, ResultadoRechazado, StringComparison.OrdinalIgnoreCase))
            partes.Add(Igual(Action, Heredado.Action, ResultadoRechazado));
        else if (string.Equals(q.Outcome, ResultadoAceptado, StringComparison.OrdinalIgnoreCase))
            partes.Add(f.And(f.Ne(Action, ResultadoRechazado), f.Ne(Heredado.Action, ResultadoRechazado)));

        return partes.Count == 0 ? f.Empty : f.And(partes);
    }

    public static FilterDefinition<BsonDocument> FiltroPorEntidad(string entityType, string entityId) =>
        Builders<BsonDocument>.Filter.And(
            Igual(EntityType, Heredado.EntityType, entityType),
            Igual(EntityPublicId, Heredado.EntityId, entityId));

    public static FilterDefinition<BsonDocument> FiltroPorUsuario(string userId, DateTime desde, DateTime hasta) =>
        Builders<BsonDocument>.Filter.And(
            Igual(UserId, Heredado.UserId, userId),
            Rango(desde, hasta));

    /// <summary>
    /// Los documentos canónicos primero (por <c>occurredAt</c>, que está indexado);
    /// los heredados, que no tienen ese campo, quedan detrás ordenados por el suyo.
    /// </summary>
    public static SortDefinition<BsonDocument> MasRecientePrimero =>
        Builders<BsonDocument>.Sort.Descending(OccurredAt).Descending(Heredado.Timestamp);

    public static FilterDefinition<BsonDocument> Igual(string canonico, string heredado, string valor)
    {
        var f = Builders<BsonDocument>.Filter;
        return f.Or(f.Eq(canonico, valor), f.Eq(heredado, valor));
    }

    public static FilterDefinition<BsonDocument> Rango(DateTime? desde, DateTime? hasta)
    {
        var f = Builders<BsonDocument>.Filter;
        return f.Or(RangoSobre(OccurredAt, desde, hasta), RangoSobre(Heredado.Timestamp, desde, hasta));
    }

    private static FilterDefinition<BsonDocument> RangoSobre(string campo, DateTime? desde, DateTime? hasta)
    {
        var f = Builders<BsonDocument>.Filter;
        var partes = new List<FilterDefinition<BsonDocument>>();
        if (desde.HasValue) partes.Add(f.Gte(campo, desde.Value));
        if (hasta.HasValue) partes.Add(f.Lte(campo, hasta.Value));
        return f.And(partes);
    }

    /// <summary>La acción de un rechazo (<c>AuditEventTypes.CommandRejected</c>), el valor de <c>Outcome</c> que la pide.</summary>
    public const string ResultadoRechazado = "Rejected";

    /// <summary>El valor de <c>Outcome</c> que pide todo lo que no es un rechazo.</summary>
    public const string ResultadoAceptado = "Accepted";

    // ---------------------------------------------------------- ayudantes --

    /// <summary>La metadata (canal, origen, actor, clave, motivo, código de error) como texto; nula si no hay.</summary>
    private static IReadOnlyDictionary<string, string>? Diccionario(BsonDocument d, string canonico, string heredado)
    {
        var v = Valor(d, canonico, heredado);
        if (v is null || !v.IsBsonDocument) return null;
        var resultado = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var e in v.AsBsonDocument)
            if (!e.Value.IsBsonNull) resultado[e.Name] = e.Value.IsString ? e.Value.AsString : e.Value.ToString() ?? string.Empty;
        return resultado.Count == 0 ? null : resultado;
    }

    /// <summary>La posición en la cadena de sellos (<c>chain.seq</c>, feature 012 T38); nula fuera de los módulos encadenados.</summary>
    private static long? Secuencia(BsonDocument d)
    {
        if (!d.TryGetValue("chain", out var cadena) || !cadena.IsBsonDocument) return null;
        return cadena.AsBsonDocument.TryGetValue("seq", out var seq) && seq.IsNumeric ? seq.ToInt64() : null;
    }

    private static BsonValue? Valor(BsonDocument d, string canonico, string heredado)
    {
        if (d.TryGetValue(canonico, out var v) && !v.IsBsonNull) return v;
        if (d.TryGetValue(heredado, out v) && !v.IsBsonNull) return v;
        return null;
    }

    private static string Identificador(BsonDocument d)
    {
        if (!d.TryGetValue("_id", out var id) || id.IsBsonNull) return string.Empty;
        return id.IsObjectId ? id.AsObjectId.ToString() : id.ToString() ?? string.Empty;
    }

    private static string? Texto(BsonDocument d, string canonico, string heredado)
    {
        var v = Valor(d, canonico, heredado);
        if (v is null) return null;
        var s = v.IsString ? v.AsString : v.ToString();
        return string.IsNullOrEmpty(s) ? null : s;
    }

    /// <summary>
    /// Canónico: JSON en texto. Heredado: un subdocumento, que se vuelve JSON. Vacío
    /// en cualquiera de los dos es «no hay valores», no una cadena vacía.
    /// </summary>
    private static string? Json(BsonDocument d, string canonico, string heredado)
    {
        var v = Valor(d, canonico, heredado);
        if (v is null) return null;
        if (v.IsBsonDocument) return v.AsBsonDocument.ToJson();
        if (v.IsString) return string.IsNullOrWhiteSpace(v.AsString) ? null : v.AsString;
        return v.ToJson();
    }

    private static List<string>? Lista(BsonDocument d, string canonico, string heredado)
    {
        var v = Valor(d, canonico, heredado);
        if (v is null || !v.IsBsonArray) return null;
        return v.AsBsonArray
            .Where(x => !x.IsBsonNull)
            .Select(x => x.IsString ? x.AsString : x.ToString() ?? string.Empty)
            .ToList();
    }

    private static long Entero(BsonDocument d, string canonico, string heredado)
    {
        var v = Valor(d, canonico, heredado);
        return v is not null && v.IsNumeric ? v.ToInt64() : 0L;
    }

    private static DateTime Fecha(BsonDocument d, string canonico, string heredado)
    {
        var v = Valor(d, canonico, heredado);
        if (v is null) return DateTime.MinValue;
        if (v.IsValidDateTime) return v.ToUniversalTime();
        if (v.IsString && DateTime.TryParse(v.AsString, null,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                out var parsed))
        {
            return parsed;
        }
        return DateTime.MinValue;
    }
}
