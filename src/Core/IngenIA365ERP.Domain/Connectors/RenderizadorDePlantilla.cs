using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace IngenIA365ERP.Domain.Connectors;

/// <summary>Un problema de un conector: su código tipado y el mensaje para quien lo configura.</summary>
public sealed record ProblemaDeConector(string Codigo, string Mensaje);

/// <summary>Una variable de un conector: su nombre (<c>vehiculo.placa</c>) y su tipo.</summary>
public sealed record VariableDeConector(string Nombre, TipoDeVariable Tipo, string Descripcion, bool Sensible = false);

public enum TipoDeVariable
{
    Texto = 1,
    Entero = 2,
    Decimal = 3,
    Fecha = 4,
}

/// <summary>
/// El catálogo fijo de variables que una plantilla de conector puede usar (feature 020, R15; contracts/api.md §1, «Variables disponibles»).
/// <c>cred.&lt;campo&gt;</c> son los campos de credencial que declara la definición y sólo se admiten en la autenticación.
/// </summary>
public static class VariablesDeConector
{
    public const string PrefijoDeCredencial = "cred.";

    public static IReadOnlyList<VariableDeConector> Todas { get; } =
    [
        new("vehiculo.placa", TipoDeVariable.Texto, "Placa del vehículo"),
        new("vehiculo.numeroInterno", TipoDeVariable.Texto, "Número interno del vehículo en la cooperativa"),
        new("vehiculo.radioMovil", TipoDeVariable.Texto, "Código radio-móvil del vehículo (lo identifica ante el sistema externo)"),
        new("propietario.documento", TipoDeVariable.Texto, "Documento del propietario", Sensible: true),
        new("propietario.nombre", TipoDeVariable.Texto, "Nombre del propietario", Sensible: true),
        new("servicio.codigo", TipoDeVariable.Texto, "Código del producto del servicio"),
        new("servicio.nombre", TipoDeVariable.Texto, "Nombre del servicio"),
        new("periodo.inicio", TipoDeVariable.Fecha, "Primer día del período facturado"),
        new("periodo.fin", TipoDeVariable.Fecha, "Último día del período facturado"),
        new("periodo.texto", TipoDeVariable.Texto, "Período en palabras («diciembre 2026»)"),
        new("pago.valor", TipoDeVariable.Decimal, "Valor del servicio pagado"),
        new("pago.recibo", TipoDeVariable.Texto, "Número del recibo o de la factura de contado que lo pagó"),
        new("pago.fecha", TipoDeVariable.Fecha, "Fecha del pago"),
        new("factura.numero", TipoDeVariable.Texto, "Número de la factura de Comercial"),
        new("envio.id", TipoDeVariable.Texto, "Identificador único del envío: el mismo en todos los reintentos"),
    ];

    private static readonly Dictionary<string, VariableDeConector> PorNombre = Todas.ToDictionary(v => v.Nombre, StringComparer.Ordinal);

    public static VariableDeConector? Buscar(string nombre) => PorNombre.GetValueOrDefault(nombre);

    /// <summary>Los nombres de las variables sensibles: no se escriben en el cuerpo saneado del registro ni en la auditoría.</summary>
    public static IReadOnlySet<string> Sensibles { get; } = Todas.Where(v => v.Sensible).Select(v => v.Nombre).ToHashSet(StringComparer.Ordinal);
}

