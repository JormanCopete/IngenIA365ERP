using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Risk.Normativa;

/// <summary>Una celda de la política sobre el inicio de una categoría: menos días es más estricto.</summary>
public sealed record InicioDeCategoriaDePolitica(string Segmento, RatingCategory Categoria, int DiasDesde);

/// <summary>
/// Una celda de la política sobre un porcentaje de deterioro: más alto es más estricto. <see cref="CondicionDesde"/> distingue dos
/// porcentajes de la misma categoría y subcategoría (vivienda E2 por meses en E); nulo, la celda tiene que ser única en la norma.
/// </summary>
public sealed record PorcentajeDePolitica(string Segmento, RatingCategory Categoria, RatingSubcategory Subcategoria, int? CondicionDesde, decimal Fraccion);

/// <summary>Una celda de la política sobre un tramo de garantía: una fracción admisible más baja es más estricta.</summary>
public sealed record TramoDeGarantiaDePolitica(NaturalezaDeGarantia Naturaleza, int MesesDesde, decimal FraccionAdmisible);

/// <summary>K6a (T1016): una celda de la política sobre la PI de una calificación del Anexo 2: más alta es más estricta.</summary>
public sealed record PiDePolitica(string Segmento, string TipoDeOrganizacion, RatingCategory Categoria, decimal Fraccion);

/// <summary>K6a: una celda de la política sobre un tramo de PDI del Anexo 2 (por días en incumplimiento): más alta es más estricta.</summary>
public sealed record PdiDePolitica(string TipoDeGarantia, int DiasDesde, decimal Fraccion);

/// <summary>K6a: una celda de la política sobre la PDI sin incumplimiento de un segmento del Anexo 2: más alta es más estricta.</summary>
public sealed record PdiSinIncumplimientoDePolitica(string Segmento, string TipoDeGarantia, decimal Fraccion);

/// <summary>
/// La política SARC del consejo (feature 013, US9, T0690, T0692; T45, R54; data-model §23.10): el <b>esquema parcial</b> del
/// <see cref="Anexo1"/> con sólo las celdas que la cooperativa endurece, más el deterioro general adicional, que la norma no tiene.
/// Es el contenido de <c>LND_RiskPolicyVersions.Overrides</c>; lo combina con la norma vigente <c>PoliticaSarcEfectiva</c>.
///
/// <para>Esquema 1 (camelCase): <c>{ segments: [ { code, categories: [ { category, minDaysPastDue } ], impairmentRates: [ { category,
/// subcategory?, conditionFrom?, rate } ] } ], collateral: [ { nature, brackets: [ { fromMonths, admissibleFraction } ] } ],
/// generalImpairment: { minimumRate?, additionalRate?, additionalCompensatesIndividual? }, expectedLoss?: { probabilityOfDefault: [ { segment,
/// organizationType, rating, rate } ], lossGivenDefault: [ { collateralType, fromDaysAfterDefault, rate } ], performingLossGivenDefault: [ {
/// segment, collateralType, rate } ] } }</c>. <c>expectedLoss</c> (K6a, T1016; data-model §23.10) endurece celdas del <see cref="Anexo2"/> y las combina
/// <c>PoliticaSarcEfectiva.CombinarAnexo2</c>. Todo es opcional; una celda que no
/// existe en la norma es un error de la combinación, no de la lectura (la norma puede cambiar).</para>
///
/// <para>Rutas de celda (las que devuelve <c>PoliticaSarcEfectiva</c> y nombra <c>Lending.RiskPolicy.LessStrict</c>):
/// <c>segments.{segmento}.categories.{categoría}.minDaysPastDue</c>, <c>segments.{segmento}.impairmentRates.{categoría}[.{sub}][.from{n}]</c>,
/// <c>collateral.{naturaleza}.brackets.{desde}.admissibleFraction</c>, <c>generalImpairment.minimumRate</c>,
/// <c>generalImpairment.additionalRate</c>; del Anexo 2, <c>expectedLoss.probabilityOfDefault.{segmento}.{tipo}.{calificación}</c>,
/// <c>expectedLoss.lossGivenDefault.{garantía}.{días}</c> y <c>expectedLoss.performingLossGivenDefault.{segmento}.{garantía}</c>. Sin valores
/// legales: sólo la forma.</para>
/// </summary>
public sealed class PoliticaSarc
{
    public const short VersionDeEsquemaVigente = 1;

    public static IReadOnlyList<short> VersionesDeEsquema { get; } = [1];

    /// <summary>La política vacía: la norma tal cual.</summary>
    public static PoliticaSarc Vacia { get; } = new();

    private PoliticaSarc() { }

    public IReadOnlyList<InicioDeCategoriaDePolitica> InicioDeCategorias { get; private init; } = [];

