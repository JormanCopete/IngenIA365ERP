using System.Collections;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using IngenIA365ERP.Application.Audit.Common;
using MongoDB.Bson;

namespace IngenIA365ERP.Audit.Integrity;

/// <summary>
/// El sello de integridad de la cadena de auditoría (feature 012, T38; FR-008). <b>Una sola
/// canonicalización</b> para sellar (<c>AuditOutboxForwarder</c>) y para verificar
/// (<c>VerifyAuditIntegrityQuery</c>): si los dos lados escribieran el JSON a su manera, cualquier
/// diferencia de formato se leería como un evento alterado. Casos dorados en
/// <c>Application.Tests/Infrastructure/Audit/Casos</c>.
///
/// <list type="bullet">
/// <item>JSON canónico: objetos con las claves en orden ordinal, sin espacios; instantes en UTC ISO con
/// milisegundos y <c>Z</c>; decimales y números en cultura invariante, con su escala; y la versión
/// <c>v</c> de esta canonicalización en el objeto raíz.</item>
/// <item><c>Hash = SHA-256(PrevHash ‖ canónico)</c> en hexadecimal minúsculo; el primero de la cadena
/// encadena contra <see cref="HashInicial"/>.</item>
/// <item>Ancla: HMAC-SHA256 de <c>stream|seq|hash|anchoredAt</c> con la clave de anclaje de
/// <c>AuditSignature</c> (<see cref="IAuditSignatureService.AnchorKeyVersion"/>, nunca la de los PDF).</item>
/// </list>
///
/// <para>
/// Del documento de Mongo entra todo salvo <c>chain.hash</c> (el propio resultado): el <c>_id</c>, los
/// campos del evento, la metadata, <c>expiresAt</c> y <c>chain</c> con su flujo, posición, hash anterior,
/// algoritmo y versión. Cambiar cualquier byte de cualquiera de ellos cambia el hash.
/// </para>
/// </summary>
public sealed class SelloDeIntegridad(IAuditSignatureService firmas)
{
    /// <summary>Versión de la canonicalización. Cambiar el formato exige otra versión, nunca reescribir ésta.</summary>
    public const int Version = 1;

    public const string Algoritmo = "SHA-256";

    /// <summary>Contra qué encadena el primer evento de un flujo (y el hash del ancla génesis).</summary>
    public static readonly string HashInicial = new('0', 64);

    /// <summary>Nombres del bloque <c>chain</c> del documento de Mongo.</summary>
    public static class Cadena
    {
        public const string Campo = "chain";
        public const string Stream = "stream";
        public const string Seq = "seq";
        public const string PrevHash = "prevHash";
        public const string Hash = "hash";
        public const string Alg = "alg";
        public const string V = "v";
    }

