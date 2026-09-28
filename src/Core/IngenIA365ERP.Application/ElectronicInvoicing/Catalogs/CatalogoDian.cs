using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using IngenIA365ERP.Domain.Enums.ElectronicInvoicing;

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
/// (<c>FotoDeLaContraparte</c>) traduce aquí el tipo de identificación. T702 (US8, I4) lo amplía sin recrearlo con tipos de
/// documento (y su código en contingencia, tipo de operación y código único), tipos de operación, responsabilidades (desde las
/// marcas tributarias), tributos y esquema tributario, tipos de persona, formas de pago por clase y tipos de caja del DEE; los
/// códigos 20, 94 y 95 van marcados «por cotejar». (nuevo)
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

    /// <summary>
    /// La traducción inversa de <see cref="TipoDeIdentificacionDe"/>: el <c>COR_People.IdType</c> que corresponde a un código DIAN,
    /// eligiendo la forma más corta («13» → «C»), que es la que guarda el maestro. Nulo si ninguno lo traduce. La usa la semilla
    /// del consumidor final (T587). (nuevo)
    /// </summary>
    public string? IdTypeDe(string? codigoDian, DateOnly fecha)
    {
        if (string.IsNullOrWhiteSpace(codigoDian)) return null;
        var version = Vigente(Catalogos.TiposDeIdentificacion, fecha);
        return version?.TraduccionIdType?
            .Where(t => t.Value == codigoDian.Trim())
            .Select(t => t.Key)
            .OrderBy(k => k.Length).ThenBy(k => k, StringComparer.Ordinal)
            .FirstOrDefault();
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

    // ------------------------------------------------------------------------------------------ I4 (T702) --

    /// <summary>Los tipos de documento DIAN vigentes (01, 03, 04, 05, 20, 91, 92, 94, 95, 96…). (nuevo)</summary>
    public IReadOnlyList<CodigoDian> TiposDeDocumento(DateOnly fecha) => Codigos(Catalogos.TiposDeDocumento, fecha);

    /// <summary>
    /// El tipo de documento DIAN de un <see cref="ElectronicDocumentKind"/> a la fecha (contracts/dian.md §4.3): código según la
    /// contingencia (04 si la DIAN no está, 03 al transmitir la de papel), tipo de operación y código único. Nulo si el catálogo no
    /// lo trae. (nuevo)
    /// </summary>
    public TipoDeDocumentoDian? TipoDeDocumento(ElectronicDocumentKind tipo, ContingencyType? contingencia, DateOnly fecha)
    {
        var entrada = Vigente(Catalogos.TiposDeDocumento, fecha)?.PorTipo.FirstOrDefault(t => t.Tipo == tipo);
        if (entrada is null) return null;
        var codigo = contingencia switch
        {
            ContingencyType.Dian04 when !string.IsNullOrWhiteSpace(entrada.CodigoContingenciaDian) => entrada.CodigoContingenciaDian!,
            ContingencyType.Issuer03 when !string.IsNullOrWhiteSpace(entrada.CodigoContingenciaFacturador) => entrada.CodigoContingenciaFacturador!,
            _ => entrada.Codigo,
        };
        return new TipoDeDocumentoDian(tipo, codigo, entrada.TipoDeOperacion, entrada.CodigoUnico, entrada.PorCotejar);
    }

    /// <summary>Los tipos de operación que aplican a <paramref name="tipo"/>. (nuevo)</summary>
    public IReadOnlyList<CodigoDian> TiposDeOperacion(ElectronicDocumentKind tipo, DateOnly fecha) =>
        Vigente(Catalogos.TiposDeOperacion, fecha)?.Codigos.Where(c => c.Tipos.Contains(tipo)).Select(c => c.ComoCodigo()).ToList() ?? [];

    /// <summary>Las responsabilidades fiscales vigentes (O-13, O-15, O-23, O-47, R-99-PN). (nuevo)</summary>
    public IReadOnlyList<CodigoDian> Responsabilidades(DateOnly fecha) => Codigos(Catalogos.Responsabilidades, fecha);

    /// <summary>
    /// Las responsabilidades que corresponden a las marcas tributarias de una persona (T24), en el orden del catálogo; sin
    /// ninguna marca, la de «no aplica». Vacía sin catálogo vigente. (nuevo)
    /// </summary>
    public IReadOnlyList<string> ResponsabilidadesDe(MarcasTributarias marcas, DateOnly fecha)
    {
        ArgumentNullException.ThrowIfNull(marcas);
        var version = Vigente(Catalogos.Responsabilidades, fecha);
        if (version?.Marcas is null) return [];
        var activas = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (marca, codigo) in version.Marcas)
            if (marcas.Tiene(marca)) activas.Add(codigo);
        if (activas.Count == 0)
            return string.IsNullOrWhiteSpace(version.SinResponsabilidad) ? [] : [version.SinResponsabilidad!];
        return version.Codigos.Select(c => c.Codigo).Where(activas.Contains).ToList();
    }

    /// <summary>Los tributos vigentes (01 IVA, 04 INC, 06 retención en la fuente…, ZZ). (nuevo)</summary>
    public IReadOnlyList<CodigoDian> Tributos(DateOnly fecha) => Codigos(Catalogos.Tributos, fecha);

    /// <summary>El tributo de <paramref name="codigo"/> (<c>COR_TaxDefinitions.DianTaxCode</c>) o nulo. (nuevo)</summary>
    public CodigoDian? Tributo(string? codigo, DateOnly fecha) => Buscar(Catalogos.Tributos, codigo, fecha);

    public bool EsTributoValido(string? codigo, DateOnly fecha) => Tributo(codigo, fecha) is not null;

    /// <summary>El esquema tributario de una parte: el del IVA si es responsable, «no aplica» si no. Nulo sin catálogo. (nuevo)</summary>
    public string? EsquemaTributarioDe(bool responsableDeIva, DateOnly fecha)
    {
        var version = Vigente(Catalogos.Tributos, fecha);
        return responsableDeIva ? version?.EsquemaResponsableIva : version?.EsquemaNoResponsableIva;
    }

    /// <summary>El código DIAN del tipo de persona: jurídica o natural. Nulo sin catálogo. (nuevo)</summary>
    public string? TipoDePersonaDe(bool juridica, DateOnly fecha)
    {
        var version = Vigente(Catalogos.TiposDePersona, fecha);
        return juridica ? version?.Juridica : version?.Natural;
    }

    /// <summary>El tipo de identificación de una persona jurídica (el emisor se identifica con NIT). Nulo sin catálogo. (nuevo)</summary>
    public string? IdentificacionDeJuridica(DateOnly fecha) => Vigente(Catalogos.TiposDePersona, fecha)?.IdentificacionDeJuridica;

    /// <summary>El país (ISO 3166) de una parte nacional sin país en su dirección. Nulo sin catálogo. (nuevo)</summary>
    public string? PaisPorDefecto(DateOnly fecha) => Vigente(Catalogos.TiposDePersona, fecha)?.PaisPorDefecto;

    /// <summary>La forma de pago DIAN de contado o de crédito. (nuevo)</summary>
    public CodigoDian? FormaDePago(bool credito, DateOnly fecha)
    {
        var clase = credito ? ClaseDeFormaDePago.Credit : ClaseDeFormaDePago.Cash;
        return Vigente(Catalogos.MediosDePago, fecha)?.FormasDePago.FirstOrDefault(f => f.Clase == clase)?.ComoCodigo();
    }

    /// <summary>Los tipos de caja del DEE POS (por cotejar: la lista puede venir vacía). (nuevo)</summary>
    public IReadOnlyList<CodigoDian> TiposDeCaja(DateOnly fecha) => Codigos(Catalogos.TiposDeCaja, fecha);

    /// <summary>¿Es válido el tipo de caja? Con la lista vigente vacía (tabla por cotejar) acepta cualquier código no vacío. (nuevo)</summary>
    public bool EsTipoDeCajaValido(string? codigo, DateOnly fecha)
    {
        if (string.IsNullOrWhiteSpace(codigo)) return false;
        return TiposDeCaja(fecha).Count == 0 || Buscar(Catalogos.TiposDeCaja, codigo, fecha) is not null;
    }

    /// <summary>¿El código está marcado «por cotejar» en su catálogo vigente? (nuevo)</summary>
    public bool EstaPorCotejar(string catalogo, string codigo, DateOnly fecha) =>
        Vigente(catalogo, fecha)?.Codigos.FirstOrDefault(c => string.Equals(c.Codigo, codigo, StringComparison.OrdinalIgnoreCase))?.PorCotejar ?? false;

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
        public const string TiposDeDocumento = "tiposDeDocumento";
        public const string TiposDeOperacion = "tiposDeOperacion";
        public const string Responsabilidades = "responsabilidades";
        public const string Tributos = "tributos";
        public const string TiposDePersona = "tiposDePersona";
        public const string TiposDeCaja = "tiposDeCaja";
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
        public List<TipoEnArchivo> PorTipo { get; set; } = [];
        public Dictionary<string, string>? Marcas { get; set; }
        public string? SinResponsabilidad { get; set; }
        public string? EsquemaResponsableIva { get; set; }
        public string? EsquemaNoResponsableIva { get; set; }
        public string? Juridica { get; set; }
        public string? Natural { get; set; }
        public string? IdentificacionDeJuridica { get; set; }
        public string? PaisPorDefecto { get; set; }

        public bool CubreA(DateOnly fecha) => fecha >= VigenteDesde && (VigenteHasta is null || fecha <= VigenteHasta);
    }

    private sealed class CodigoEnArchivo
    {
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public bool EsAnulacion { get; set; }
        public List<ClaseDeNotaDian> AplicaA { get; set; } = [];
        public List<ElectronicDocumentKind> Tipos { get; set; } = [];
        public bool PorCotejar { get; set; }
        public ClaseDeFormaDePago? Clase { get; set; }

        public CodigoDian ComoCodigo() => new(Codigo, Nombre);
    }

    private sealed class TipoEnArchivo
    {
        public ElectronicDocumentKind Tipo { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string? CodigoContingenciaDian { get; set; }
        public string? CodigoContingenciaFacturador { get; set; }
        public string TipoDeOperacion { get; set; } = string.Empty;
        public UniqueCodeKind CodigoUnico { get; set; }
        public bool PorCotejar { get; set; }
    }

    private enum ClaseDeFormaDePago
    {
        Cash = 1,
        Credit = 2,
    }
}