    public IReadOnlyList<PorcentajeDePolitica> Porcentajes { get; private init; } = [];

    public IReadOnlyList<TramoDeGarantiaDePolitica> Garantias { get; private init; } = [];

    /// <summary>El mínimo del deterioro general que la política sube; nulo, el de la norma.</summary>
    public decimal? DeterioroGeneralMinimo { get; private init; }

    /// <summary>El deterioro general adicional de la cooperativa (FR-104), en fracción de la cartera bruta.</summary>
    public decimal DeterioroGeneralAdicional { get; private init; }

    /// <summary>¿El adicional se usa para compensar el deterioro individual (FR-104)?</summary>
    public bool AdicionalCompensaIndividual { get; private init; }

    /// <summary>K6a: las PI del Anexo 2 que la política sube.</summary>
    public IReadOnlyList<PiDePolitica> ProbabilidadesDeIncumplimiento { get; private init; } = [];

    /// <summary>K6a: los tramos de PDI del Anexo 2 que la política sube.</summary>
    public IReadOnlyList<PdiDePolitica> PerdidasDadoElIncumplimiento { get; private init; } = [];

    /// <summary>K6a: las PDI sin incumplimiento del Anexo 2 que la política sube.</summary>
    public IReadOnlyList<PdiSinIncumplimientoDePolitica> PerdidasSinIncumplimiento { get; private init; } = [];

    public static ResultadoDeDocumento<PoliticaSarc> Leer(string contenido, short versionDeEsquema)
    {
        var errores = new List<ErrorDeDocumentoNormativo>();
        if (!LecturaDeDocumento.VersionSoportada(versionDeEsquema, VersionesDeEsquema, errores)) return new(null, errores);
        using var doc = LecturaDeDocumento.Abrir(contenido, errores);
        if (doc is null) return new(null, errores);
        var raiz = doc.RootElement;

        var inicios = new List<InicioDeCategoriaDePolitica>();
        var porcentajes = new List<PorcentajeDePolitica>();
        if (raiz.TryGetProperty("segments", out _))
        {
            var segmentos = LecturaDeDocumento.Arreglo(raiz, "segments", "$", noVacio: false, errores);
            for (var i = 0; i < segmentos.Count; i++)
                LeerSegmento(segmentos[i], $"$.segments[{i}]", inicios, porcentajes, errores);
        }

        var garantias = new List<TramoDeGarantiaDePolitica>();
        if (raiz.TryGetProperty("collateral", out _))
        {
            var lista = LecturaDeDocumento.Arreglo(raiz, "collateral", "$", noVacio: false, errores);
            for (var i = 0; i < lista.Count; i++)
            {
                var ruta = $"$.collateral[{i}]";
                var texto = LecturaDeDocumento.Texto(lista[i], "nature", ruta, requerido: true, errores);
                if (texto is null || !Enum.TryParse<NaturalezaDeGarantia>(texto, out var naturaleza))
                {
                    errores.Add(new($"{ruta}.nature", "La naturaleza es NonMortgage o Mortgage."));
                    continue;
                }
                var tramos = LecturaDeDocumento.Arreglo(lista[i], "brackets", ruta, noVacio: true, errores);
                for (var j = 0; j < tramos.Count; j++)
                {
                    var r = $"{ruta}.brackets[{j}]";
                    var desde = Anexo1.Entero(tramos[j], "fromMonths", r, requerido: true, errores);
                    var fraccion = Anexo1.Fraccion(tramos[j], "admissibleFraction", r, errores);
                    if (desde is null || fraccion is null) continue;
                    if (garantias.Any(g => g.Naturaleza == naturaleza && g.MesesDesde == desde))
                        errores.Add(new(r, $"El tramo de {naturaleza} desde {desde} meses está repetido."));
                    garantias.Add(new(naturaleza, desde.Value, fraccion.Value));
                }
            }
        }

        decimal? minimo = null;
        var adicional = 0m;
        var compensa = false;
        if (raiz.TryGetProperty("generalImpairment", out var general) && general.ValueKind != JsonValueKind.Null)
        {
            if (general.ValueKind != JsonValueKind.Object) errores.Add(new("$.generalImpairment", "Es un objeto."));
            else
            {
                if (general.TryGetProperty("minimumRate", out _)) minimo = Anexo1.Fraccion(general, "minimumRate", "$.generalImpairment", errores);
                if (general.TryGetProperty("additionalRate", out _))
                    adicional = Anexo1.Fraccion(general, "additionalRate", "$.generalImpairment", errores) ?? 0m;
                compensa = LecturaDeDocumento.Booleano(general, "additionalCompensatesIndividual", "$.generalImpairment", false, errores);
                if (compensa && adicional == 0m)
                    errores.Add(new("$.generalImpairment.additionalCompensatesIndividual", "Sin deterioro adicional no hay con qué compensar."));
            }
        }

        var pi = new List<PiDePolitica>();
        var pdi = new List<PdiDePolitica>();
        var sinIncumplimiento = new List<PdiSinIncumplimientoDePolitica>();
        if (raiz.TryGetProperty("expectedLoss", out var perdida) && perdida.ValueKind != JsonValueKind.Null)
        {
            if (perdida.ValueKind != JsonValueKind.Object) errores.Add(new("$.expectedLoss", "Es un objeto."));
            else LeerPerdidaEsperada(perdida, pi, pdi, sinIncumplimiento, errores);
        }

        return errores.Count > 0
            ? new(null, errores)
            : new(new PoliticaSarc
            {
                InicioDeCategorias = inicios,
                Porcentajes = porcentajes,
                Garantias = garantias,
                DeterioroGeneralMinimo = minimo,
                DeterioroGeneralAdicional = adicional,
                AdicionalCompensaIndividual = compensa,
                ProbabilidadesDeIncumplimiento = pi,
                PerdidasDadoElIncumplimiento = pdi,
                PerdidasSinIncumplimiento = sinIncumplimiento,
            }, errores);
    }

