using System.Text.Json;

namespace IngenIA365ERP.Domain.Common.NationalValues;

/// <summary>
/// Punto de extensión por clase de documento normativo (feature 013, T45, R2; DT §2.9; data-model parte 3 §3.2). La
/// central valida el contenido antes de guardarlo con el validador de su <see cref="Clase"/>; si devuelve errores, el
/// registro se rechaza con <c>NationalValues.Document.Invalid</c> y <c>data.errors[] { path, message }</c>. La plataforma
/// trae los de <see cref="RegulatoryDocumentKind.CatalogoDeModalidades"/> y <see cref="RegulatoryDocumentKind.TiposDeOrganizacion"/>;
/// Cartera registra los de <c>SarcAnexo1</c>, <c>SarcAnexo2</c> y <c>ReglaDePerdidaEsperada</c> (la plataforma no depende
/// de un módulo, CH34).
/// </summary>
public interface IValidadorDeDocumentoNormativo
{
    RegulatoryDocumentKind Clase { get; }

    /// <summary>Versiones de esquema que sabe leer (las anteriores se conservan para reproducir cierres viejos).</summary>
    IReadOnlyList<short> VersionesDeEsquema { get; }

    /// <summary>Los errores del contenido con su ruta JSON (<c>$.modalities[0].code</c>); vacío si es válido.</summary>
    IReadOnlyList<ErrorDeDocumentoNormativo> Validar(string contenido, short versionDeEsquema);
}

/// <summary>Un error de un documento normativo: la ruta del JSON (o <c>schemaVersion</c>) y el motivo.</summary>
public sealed record ErrorDeDocumentoNormativo(string Ruta, string Mensaje);

/// <summary>El documento tipado leído, o los errores que impidieron leerlo.</summary>
public sealed record ResultadoDeDocumento<T>(T? Documento, IReadOnlyList<ErrorDeDocumentoNormativo> Errores) where T : class
{
    public bool EsValido => Errores.Count == 0 && Documento is not null;
}

/// <summary>Ayudas comunes de los lectores tipados: abrir el JSON (con comentarios) y leer propiedades con su ruta.</summary>
internal static class LecturaDeDocumento
{
    private static readonly JsonDocumentOptions Opciones = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static JsonDocument? Abrir(string? contenido, List<ErrorDeDocumentoNormativo> errores)
    {
        if (string.IsNullOrWhiteSpace(contenido))
        {
            errores.Add(new("$", "El documento está vacío."));
            return null;
        }
        try
        {
            var doc = JsonDocument.Parse(contenido, Opciones);
            if (doc.RootElement.ValueKind == JsonValueKind.Object) return doc;
            doc.Dispose();
            errores.Add(new("$", "El documento debe ser un objeto JSON."));
            return null;
        }
        catch (JsonException ex)
        {
            errores.Add(new("$", $"El documento no es JSON válido: {ex.Message}"));
            return null;
        }
    }

    public static bool VersionSoportada(short version, IReadOnlyList<short> soportadas, List<ErrorDeDocumentoNormativo> errores)
    {
        if (soportadas.Contains(version)) return true;
        errores.Add(new("schemaVersion", $"La versión de esquema {version} no se conoce; se admiten: {string.Join(", ", soportadas)}."));
        return false;
    }

    public static string? Texto(JsonElement objeto, string propiedad, string ruta, bool requerido, List<ErrorDeDocumentoNormativo> errores)
    {
        if (!objeto.TryGetProperty(propiedad, out var v) || v.ValueKind == JsonValueKind.Null)
        {
            if (requerido) errores.Add(new($"{ruta}.{propiedad}", "Es obligatorio."));
            return null;
        }
        if (v.ValueKind != JsonValueKind.String)
        {
            errores.Add(new($"{ruta}.{propiedad}", "Debe ser texto."));
            return null;
        }
        var s = v.GetString();
        if (string.IsNullOrWhiteSpace(s))
        {
            if (requerido) errores.Add(new($"{ruta}.{propiedad}", "Es obligatorio."));
            return null;
        }
        return s.Trim();
    }

    public static bool Booleano(JsonElement objeto, string propiedad, string ruta, bool defecto, List<ErrorDeDocumentoNormativo> errores)
    {
        if (!objeto.TryGetProperty(propiedad, out var v) || v.ValueKind == JsonValueKind.Null) return defecto;
        if (v.ValueKind is JsonValueKind.True or JsonValueKind.False) return v.GetBoolean();
        errores.Add(new($"{ruta}.{propiedad}", "Debe ser verdadero o falso."));
        return defecto;
    }

    public static DateOnly? Fecha(JsonElement objeto, string propiedad, string ruta, List<ErrorDeDocumentoNormativo> errores)
    {
        var s = Texto(objeto, propiedad, ruta, requerido: false, errores);
        if (s is null) return null;
        if (DateOnly.TryParseExact(s, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d))
            return d;
        errores.Add(new($"{ruta}.{propiedad}", "Debe ser una fecha aaaa-mm-dd."));
        return null;
    }

    /// <summary>Los elementos de un arreglo obligatorio; con <paramref name="noVacio"/>, un arreglo vacío es error.</summary>
    public static IReadOnlyList<JsonElement> Arreglo(JsonElement objeto, string propiedad, string ruta, bool noVacio, List<ErrorDeDocumentoNormativo> errores)
    {
        if (!objeto.TryGetProperty(propiedad, out var v) || v.ValueKind != JsonValueKind.Array)
        {
            errores.Add(new($"{ruta}.{propiedad}", "Debe ser una lista."));
            return [];
        }
        var lista = v.EnumerateArray().ToList();
        if (noVacio && lista.Count == 0) errores.Add(new($"{ruta}.{propiedad}", "La lista no puede estar vacía."));
        return lista;
    }

    /// <summary>Un código: mayúsculas, dígitos y guion bajo.</summary>
    public static bool EsCodigo(string s) => s.All(c => c is (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_');
}