/// <summary>
/// El tipo de documento DIAN de un <see cref="ElectronicDocumentKind"/> (contracts/dian.md §4.3): código ya elegido según la
/// contingencia, tipo de operación, código único y si está por cotejar. (nuevo)
/// </summary>
public sealed record TipoDeDocumentoDian(ElectronicDocumentKind Tipo, string Codigo, string TipoDeOperacion, UniqueCodeKind CodigoUnico, bool PorCotejar);

/// <summary>
/// Las marcas tributarias de una persona (decisiones-transversales T24) que se traducen a responsabilidades fiscales; los nombres
/// son los de las columnas de <c>COR_People</c> y de la copia fiscal, los mismos que usa el catálogo. (nuevo)
/// </summary>
public sealed record MarcasTributarias(bool IsLargeContributor, bool IsSelfWithholder, bool IsVatWithholdingAgent, bool IsSimpleTaxRegime)
{
    public bool Tiene(string marca) => marca switch
    {
        nameof(IsLargeContributor) => IsLargeContributor,
        nameof(IsSelfWithholder) => IsSelfWithholder,
        nameof(IsVatWithholdingAgent) => IsVatWithholdingAgent,
        nameof(IsSimpleTaxRegime) => IsSimpleTaxRegime,
        _ => false,
    };
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