    /// <summary>K6a (T1016): <c>expectedLoss</c>, las celdas del Anexo 2 que la política endurece; todas opcionales y sin repetir.</summary>
    private static void LeerPerdidaEsperada(JsonElement p, List<PiDePolitica> pi, List<PdiDePolitica> pdi, List<PdiSinIncumplimientoDePolitica> sinIncumplimiento,
        List<ErrorDeDocumentoNormativo> errores)
    {
        const string ruta = "$.expectedLoss";
        if (p.TryGetProperty("probabilityOfDefault", out _))
        {
            var lista = LecturaDeDocumento.Arreglo(p, "probabilityOfDefault", ruta, noVacio: false, errores);
            for (var i = 0; i < lista.Count; i++)
            {
                var r = $"{ruta}.probabilityOfDefault[{i}]";
                var segmento = LecturaDeDocumento.Texto(lista[i], "segment", r, requerido: true, errores);
                var tipo = LecturaDeDocumento.Texto(lista[i], "organizationType", r, requerido: true, errores);
                var cat = Anexo1.Categoria(lista[i], "rating", r, errores);
                var tasa = Anexo1.Fraccion(lista[i], "rate", r, errores);
                if (segmento is null || tipo is null || cat is null || tasa is null) continue;
                if (pi.Any(x => x.Segmento == segmento && x.TipoDeOrganizacion == tipo && x.Categoria == cat))
                    errores.Add(new(r, $"La PI de {cat} de {tipo} en {segmento} está repetida."));
                pi.Add(new(segmento, tipo, cat.Value, tasa.Value));
            }
        }
        if (p.TryGetProperty("lossGivenDefault", out _))
        {
            var lista = LecturaDeDocumento.Arreglo(p, "lossGivenDefault", ruta, noVacio: false, errores);
            for (var i = 0; i < lista.Count; i++)
            {
                var r = $"{ruta}.lossGivenDefault[{i}]";
                var tipo = LecturaDeDocumento.Texto(lista[i], "collateralType", r, requerido: true, errores);
                var desde = Anexo1.Entero(lista[i], "fromDaysAfterDefault", r, requerido: true, errores);
                var tasa = Anexo1.Fraccion(lista[i], "rate", r, errores);
                if (tipo is null || desde is null || tasa is null) continue;
                if (pdi.Any(x => x.TipoDeGarantia == tipo && x.DiasDesde == desde)) errores.Add(new(r, $"El tramo de PDI de {tipo} desde {desde} días está repetido."));
                pdi.Add(new(tipo, desde.Value, tasa.Value));
            }
        }
        if (p.TryGetProperty("performingLossGivenDefault", out _))
        {
            var lista = LecturaDeDocumento.Arreglo(p, "performingLossGivenDefault", ruta, noVacio: false, errores);
            for (var i = 0; i < lista.Count; i++)
            {
                var r = $"{ruta}.performingLossGivenDefault[{i}]";
                var segmento = LecturaDeDocumento.Texto(lista[i], "segment", r, requerido: true, errores);
                var tipo = LecturaDeDocumento.Texto(lista[i], "collateralType", r, requerido: true, errores);
                var tasa = Anexo1.Fraccion(lista[i], "rate", r, errores);
                if (segmento is null || tipo is null || tasa is null) continue;
                if (sinIncumplimiento.Any(x => x.Segmento == segmento && x.TipoDeGarantia == tipo))
                    errores.Add(new(r, $"La PDI sin incumplimiento de {tipo} en {segmento} está repetida."));
                sinIncumplimiento.Add(new(segmento, tipo, tasa.Value));
            }
        }
    }