/// <summary>
/// Renderiza las plantillas de un conector (feature 020, R15; FR-036): marcadores <c>{{variable}}</c> y <c>{{variable:formato}}</c> con formato
/// <c>int</c> (entero sin decimales), <c>dec2</c> (dos decimales, punto), <c>date(aaaa-MM-dd)</c> o texto por defecto, escapado para JSON (las
/// comillas las pone la plantilla). No hay expresiones ni lógica: una variable desconocida es un error al guardar la versión, y una variable
/// sin valor al enviar también (el envío queda «revisar»). Lo usan el envío y la simulación del maestro: el mismo cuerpo.
/// </summary>
public static partial class RenderizadorDePlantilla
{
    [GeneratedRegex(@"\{\{\s*([A-Za-z][A-Za-z0-9_.]*)\s*(?::\s*([A-Za-z0-9]+(?:\([^)]*\))?)\s*)?\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex Marcador();

    private static readonly JsonEncodedText Vacio = JsonEncodedText.Encode(string.Empty);

    /// <summary>Los nombres de variable que usa una plantilla.</summary>
    public static IReadOnlyList<string> Variables(string? plantilla) =>
        string.IsNullOrEmpty(plantilla) ? [] : Marcador().Matches(plantilla).Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal).ToList();

    /// <summary>
    /// Revisa una plantilla al guardar la versión: llaves balanceadas, variables del catálogo, formatos válidos y <c>cred.*</c> sólo en la
    /// autenticación y declaradas en <paramref name="camposDeCredencial"/>.
    /// </summary>
    public static IReadOnlyList<ProblemaDeConector> Validar(string? plantilla, bool enAutenticacion, IReadOnlySet<string> camposDeCredencial, string donde)
    {
        var problemas = new List<ProblemaDeConector>();
        if (string.IsNullOrEmpty(plantilla)) return problemas;
        var sinMarcadores = Marcador().Replace(plantilla, string.Empty);
        if (sinMarcadores.Contains("{{", StringComparison.Ordinal) || sinMarcadores.Contains("}}", StringComparison.Ordinal))
            problemas.Add(new("Connector.TemplateSyntax", $"La plantilla de {donde} tiene un marcador mal escrito: use {{{{variable}}}} o {{{{variable:formato}}}}."));

        foreach (Match m in Marcador().Matches(plantilla))
        {
            var nombre = m.Groups[1].Value;
            var formato = m.Groups[2].Success ? m.Groups[2].Value : null;
            if (nombre.StartsWith(VariablesDeConector.PrefijoDeCredencial, StringComparison.Ordinal))
            {
                if (!enAutenticacion)
                    problemas.Add(new("Connector.CredentialOutsideAuth", $"«{nombre}» es un campo de credencial: sólo se usa en la autenticación, no en {donde}."));
                else if (!camposDeCredencial.Contains(nombre[VariablesDeConector.PrefijoDeCredencial.Length..]))
                    problemas.Add(new("Connector.UnknownVariable", $"«{nombre}» no está entre los campos de credencial que declara el conector."));
            }
            else if (VariablesDeConector.Buscar(nombre) is null)
            {
                problemas.Add(new("Connector.UnknownVariable", $"«{nombre}» no es una variable de los conectores (en {donde})."));
            }
            if (formato is not null && !FormatoValido(formato))
                problemas.Add(new("Connector.UnknownFormat", $"«{formato}» no es un formato: use int, dec2, date(aaaa-MM-dd) o ninguno (texto)."));
        }
        return problemas;
    }

    /// <summary>
    /// Renderiza con los valores dados (texto, número o fecha). Devuelve el texto, o la lista de variables que no tienen valor.
    /// </summary>
    public static (string? Texto, IReadOnlyList<string> Faltantes) Renderizar(string? plantilla, IReadOnlyDictionary<string, object?> valores)
    {
        if (string.IsNullOrEmpty(plantilla)) return (string.Empty, []);
        var faltan = new List<string>();
        var texto = Marcador().Replace(plantilla, m =>
        {
            var nombre = m.Groups[1].Value;
            var formato = m.Groups[2].Success ? m.Groups[2].Value : null;
            if (!valores.TryGetValue(nombre, out var valor) || valor is null || valor is string { Length: 0 })
            {
                faltan.Add(nombre);
                return string.Empty;
            }
            return Formatear(valor, formato);
        });
        return faltan.Count == 0 ? (texto, []) : (null, faltan.Distinct(StringComparer.Ordinal).ToList());
    }

    /// <summary>El cuerpo con las variables sensibles (y toda credencial) cambiadas por <c>***</c>: lo que se registra.</summary>
    public static string Sanear(string? plantilla, IReadOnlyDictionary<string, object?> valores, IReadOnlySet<string> sensibles)
    {
        if (string.IsNullOrEmpty(plantilla)) return string.Empty;
        return Marcador().Replace(plantilla, m =>
        {
            var nombre = m.Groups[1].Value;
            if (nombre.StartsWith(VariablesDeConector.PrefijoDeCredencial, StringComparison.Ordinal) || sensibles.Contains(nombre)) return "***";
            var formato = m.Groups[2].Success ? m.Groups[2].Value : null;
            return valores.TryGetValue(nombre, out var v) && v is not null ? Formatear(v, formato) : string.Empty;
        });
    }

    private static bool FormatoValido(string formato) =>
        formato is "int" or "dec2" or "text" || (formato.StartsWith("date(", StringComparison.Ordinal) && formato.EndsWith(')'));

    private static string Formatear(object valor, string? formato)
    {
        switch (formato)
        {
            case "int":
                return Decimal(valor) is { } d ? Math.Round(d, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) : Escapar(Convert.ToString(valor, CultureInfo.InvariantCulture));
            case "dec2":
                return Decimal(valor) is { } e ? Math.Round(e, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture) : Escapar(Convert.ToString(valor, CultureInfo.InvariantCulture));
            case { } f when f.StartsWith("date(", StringComparison.Ordinal):
                var patron = f[5..^1];
                var fecha = valor switch
                {
                    DateOnly x => x,
                    DateTime y => DateOnly.FromDateTime(y),
                    _ => DateOnly.TryParse(Convert.ToString(valor, CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, out var z) ? z : (DateOnly?)null,
                };
                return fecha is { } g ? Escapar(g.ToString(string.IsNullOrWhiteSpace(patron) ? "yyyy-MM-dd" : patron, CultureInfo.InvariantCulture)) : Escapar(valor.ToString());
            default:
                return valor switch
                {
                    DateOnly x => x.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    decimal y => y.ToString(CultureInfo.InvariantCulture),
                    _ => Escapar(Convert.ToString(valor, CultureInfo.InvariantCulture)),
                };
        }
    }

    private static decimal? Decimal(object valor) => valor switch
    {
        decimal d => d,
        int i => i,
        long l => l,
        double db => (decimal)db,
        string s when decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var x) => x,
        _ => null,
    };

    /// <summary>El texto escapado para ir dentro de comillas JSON (sin las comillas).</summary>
    private static string Escapar(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return Vacio.ToString();
        var codificado = JsonEncodedText.Encode(texto, JavaScriptEncoder.UnsafeRelaxedJsonEscaping);
        return codificado.ToString();
    }
}

/// <summary>Qué hacer con una respuesta del sistema externo (FR-037).</summary>
public sealed record ReglaDeRespuesta(int Status, string? Contains, Enums.Connectors.ConnectorOutcome Outcome);

/// <summary>
/// El mapa de respuestas de un conector (feature 020, R15; FR-037): reglas en orden por código HTTP y, si la trae, un texto que la respuesta
/// contiene (sin distinguir mayúsculas ni tildes). <c>status: 0</c> es un error de red o un tiempo agotado. Lo no previsto es «revisar a mano».
/// </summary>
public static class MapaDeRespuestas
{
    public static Enums.Connectors.ConnectorOutcome Clasificar(IReadOnlyList<ReglaDeRespuesta> reglas, int status, string? cuerpo)
    {
        var texto = Normalizar(cuerpo);
        foreach (var r in reglas)
        {
            if (r.Status != status) continue;
            if (string.IsNullOrEmpty(r.Contains) || texto.Contains(Normalizar(r.Contains), StringComparison.Ordinal)) return r.Outcome;
        }
        return Enums.Connectors.ConnectorOutcome.ManualReview;
    }

    public static string Normalizar(string? texto)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;
        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
        return sb.ToString();
    }
}
