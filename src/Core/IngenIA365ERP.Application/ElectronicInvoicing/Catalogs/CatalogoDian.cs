using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace IngenIA365ERP.Application.ElectronicInvoicing.Catalogs;

/// <summary>
/// Los catálogos de la DIAN como <b>datos versionados por vigencia</b> (feature 012, T180; decisiones-transversales T24;
/// contracts/dian.md §1 y §4.4; data-model «catálogos DIAN como JSON versionados»): unidades UN/ECE Rec. 20, medios y formas
/// de pago, tipos de identificación —con la traducción de <c>COR_People.IdType</c> y la identificación del consumidor
/// final (Res. 202/2025)— y conceptos de corrección de las notas. Viven en <c>Catalogs/Data/*.json</c> embebidos, sin
/// tablas: una versión nueva de un anexo es otra entrada con <c>vigenteDesde</c>, no lógica. Toda lectura es <b>a una
/// fecha</b>; antes de la primera vigencia no hay catálogo.
///
/// <para>
/// Lo consumen I1 e I3 antes de I4: <c>dianUnitCode</c> de las unidades (T214), el consumidor final (T587), el medio de
/// pago (T590) y el concepto de corrección de las notas (T612); la copia fiscal de la contraparte
/// (<c>FotoDeLaContraparte</c>) traduce aquí el tipo de identificación. T702 (US8) lo amplía sin recrearlo. (nuevo)
/// </para>
/// </summary>
public sealed class CatalogoDian
{
    private static readonly Lazy<CatalogoDian> Cargado = new(CargarEmbebido);

    /// <summary>Los catálogos embebidos en el ensamblado.</summary>
    public static CatalogoDian Embebido => Cargado.Value;

    private readonly Dictionary<string, List<VersionDelCatalogo>> _catalogos;

    private CatalogoDian(Dictionary<string, List<VersionDelCatalogo>> catalogos) => _catalogos = catalogos;

    /// <summary>Un catálogo armado con el contenido de uno o varios archivos JSON (pruebas y cargas explícitas).</summary>
    public static CatalogoDian DesdeJson(params string[] contenidos)
    {
        var archivos = contenidos.Select(c => JsonSerializer.Deserialize<ArchivoDeCatalogo>(c, Json)
            ?? throw new InvalidOperationException("Un catálogo DIAN vino vacío."));
        return Armar(archivos);
    }

    // ------------------------------------------------------------------------------------------ unidades --

    public IReadOnlyList<CodigoDian> Unidades(DateOnly fecha) => Codigos(Catalogos.Unidades, fecha);

    public CodigoDian? Unidad(string? codigo, DateOnly fecha) => Buscar(Catalogos.Unidades, codigo, fecha);

    public bool EsUnidadValida(string? codigo, DateOnly fecha) => Unidad(codigo, fecha) is not null;

    // ------------------------------------------------------------------------------------------ pagos --

    public IReadOnlyList<CodigoDian> MediosDePago(DateOnly fecha) => Codigos(Catalogos.MediosDePago, fecha);

    public CodigoDian? MedioDePago(string? codigo, DateOnly fecha) => Buscar(Catalogos.MediosDePago, codigo, fecha);

    /// <summary>Contado (1) y crédito (2); la clase del documento elige cuál.</summary>
    public IReadOnlyList<CodigoDian> FormasDePago(DateOnly fecha) =>
        Vigente(Catalogos.MediosDePago, fecha)?.FormasDePago.Select(c => c.ComoCodigo()).ToList() ?? [];

    // ------------------------------------------------------------------------------------------ identificación --

    public IReadOnlyList<CodigoDian> TiposDeIdentificacion(DateOnly fecha) => Codigos(Catalogos.TiposDeIdentificacion, fecha);

    public CodigoDian? TipoDeIdentificacion(string? codigo, DateOnly fecha) => Buscar(Catalogos.TiposDeIdentificacion, codigo, fecha);

    /// <summary>
    /// El código DIAN del tipo de identificación guardado en <c>COR_People.IdType</c> («C», «NI», «CE»…), por la tabla del
    /// catálogo vigente (como hace la dispersión con «C» → «CC»). Nulo si no tiene traducción: no se inventa una cédula;
    /// el canónico lo reporta como dato faltante.
    /// </summary>
    public string? TipoDeIdentificacionDe(string? idType, DateOnly fecha)
    {
        if (string.IsNullOrWhiteSpace(idType)) return null;
        var version = Vigente(Catalogos.TiposDeIdentificacion, fecha);
        if (version?.TraduccionIdType is null) return null;
        return version.TraduccionIdType.TryGetValue(idType.Trim().ToUpperInvariant(), out var dian) ? dian : null;
    }

    /// <summary>La identificación genérica del adquirente «consumidor final» (Res. 202/2025), o nulo sin catálogo vigente.</summary>
    public ConsumidorFinalDian? ConsumidorFinal(DateOnly fecha) => Vigente(Catalogos.TiposDeIdentificacion, fecha)?.ConsumidorFinal;

    // ------------------------------------------------------------------------------------------ notas --

    public IReadOnlyList<ConceptoDeCorreccionDian> ConceptosDeCorreccion(ClaseDeNotaDian clase, DateOnly fecha) =>
        Vigente(Catalogos.ConceptosDeCorreccion, fecha)?.Codigos
            .Where(c => c.AplicaA.Contains(clase))
            .Select(c => new ConceptoDeCorreccionDian(c.Codigo, c.Nombre, c.EsAnulacion))
            .ToList() ?? [];

    public ConceptoDeCorreccionDian? ConceptoDeCorreccion(ClaseDeNotaDian clase, string? codigo, DateOnly fecha) =>
        string.IsNullOrWhiteSpace(codigo) ? null : ConceptosDeCorreccion(clase, fecha).FirstOrDefault(c => c.Codigo == codigo.Trim());

