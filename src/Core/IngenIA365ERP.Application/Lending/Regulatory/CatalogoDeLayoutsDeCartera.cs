using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Common.Layouts;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;

namespace IngenIA365ERP.Application.Lending.Regulatory;

/// <summary>
/// Un layout regulatorio de Cartera (feature 013, K5, US16; adaptadores-y-archivos.md §7.2–§7.3; T79): el layout genérico del motor más lo
/// que sólo le importa al catálogo —la clase de archivo, la central, quién lo cotejó y contra qué, y sus notas—. Un layout con campos sin
/// cotejar <b>es vigente</b> (precedente D-43 de la PILA): cada generación con él lleva la advertencia <c>Lending.Regulatory.LayoutSinCotejar</c>.
/// </summary>
public sealed record LayoutDeCartera(
    string Resource,
    RegulatoryFileKind Kind,
    string? BureauCode,
    LayoutDeArchivo Layout,
    string? VerifiedBy,
    DateOnly? VerifiedOn,
    string? VerifiedAgainst,
    IReadOnlyList<string> Notes)
{
    public string Code => Layout.Code;

    /// <summary>
    /// Feature 014, US17 (T220): el formato de captaciones (<c>sicses-captaciones</c>, <c>exogena-1019</c>…) en <c>DepositsSicses</c> y <c>Exogenous</c>; nulo
    /// en el SICSES de cartera y en las centrales.
    /// </summary>
    public string? FormatCode { get; init; }
}

/// <summary>Una fila de <c>GET /api/lending/regulatory/layouts</c>: el layout con su estado de cotejo (campos cotejados sobre el total).</summary>
public sealed record RegulatoryLayoutDto(
    string Code,
    RegulatoryFileKind Kind,
    string? BureauCode,
    string Version,
    string Source,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string FileKind,
    int TotalFields,
    int VerifiedFields,
    int UnverifiedFields,
    string? VerifiedBy,
    DateOnly? VerifiedOn,
    string? VerifiedAgainst,
    IReadOnlyList<string> Notes,
    string? FormatCode = null);

/// <summary>
/// Los layouts del SICSES y de las centrales de riesgo embebidos en el ensamblado (<c>Lending/Regulatory/Layouts/*.json</c>; feature 013, K5,
/// US16, T0999; molde <c>PilaLayoutCatalog</c>). Se cargan y se validan <b>al arrancar</b> —la composición de Application lo pide al registrarse,
/// así un JSON mal formado impide arrancar—: el motor revisa cada definición (<see cref="LectorDeLayout"/>) y el catálogo, lo suyo: la clase,
/// la central (obligatoria en <c>CreditBureau</c>, prohibida en <c>Sicses</c>, en mayúsculas y de hasta 20), que el nombre del archivo
/// corresponda (<c>sicses-cartera-*</c>, <c>central-{CODIGO}-*</c>), códigos únicos de hasta 80 y que dos layouts de la misma clase y central
/// no se crucen en vigencia. Una versión nueva del instructivo es un archivo nuevo con su <c>validFrom</c>, nunca lógica. (nuevo)
/// </summary>
public sealed class CatalogoDeLayoutsDeCartera
{
    private const string Carpeta = ".Lending.Regulatory.Layouts.";
    private static readonly Regex CodigoDeCentral = new("^[A-Z0-9_]{1,20}$", RegexOptions.Compiled);
    private static readonly Lazy<CatalogoDeLayoutsDeCartera> DelEnsamblado = new(CargarEmbebidos);

    /// <summary>El catálogo de los JSON embebidos; la primera lectura los carga y valida (y lanza si alguno no sirve).</summary>
    public static CatalogoDeLayoutsDeCartera Embebido => DelEnsamblado.Value;

    public IReadOnlyList<LayoutDeCartera> Todos { get; }

    private CatalogoDeLayoutsDeCartera(IReadOnlyList<LayoutDeCartera> todos) => Todos = todos;