    private const string FormatoDeInstante = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    private static readonly JsonWriterOptions Escritura = new()
    {
        Indented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    // ---------------------------------------------------------- canónico --

    /// <summary>El JSON canónico de un conjunto de campos, con <c>v</c> en la raíz.</summary>
    /// <exception cref="ArgumentException">Si algún valor no es de un tipo que la canonicalización conozca.</exception>
    public static string Canonico(IReadOnlyDictionary<string, object?> campos)
    {
        ArgumentNullException.ThrowIfNull(campos);
        var raiz = new Dictionary<string, object?>(campos, StringComparer.Ordinal) { ["v"] = Version };
        using var memoria = new MemoryStream();
        using (var escritor = new Utf8JsonWriter(memoria, Escritura))
        {
            Escribir(escritor, raiz);
        }
        return Encoding.UTF8.GetString(memoria.ToArray());
    }

    /// <summary>El JSON canónico de un documento de auditoría de Mongo: todo menos <c>chain.hash</c>.</summary>
    public static string Canonico(BsonDocument documento) => Canonico(Campos(documento));

    /// <summary>Los campos de un documento de Mongo como valores .NET, sin <c>chain.hash</c>.</summary>
    public static Dictionary<string, object?> Campos(BsonDocument documento)
    {
        ArgumentNullException.ThrowIfNull(documento);
        var campos = (Dictionary<string, object?>)DeBson(documento)!;
        if (campos.TryGetValue(Cadena.Campo, out var cadena) && cadena is Dictionary<string, object?> bloque)
            bloque.Remove(Cadena.Hash);
        return campos;
    }

    /// <summary><c>SHA-256(prevHash ‖ canónico)</c> en hexadecimal minúsculo.</summary>
    public static string Hash(string prevHash, string canonico)
    {
        ArgumentException.ThrowIfNullOrEmpty(prevHash);
        ArgumentNullException.ThrowIfNull(canonico);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(prevHash + canonico)));
    }

    // -------------------------------------------------------------- anclas --

    /// <summary>Lo que firma el HMAC de un ancla: <c>stream|seq|hash|anchoredAt</c>.</summary>
    public static string CargaDeAncla(string stream, long seq, string hash, DateTime anchoredAt) =>
        string.Join('|', stream, seq.ToString(CultureInfo.InvariantCulture), hash, Instante(anchoredAt));

    /// <summary>Firma un ancla con la clave de anclaje vigente.</summary>
    /// <exception cref="InvalidOperationException">Si no hay clave de anclaje configurada (pregunta A2).</exception>
    public (string Hmac, string KeyVersion) FirmarAncla(string stream, long seq, string hash, DateTime anchoredAt)
    {
        var version = firmas.AnchorKeyVersion ?? throw new InvalidOperationException(
            "[Auditoria.AnclaSinClave] AuditSignature:AnchorKeyVersion no está configurada, no está en Keys o es dev-v1: " +
            "la cadena se sigue sellando, pero sin anclas.");
        var hmac = firmas.ComputeHmacBase64(Encoding.UTF8.GetBytes(CargaDeAncla(stream, seq, hash, anchoredAt)), version);
        return (hmac, version);
    }

    /// <summary>Si el HMAC de un ancla es el de su contenido con la clave de su versión.</summary>
    public bool AnclaValida(string stream, long seq, string hash, DateTime anchoredAt, string hmac, string keyVersion) =>
        firmas.VerifyHmacBase64(Encoding.UTF8.GetBytes(CargaDeAncla(stream, seq, hash, anchoredAt)), hmac, keyVersion);

    // ----------------------------------------------------------- escritura --

    private static void Escribir(Utf8JsonWriter w, object? valor)
    {
        switch (valor)
        {
            case null:
                w.WriteNullValue();
                return;
            case string texto:
                w.WriteStringValue(texto);
                return;
            case bool logico:
                w.WriteBooleanValue(logico);
                return;
            case int or long or short or byte or uint or ulong:
                w.WriteRawValue(Convert.ToString(valor, CultureInfo.InvariantCulture)!, skipInputValidation: true);
                return;
            case decimal numero:
                w.WriteRawValue(numero.ToString(CultureInfo.InvariantCulture), skipInputValidation: true);
                return;
            case double doble:
                w.WriteRawValue(doble.ToString("R", CultureInfo.InvariantCulture), skipInputValidation: true);
                return;
            case DateTime instante:
                w.WriteStringValue(Instante(instante));
                return;
            case DateTimeOffset conZona:
                w.WriteStringValue(Instante(conZona.UtcDateTime));
                return;
            case Guid guid:
                w.WriteStringValue(guid.ToString("D"));
                return;
            case IReadOnlyDictionary<string, object?> objeto:
                EscribirObjeto(w, objeto);
                return;
            case IDictionary<string, object?> objeto:
                EscribirObjeto(w, objeto.ToDictionary(p => p.Key, p => p.Value));
                return;
            case IDictionary<string, string> textos:
                EscribirObjeto(w, textos.ToDictionary(p => p.Key, p => (object?)p.Value));
                return;
            case IEnumerable lista:
                w.WriteStartArray();
                foreach (var elemento in lista) Escribir(w, elemento);
                w.WriteEndArray();
                return;
            default:
                throw new ArgumentException($"La canonicalización no conoce el tipo {valor.GetType().Name}.", nameof(valor));
        }
    }

    private static void EscribirObjeto(Utf8JsonWriter w, IReadOnlyDictionary<string, object?> objeto)
    {
        w.WriteStartObject();
        foreach (var clave in objeto.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            w.WritePropertyName(clave);
            Escribir(w, objeto[clave]);
        }
        w.WriteEndObject();
    }

    private static string Instante(DateTime instante)
    {
        var utc = instante.Kind switch
        {
            DateTimeKind.Local => instante.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(instante, DateTimeKind.Utc),
            _ => instante,
        };
        return utc.ToString(FormatoDeInstante, CultureInfo.InvariantCulture);
    }

    // -------------------------------------------------------------- lectura --

    private static object? DeBson(BsonValue valor) => valor.BsonType switch
    {
        BsonType.Null or BsonType.Undefined => null,
        BsonType.String => valor.AsString,
        BsonType.Boolean => valor.AsBoolean,
        BsonType.Int32 => (long)valor.AsInt32,
        BsonType.Int64 => valor.AsInt64,
        BsonType.Double => valor.AsDouble,
        BsonType.Decimal128 => (decimal)valor.AsDecimal128,
        BsonType.DateTime => valor.ToUniversalTime(),
        BsonType.ObjectId => valor.AsObjectId.ToString(),
        BsonType.Array => valor.AsBsonArray.Select(DeBson).ToList(),
        BsonType.Document => valor.AsBsonDocument.Elements.ToDictionary(e => e.Name, e => DeBson(e.Value), StringComparer.Ordinal),
        _ => valor.ToString(),
    };
}