    // ------------------------------------------------------------------------------------------ comunes --

    /// <summary>De dónde sale la versión vigente de un catálogo y si está por cotejar (para las alertas de alistamiento).</summary>
    public (string Fuente, bool PorCotejar)? Procedencia(string catalogo, DateOnly fecha) =>
        Vigente(catalogo, fecha) is { } v ? (v.Fuente, v.PorCotejar) : null;

    private IReadOnlyList<CodigoDian> Codigos(string catalogo, DateOnly fecha) =>
        Vigente(catalogo, fecha)?.Codigos.Select(c => c.ComoCodigo()).ToList() ?? [];

    private CodigoDian? Buscar(string catalogo, string? codigo, DateOnly fecha)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return null;
        var buscado = codigo.Trim();
        return Vigente(catalogo, fecha)?.Codigos
            .FirstOrDefault(c => string.Equals(c.Codigo, buscado, StringComparison.OrdinalIgnoreCase))?.ComoCodigo();
    }

    private VersionDelCatalogo? Vigente(string catalogo, DateOnly fecha) =>
        _catalogos.TryGetValue(catalogo, out var versiones) ? versiones.FirstOrDefault(v => v.CubreA(fecha)) : null;

    private static CatalogoDian CargarEmbebido()
    {
        var ensamblado = Assembly.GetExecutingAssembly();
        var archivos = ensamblado.GetManifestResourceNames()
            .Where(n => n.Contains(".ElectronicInvoicing.Catalogs.Data.", StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Select(n =>
            {
                using var flujo = ensamblado.GetManifestResourceStream(n)!;
                return JsonSerializer.Deserialize<ArchivoDeCatalogo>(flujo, Json)
                       ?? throw new InvalidOperationException($"El catálogo DIAN embebido {n} está vacío.");
            });
        return Armar(archivos);
    }

    private static CatalogoDian Armar(IEnumerable<ArchivoDeCatalogo> archivos)
    {
        var catalogos = new Dictionary<string, List<VersionDelCatalogo>>(StringComparer.Ordinal);
        foreach (var archivo in archivos)
        {
            if (string.IsNullOrWhiteSpace(archivo.Catalogo)) throw new InvalidOperationException("Un catálogo DIAN no dice qué catálogo es.");
            if (!catalogos.TryGetValue(archivo.Catalogo, out var lista)) catalogos[archivo.Catalogo] = lista = [];
            lista.AddRange(archivo.Versiones);
        }
        foreach (var (nombre, versiones) in catalogos)
        {
            versiones.Sort((a, b) => a.VigenteDesde.CompareTo(b.VigenteDesde));
            for (var i = 1; i < versiones.Count; i++)
            {
                var anterior = versiones[i - 1];
                if (anterior.VigenteHasta is not { } hasta || hasta >= versiones[i].VigenteDesde)
                    throw new InvalidOperationException(
                        $"El catálogo DIAN «{nombre}» tiene dos vigencias que se cruzan ({anterior.VigenteDesde:yyyy-MM-dd} y {versiones[i].VigenteDesde:yyyy-MM-dd}): cierre la anterior.");
            }
        }
        return new CatalogoDian(catalogos);
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Los nombres de los catálogos en los archivos.</summary>
    public static class Catalogos
    {
        public const string Unidades = "unidades";
        public const string MediosDePago = "mediosDePago";
        public const string TiposDeIdentificacion = "tiposDeIdentificacion";
        public const string ConceptosDeCorreccion = "conceptosDeCorreccion";
    }

    // ------------------------------------------------------------------------------------------ el archivo --

    private sealed class ArchivoDeCatalogo
    {
        public string Catalogo { get; set; } = string.Empty;
        public List<VersionDelCatalogo> Versiones { get; set; } = [];
    }

    private sealed class VersionDelCatalogo
    {
        public DateOnly VigenteDesde { get; set; }
        public DateOnly? VigenteHasta { get; set; }
        public string Fuente { get; set; } = string.Empty;
        public bool PorCotejar { get; set; }
        public string? Notas { get; set; }
        public List<CodigoEnArchivo> Codigos { get; set; } = [];
        public List<CodigoEnArchivo> FormasDePago { get; set; } = [];
        public Dictionary<string, string>? TraduccionIdType { get; set; }
        public ConsumidorFinalDian? ConsumidorFinal { get; set; }

        public bool CubreA(DateOnly fecha) => fecha >= VigenteDesde && (VigenteHasta is null || fecha <= VigenteHasta);
    }

    private sealed class CodigoEnArchivo
    {
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public bool EsAnulacion { get; set; }
        public List<ClaseDeNotaDian> AplicaA { get; set; } = [];

        public CodigoDian ComoCodigo() => new(Codigo, Nombre);
    }
}

/// <summary>Un código de un catálogo DIAN con su nombre. (nuevo)</summary>
public sealed record CodigoDian(string Codigo, string Nombre);

/// <summary>La identificación genérica del consumidor final (Res. 202/2025). (nuevo)</summary>
public sealed record ConsumidorFinalDian(string TipoDeIdentificacion, string Numero, string Nombre);

/// <summary>Un concepto de corrección de nota; <see cref="EsAnulacion"/> es el que exige <c>IsFullReversal</c>. (nuevo)</summary>
public sealed record ConceptoDeCorreccionDian(string Codigo, string Nombre, bool EsAnulacion);

/// <summary>A qué nota se aplica un concepto de corrección (data-model §14). (nuevo)</summary>
public enum ClaseDeNotaDian
{
    NotaCredito = 1,
    NotaDebito = 2,
    NotaDeAjustePos = 3,
    NotaDeAjusteDelDocumentoSoporte = 4,
}