    /// <summary>Las centrales con algún layout, en orden.</summary>
    public IReadOnlyList<string> Centrales =>
        Todos.Where(l => l.BureauCode is not null).Select(l => l.BureauCode!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

    /// <summary>El layout vigente para la clase (y central) a la fecha de fin del período, o nulo si ninguno lo cubre.</summary>
    public LayoutDeCartera? Vigente(RegulatoryFileKind clase, string? central, DateOnly finDelPeriodo) =>
        Todos.Where(l => l.Kind == clase && MismaCentral(l.BureauCode, central) && l.Layout.IsValidAt(finDelPeriodo))
            .OrderByDescending(l => l.Layout.ValidFrom).FirstOrDefault();

    /// <summary>
    /// Feature 014, US17 (T220): el layout vigente del formato de captaciones a la fecha de fin del período (el último día del mes en el SICSES, el 31 de
    /// diciembre en la exógena), o nulo si ninguno lo cubre.
    /// </summary>
    public LayoutDeCartera? VigenteDelFormato(string formato, DateOnly finDelPeriodo) =>
        Todos.Where(l => string.Equals(l.FormatCode, formato, StringComparison.OrdinalIgnoreCase) && l.Layout.IsValidAt(finDelPeriodo))
            .OrderByDescending(l => l.Layout.ValidFrom).FirstOrDefault();

    /// <summary>Los formatos de captaciones con algún layout, en orden.</summary>
    public IReadOnlyList<string> Formatos =>
        Todos.Where(l => l.FormatCode is not null).Select(l => l.FormatCode!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

    public LayoutDeCartera? PorCodigo(string codigo) =>
        Todos.FirstOrDefault(l => string.Equals(l.Code, codigo, StringComparison.OrdinalIgnoreCase));

    /// <summary>¿Hay algún layout de esa central (vigente o no)? Para responder <c>Lending.Regulatory.BureauUnknown</c>.</summary>
    public bool ConoceLaCentral(string central) => Todos.Any(l => MismaCentral(l.BureauCode, central));

    private static bool MismaCentral(string? a, string? b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Arma un catálogo desde pares (nombre del archivo, JSON). Lanza <see cref="FormatException"/> con todos los problemas juntos si un JSON no
    /// se lee, una definición no es válida o dos layouts se cruzan.
    /// </summary>
    public static CatalogoDeLayoutsDeCartera Cargar(IEnumerable<(string Nombre, string Json)> archivos)
    {
        var errores = new List<string>();
        var lista = new List<LayoutDeCartera>();
        foreach (var (nombre, json) in archivos)
        {
            try { lista.Add(Leer(nombre, json)); }
            catch (FormatException e) { errores.Add(e.Message); }
        }

        foreach (var repetido in lista.GroupBy(l => l.Code, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            errores.Add($"El código de layout {repetido.Key} está en {string.Join(" y ", repetido.Select(l => l.Resource))}.");

        foreach (var grupo in lista.GroupBy(l => (l.Kind, Central: l.BureauCode?.ToUpperInvariant(), Formato: l.FormatCode?.ToLowerInvariant())))
        {
            var ordenados = grupo.OrderBy(l => l.Layout.ValidFrom).ToList();
            for (var i = 1; i < ordenados.Count; i++)
            {
                var anterior = ordenados[i - 1];
                var siguiente = ordenados[i];
                if (anterior.Layout.ValidTo is null || anterior.Layout.ValidTo >= siguiente.Layout.ValidFrom)
                    errores.Add($"Los layouts {anterior.Code} y {siguiente.Code} ({grupo.Key.Kind}{(grupo.Key.Central is null ? "" : " " + grupo.Key.Central)}" +
                                $"{(grupo.Key.Formato is null ? "" : " " + grupo.Key.Formato)}) " +
                                "se cruzan en vigencia: cierre el anterior la víspera del siguiente.");
            }
        }

        if (errores.Count > 0) throw new FormatException("Los layouts regulatorios de Cartera no se pueden cargar: " + string.Join(" ", errores));
        return new CatalogoDeLayoutsDeCartera(lista.OrderBy(l => l.Kind).ThenBy(l => l.BureauCode, StringComparer.Ordinal).ThenBy(l => l.Layout.ValidFrom).ToList());
    }

    private static LayoutDeCartera Leer(string nombre, string json)
    {
        var layout = LectorDeLayout.Leer(json, nombre);
        CatalogoJson extra;
        try { extra = JsonSerializer.Deserialize<CatalogoJson>(json, Opciones) ?? throw new FormatException($"El layout {nombre} está vacío."); }
        catch (JsonException e) { throw new FormatException($"El layout {nombre} no es un JSON válido: {e.Message}", e); }

        if (extra.Kind is not { } clase || !Enum.IsDefined(clase))
            throw new FormatException($"El layout {nombre} no dice su «kind» (Sicses, CreditBureau, DepositsSicses o Exogenous).");
        if (layout.Code.Length > 80) throw new FormatException($"El layout {nombre}: el código {layout.Code} pasa de 80 caracteres.");

        var archivo = NombreDeArchivo(nombre);
        if (clase is RegulatoryFileKind.DepositsSicses or RegulatoryFileKind.Exogenous)
        {
            // Feature 014, US17 (T220): un formato de captaciones dice su código, en minúsculas, con el prefijo de su clase; el archivo empieza por él.
            var prefijo = clase == RegulatoryFileKind.DepositsSicses ? "sicses-" : "exogena-";
            if (extra.BureauCode is not null) throw new FormatException($"El layout {nombre} es de captaciones y no lleva «bureauCode».");
            if (extra.FormatCode is not { Length: > 0 } formato || formato != formato.ToLowerInvariant() || !formato.StartsWith(prefijo, StringComparison.Ordinal)
                || formato.Length > 40 || formato == "sicses-cartera")
                throw new FormatException($"El layout {nombre} necesita «formatCode» en minúsculas que empiece por «{prefijo}» (de hasta 40 caracteres).");
            if (!archivo.StartsWith(formato + "-", StringComparison.OrdinalIgnoreCase))
                throw new FormatException($"El layout {nombre} es del formato {formato} y su archivo debe llamarse {formato}-{{versión}}-{{aaaa-mm-dd}}.json.");
            return new LayoutDeCartera(nombre, clase, null, layout, extra.VerifiedBy, extra.VerifiedOn, extra.VerifiedAgainst, extra.Notes ?? [])
            {
                FormatCode = formato,
            };
        }
        if (extra.FormatCode is not null) throw new FormatException($"El layout {nombre} no es de captaciones y no lleva «formatCode».");
        if (clase == RegulatoryFileKind.Sicses)
        {
            if (extra.BureauCode is not null) throw new FormatException($"El layout {nombre} es del SICSES y no lleva «bureauCode».");
            if (!archivo.StartsWith("sicses-cartera-", StringComparison.OrdinalIgnoreCase))
                throw new FormatException($"El layout {nombre} es del SICSES y su archivo debe llamarse sicses-cartera-{{versión}}-{{aaaa-mm-dd}}.json.");
        }
        else
        {
            if (extra.BureauCode is null || !CodigoDeCentral.IsMatch(extra.BureauCode))
                throw new FormatException($"El layout {nombre} es de una central y necesita «bureauCode» en mayúsculas, de hasta 20 caracteres.");
            if (!archivo.StartsWith($"central-{extra.BureauCode}-", StringComparison.OrdinalIgnoreCase))
                throw new FormatException($"El layout {nombre} es de {extra.BureauCode} y su archivo debe llamarse central-{extra.BureauCode}-{{versión}}-{{aaaa-mm-dd}}.json.");
        }
        return new LayoutDeCartera(nombre, clase, extra.BureauCode, layout, extra.VerifiedBy, extra.VerifiedOn, extra.VerifiedAgainst, extra.Notes ?? []);
    }

    /// <summary>De <c>IngenIA365ERP.Application.Lending.Regulatory.Layouts.central-X-1-2026-12-01.json</c> deja <c>central-X-1-2026-12-01.json</c>.</summary>
    private static string NombreDeArchivo(string nombre)
    {
        var i = nombre.IndexOf(Carpeta, StringComparison.Ordinal);
        return i >= 0 ? nombre[(i + Carpeta.Length)..] : Path.GetFileName(nombre);
    }

    private static CatalogoDeLayoutsDeCartera CargarEmbebidos()
    {
        var asm = Assembly.GetExecutingAssembly();
        var archivos = asm.GetManifestResourceNames()
            .Where(n => n.Contains(Carpeta, StringComparison.Ordinal) && n.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)
            .Select(n =>
            {
                using var stream = asm.GetManifestResourceStream(n)!;
                using var lector = new StreamReader(stream);
                return (n, lector.ReadToEnd());
            })
            .ToList();
        return Cargar(archivos);
    }

    private static readonly JsonSerializerOptions Opciones = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(allowIntegerValues: false) },
    };

    private sealed class CatalogoJson
    {
        public RegulatoryFileKind? Kind { get; set; }
        public string? BureauCode { get; set; }
        public string? FormatCode { get; set; }
        public string? VerifiedBy { get; set; }
        public DateOnly? VerifiedOn { get; set; }
        public string? VerifiedAgainst { get; set; }
        public List<string>? Notes { get; set; }
    }
}

/// <summary>
/// <c>GET /api/lending/regulatory/layouts</c> (feature 013, K5, US16; adaptadores-y-archivos.md §7.2; <c>Lending.RegulatoryFiles.Read</c>, C85: la
/// ruta llega con el bloque de API): los layouts regulatorios con su vigencia, su fuente y su estado de cotejo. (nuevo)
/// </summary>
public sealed record ListRegulatoryLayoutsQuery : IRequest<Result<IReadOnlyList<RegulatoryLayoutDto>>>;

public sealed class ListRegulatoryLayoutsQueryHandler(CatalogoDeLayoutsDeCartera catalogo)
    : IRequestHandler<ListRegulatoryLayoutsQuery, Result<IReadOnlyList<RegulatoryLayoutDto>>>
{
    public Task<Result<IReadOnlyList<RegulatoryLayoutDto>>> Handle(ListRegulatoryLayoutsQuery request, CancellationToken ct)
    {
        IReadOnlyList<RegulatoryLayoutDto> filas = catalogo.Todos.Select(l => new RegulatoryLayoutDto(l.Code, l.Kind, l.BureauCode, l.Layout.Version,
            l.Layout.Source, l.Layout.ValidFrom, l.Layout.ValidTo, l.Layout.FileKind.ToString(), l.Layout.TotalFields, l.Layout.VerifiedFields,
            l.Layout.UnverifiedFields, l.VerifiedBy, l.VerifiedOn, l.VerifiedAgainst, l.Notes, l.FormatCode)).ToList();
        return Task.FromResult(Result.Success(filas));
    }
}