    private static void LeerSegmento(JsonElement s, string ruta, List<InicioDeCategoriaDePolitica> inicios, List<PorcentajeDePolitica> porcentajes,
        List<ErrorDeDocumentoNormativo> errores)
    {
        if (s.ValueKind != JsonValueKind.Object)
        {
            errores.Add(new(ruta, "Cada segmento es un objeto."));
            return;
        }
        var codigo = LecturaDeDocumento.Texto(s, "code", ruta, requerido: true, errores);
        if (codigo is null) return;
        if (!Anexo1.SegmentosConocidos.Contains(codigo, StringComparer.Ordinal))
        {
            errores.Add(new($"{ruta}.code", $"El segmento «{codigo}» no se conoce; se admiten: {string.Join(", ", Anexo1.SegmentosConocidos)}."));
            return;
        }

        if (s.TryGetProperty("categories", out _))
        {
            var lista = LecturaDeDocumento.Arreglo(s, "categories", ruta, noVacio: false, errores);
            for (var i = 0; i < lista.Count; i++)
            {
                var r = $"{ruta}.categories[{i}]";
                var cat = Anexo1.Categoria(lista[i], "category", r, errores);
                var desde = Anexo1.Entero(lista[i], "minDaysPastDue", r, requerido: true, errores);
                if (cat is null || desde is null) continue;
                if (cat == RatingCategory.A) errores.Add(new($"{r}.category", "La categoría A siempre empieza en 0 días: no se endurece."));
                else if (inicios.Any(x => x.Segmento == codigo && x.Categoria == cat)) errores.Add(new(r, $"La categoría {cat} de {codigo} está repetida."));
                else inicios.Add(new(codigo, cat.Value, desde.Value));
            }
        }

        if (s.TryGetProperty("impairmentRates", out _))
        {
            var lista = LecturaDeDocumento.Arreglo(s, "impairmentRates", ruta, noVacio: false, errores);
            for (var i = 0; i < lista.Count; i++)
            {
                var r = $"{ruta}.impairmentRates[{i}]";
                var cat = Anexo1.Categoria(lista[i], "category", r, errores);
                var sub = RatingSubcategory.None;
                var textoSub = LecturaDeDocumento.Texto(lista[i], "subcategory", r, requerido: false, errores);
                if (textoSub is not null && (!Enum.TryParse(textoSub, out sub) || sub == RatingSubcategory.None))
                    errores.Add(new($"{r}.subcategory", "La subcategoría es E1 o E2."));
                var desde = Anexo1.Entero(lista[i], "conditionFrom", r, requerido: false, errores);
                var tasa = Anexo1.Fraccion(lista[i], "rate", r, errores);
                if (cat is null || tasa is null) continue;
                if (porcentajes.Any(p => p.Segmento == codigo && p.Categoria == cat && p.Subcategoria == sub && p.CondicionDesde == desde))
                    errores.Add(new(r, $"El porcentaje de {cat} {sub} de {codigo} está repetido."));
                porcentajes.Add(new(codigo, cat.Value, sub, desde, tasa.Value));
            }
        }
    }

    // ------------------------------------------------------------------------------------------------- rutas --

    internal static string RutaDeInicio(string segmento, RatingCategory categoria) => $"segments.{segmento}.categories.{categoria}.minDaysPastDue";

    internal static string RutaDePorcentaje(string segmento, PorcentajeDeDeterioro p) =>
        $"segments.{segmento}.impairmentRates.{p.Categoria}"
        + (p.Subcategoria == RatingSubcategory.None ? string.Empty : "." + p.Subcategoria)
        + (p.Condicion is { } c ? ".from" + c.Desde.ToString(CultureInfo.InvariantCulture) : string.Empty);

    internal static string RutaDeTramo(NaturalezaDeGarantia naturaleza, int desde) =>
        $"collateral.{naturaleza}.brackets.{desde.ToString(CultureInfo.InvariantCulture)}.admissibleFraction";

    internal const string RutaDelMinimoGeneral = "generalImpairment.minimumRate";

    internal const string RutaDelAdicionalGeneral = "generalImpairment.additionalRate";

    internal static string RutaDePi(string segmento, string tipo, RatingCategory categoria) => $"expectedLoss.probabilityOfDefault.{segmento}.{tipo}.{categoria}";

    internal static string RutaDePdi(string tipo, int desde) =>
        $"expectedLoss.lossGivenDefault.{tipo}.{desde.ToString(CultureInfo.InvariantCulture)}";

    internal static string RutaDePdiSinIncumplimiento(string segmento, string tipo) => $"expectedLoss.performingLossGivenDefault.{segmento}.{tipo}";
}
