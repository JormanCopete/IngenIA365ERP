using System.Globalization;
using System.Text.Json;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Risk.Normativa;

/// <summary>Sobre qué base se aplica un porcentaje de deterioro: lo descubierto (saldo − garantía admisible − aportes) o el saldo total.</summary>
public enum BaseDeDeterioro
{
    Uncovered = 1,
    TotalBalance = 2,
}

/// <summary>El criterio de una condición de porcentaje o de subcategoría: días de mora o meses en la categoría E.</summary>
public enum CriterioDeCondicion
{
    DaysPastDue = 1,
    MonthsInE = 2,
}

/// <summary>Naturaleza de la garantía para la tabla de admisibilidad por meses de mora.</summary>
public enum NaturalezaDeGarantia
{
    NonMortgage = 1,
    Mortgage = 2,
}

/// <summary>Un rango cerrado de un criterio; <see cref="Hasta"/> nulo = sin tope.</summary>
public sealed record RangoDeCondicion(CriterioDeCondicion Criterio, int Desde, int? Hasta)
{
    public bool Contiene(int valor) => valor >= Desde && (Hasta is null || valor <= Hasta);

    public bool SeCruzaCon(RangoDeCondicion otro) =>
        Criterio == otro.Criterio && Desde <= (otro.Hasta ?? int.MaxValue) && otro.Desde <= (Hasta ?? int.MaxValue);
}

/// <summary>El rango de días de mora de una categoría en un segmento SARC.</summary>
public sealed record RangoDeCategoria(RatingCategory Categoria, int DiasDesde, int? DiasHasta);

/// <summary>Un porcentaje de deterioro de una categoría (y subcategoría) con su condición opcional y su base.</summary>
public sealed record PorcentajeDeDeterioro(RatingCategory Categoria, RatingSubcategory Subcategoria, RangoDeCondicion? Condicion,
    BaseDeDeterioro Base, decimal Fraccion);

/// <summary>Un segmento SARC del Anexo 1 con sus categorías, porcentajes y la categoría desde la que se suspende la causación.</summary>
public sealed record SegmentoSarc(string Codigo, string Nombre, IReadOnlyList<RangoDeCategoria> Categorias,
    IReadOnlyList<PorcentajeDeDeterioro> Porcentajes, RatingCategory CategoriaDeSuspension);

/// <summary>Un tramo de la tabla de garantías: meses de mora y la fracción del valor de la garantía que se admite.</summary>
public sealed record TramoDeGarantia(int MesesDesde, int? MesesHasta, decimal FraccionAdmisible);

/// <summary>La tabla de admisibilidad de una naturaleza de garantía.</summary>
public sealed record GarantiaPorMeses(NaturalezaDeGarantia Naturaleza, IReadOnlyList<TramoDeGarantia> Tramos);

/// <summary>Regla especial: un crédito de pago único pasa a <see cref="Categoria"/> desde <see cref="DiasDeMoraDesde"/>.</summary>
public sealed record ReglaDePagoUnico(RatingCategory Categoria, int DiasDeMoraDesde);

/// <summary>El arrastre: desde qué categoría, si se mide dentro del mismo segmento y si alcanza las codeudas.</summary>
public sealed record ReglaDeArrastre(RatingCategory DesdeCategoria, bool DentroDelSegmento, bool IncluyeCodeudas);

/// <summary>Los aportes como deducción del deterioro y sus condiciones.</summary>
public sealed record ReglaDeAportes(bool Deducibles, bool ExigeSinPerdidas, bool ExigeSolvenciaMinima);

/// <summary>Reestructurados: categoría mínima al reestructurar y cuotas pagadas seguidas para mejorar.</summary>
public sealed record ReglaDeReestructurados(RatingCategory CategoriaMinima, int CuotasPagadasParaMejorar);

/// <summary>Deterioro general mínimo de un perfil.</summary>
public sealed record DeterioroGeneralDePerfil(string Perfil, decimal FraccionMinima);

/// <summary>
/// Documento normativo <see cref="RegulatoryDocumentKind.SarcAnexo1"/> tipado (feature 013, T45, R2, R54, T0204; data-model parte 3
/// §12.4): segmentos SARC, rangos de mora por categoría, porcentajes de deterioro por categoría y subcategoría con su condición tipada
/// (días de mora o meses en E) y su base, garantías por naturaleza con tramos en meses, aportes, arrastre, pago único, suspensión
/// por segmento, reestructurados y deterioro general por perfil. <b>Sólo forma y reglas de forma</b>: los motores que lo usan
/// (<c>MotorDeCalificacion</c>, <c>MotorDeDeterioroIndividual</c>) son de US9. Los valores viven en el documento con vigencia, nunca
/// en el código (<c>LaCarteraNoTieneValoresLegalesFijos</c>).
///
/// <para>Esquema 1 (camelCase): <c>{ segments: [ { code, name, categories: [ { category, minDaysPastDue, maxDaysPastDue? } ],
/// impairmentRates: [ { category, subcategory?, condition?: { kind, from, to? }, base, rate } ], accrualSuspensionCategory } ],
/// singleInstallment: [ { category, minDaysPastDue } ], drag: { fromCategory, withinSegment, includesCoDebts },
/// contributions: { deductible, requiresNoLosses, requiresMinimumSolvency }, collateral: [ { nature, brackets: [ { fromMonths,
/// toMonths?, admissibleFraction } ] } ], restructured: { minimumCategory, consecutivePaidInstallmentsToImprove },
/// generalImpairment: [ { profile, minimumRate } ] }</c>.</para>
///
/// <para>Reglas de forma: segmentos conocidos (<see cref="SegmentosConocidos"/>) sin repetir; en cada uno las cinco categorías A–E
/// en orden, la primera desde 0, cada una empieza el día siguiente a la anterior y sólo la última abierta (sin rangos cruzados ni
/// huecos); fracciones entre 0 y 1; subcategoría E1/E2 sólo en E; dos porcentajes de la misma categoría y subcategoría no se
/// cruzan; tramos de garantía ordenados desde 0, sin cruces y con sólo el último abierto.</para>
/// </summary>
public sealed class Anexo1
{
    public const short VersionDeEsquemaVigente = 1;

    public static IReadOnlyList<short> VersionesDeEsquema { get; } = [1];

    /// <summary>Los segmentos SARC (data-model parte 3 §12.4).</summary>
    public static IReadOnlyList<string> SegmentosConocidos { get; } = ["VIVIENDA", "CONSUMO", "MICROCREDITO", "COMERCIAL_PN", "COMERCIAL_PJ"];

    private Anexo1() { }

    public IReadOnlyList<SegmentoSarc> Segmentos { get; private init; } = [];

    public IReadOnlyList<ReglaDePagoUnico> PagoUnico { get; private init; } = [];

    public ReglaDeArrastre Arrastre { get; private init; } = null!;

    public ReglaDeAportes Aportes { get; private init; } = null!;

    public IReadOnlyList<GarantiaPorMeses> Garantias { get; private init; } = [];

    public ReglaDeReestructurados Reestructurados { get; private init; } = null!;

    public IReadOnlyList<DeterioroGeneralDePerfil> DeterioroGeneral { get; private init; } = [];

    public SegmentoSarc? Segmento(string codigo) => Segmentos.FirstOrDefault(s => s.Codigo == codigo);

    public static ResultadoDeDocumento<Anexo1> Leer(string contenido, short versionDeEsquema)
    {
        var errores = new List<ErrorDeDocumentoNormativo>();
        if (!LecturaDeDocumento.VersionSoportada(versionDeEsquema, VersionesDeEsquema, errores)) return new(null, errores);
        using var doc = LecturaDeDocumento.Abrir(contenido, errores);
        if (doc is null) return new(null, errores);
        var raiz = doc.RootElement;

        var segmentos = new List<SegmentoSarc>();
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var lista = LecturaDeDocumento.Arreglo(raiz, "segments", "$", noVacio: true, errores);
        for (var i = 0; i < lista.Count; i++)
        {
            var s = LeerSegmento(lista[i], $"$.segments[{i}]", errores);
            if (s is null) continue;
            if (!vistos.Add(s.Codigo)) errores.Add(new($"$.segments[{i}].code", $"El segmento {s.Codigo} está repetido."));
            segmentos.Add(s);
        }

        var pagoUnico = new List<ReglaDePagoUnico>();
        var reglas = LecturaDeDocumento.Arreglo(raiz, "singleInstallment", "$", noVacio: false, errores);
        for (var i = 0; i < reglas.Count; i++)
        {
            var ruta = $"$.singleInstallment[{i}]";
            var cat = Categoria(reglas[i], "category", ruta, errores);
            var dias = Entero(reglas[i], "minDaysPastDue", ruta, requerido: true, errores) ?? 0;
            if (cat is not null) pagoUnico.Add(new(cat.Value, dias));
        }

        var arrastre = Objeto(raiz, "drag", "$", errores);
        var reglaDeArrastre = arrastre is { } a
            ? new ReglaDeArrastre(Categoria(a, "fromCategory", "$.drag", errores) ?? RatingCategory.B,
                LecturaDeDocumento.Booleano(a, "withinSegment", "$.drag", true, errores),
                LecturaDeDocumento.Booleano(a, "includesCoDebts", "$.drag", false, errores))
            : null;

        var aportes = Objeto(raiz, "contributions", "$", errores);
        var reglaDeAportes = aportes is { } ap
            ? new ReglaDeAportes(LecturaDeDocumento.Booleano(ap, "deductible", "$.contributions", false, errores),
                LecturaDeDocumento.Booleano(ap, "requiresNoLosses", "$.contributions", false, errores),
                LecturaDeDocumento.Booleano(ap, "requiresMinimumSolvency", "$.contributions", false, errores))
            : null;

        var garantias = new List<GarantiaPorMeses>();
        var listaGarantias = LecturaDeDocumento.Arreglo(raiz, "collateral", "$", noVacio: false, errores);
        var naturalezas = new HashSet<NaturalezaDeGarantia>();
        for (var i = 0; i < listaGarantias.Count; i++)
        {
            var g = LeerGarantia(listaGarantias[i], $"$.collateral[{i}]", errores);
            if (g is null) continue;
            if (!naturalezas.Add(g.Naturaleza)) errores.Add(new($"$.collateral[{i}].nature", $"La naturaleza {g.Naturaleza} está repetida."));
            garantias.Add(g);
        }

        var reestructurados = Objeto(raiz, "restructured", "$", errores);
        var reglaDeReestructurados = reestructurados is { } r
            ? new ReglaDeReestructurados(Categoria(r, "minimumCategory", "$.restructured", errores) ?? RatingCategory.B,
                Entero(r, "consecutivePaidInstallmentsToImprove", "$.restructured", requerido: true, errores) ?? 0)
            : null;

        var general = new List<DeterioroGeneralDePerfil>();
        var perfiles = LecturaDeDocumento.Arreglo(raiz, "generalImpairment", "$", noVacio: true, errores);
        for (var i = 0; i < perfiles.Count; i++)
        {
            var ruta = $"$.generalImpairment[{i}]";
            var perfil = LecturaDeDocumento.Texto(perfiles[i], "profile", ruta, requerido: true, errores);
            var tasa = Fraccion(perfiles[i], "minimumRate", ruta, errores);
            if (perfil is not null && tasa is not null) general.Add(new(perfil, tasa.Value));
        }

        if (errores.Count > 0 || reglaDeArrastre is null || reglaDeAportes is null || reglaDeReestructurados is null) return new(null, errores);
        return new(new Anexo1
        {
            Segmentos = segmentos,
            PagoUnico = pagoUnico,
            Arrastre = reglaDeArrastre,
            Aportes = reglaDeAportes,
            Garantias = garantias,
            Reestructurados = reglaDeReestructurados,
            DeterioroGeneral = general,
        }, errores);
    }

    private static SegmentoSarc? LeerSegmento(JsonElement s, string ruta, List<ErrorDeDocumentoNormativo> errores)
    {
        if (s.ValueKind != JsonValueKind.Object)
        {
            errores.Add(new(ruta, "Cada segmento es un objeto."));
            return null;
        }
        var codigo = LecturaDeDocumento.Texto(s, "code", ruta, requerido: true, errores);
        if (codigo is not null && !SegmentosConocidos.Contains(codigo, StringComparer.Ordinal))
            errores.Add(new($"{ruta}.code", $"El segmento «{codigo}» no se conoce; se admiten: {string.Join(", ", SegmentosConocidos)}."));
        var nombre = LecturaDeDocumento.Texto(s, "name", ruta, requerido: true, errores);

        var categorias = new List<RangoDeCategoria>();
        var lista = LecturaDeDocumento.Arreglo(s, "categories", ruta, noVacio: true, errores);
        for (var i = 0; i < lista.Count; i++)
        {
            var r = $"{ruta}.categories[{i}]";
            var cat = Categoria(lista[i], "category", r, errores);
            var desde = Entero(lista[i], "minDaysPastDue", r, requerido: true, errores);
            var hasta = Entero(lista[i], "maxDaysPastDue", r, requerido: false, errores);
            if (cat is not null && desde is not null) categorias.Add(new(cat.Value, desde.Value, hasta));
        }
        RevisarCategorias(categorias, $"{ruta}.categories", errores);

        var porcentajes = new List<PorcentajeDeDeterioro>();
        var tasas = LecturaDeDocumento.Arreglo(s, "impairmentRates", ruta, noVacio: true, errores);
        for (var i = 0; i < tasas.Count; i++)
        {
            var p = LeerPorcentaje(tasas[i], $"{ruta}.impairmentRates[{i}]", errores);
            if (p is null) continue;
            var cruzado = porcentajes.FirstOrDefault(o => o.Categoria == p.Categoria && o.Subcategoria == p.Subcategoria
                && (o.Condicion is null || p.Condicion is null || o.Condicion.SeCruzaCon(p.Condicion)));
            if (cruzado is not null)
                errores.Add(new($"{ruta}.impairmentRates[{i}]", $"El porcentaje de {p.Categoria} {p.Subcategoria} se cruza con otro de la misma categoría."));
            porcentajes.Add(p);
        }

        var suspension = Categoria(s, "accrualSuspensionCategory", ruta, errores);
        return codigo is null || nombre is null || suspension is null
            ? null
            : new SegmentoSarc(codigo, nombre, categorias, porcentajes, suspension.Value);
    }

    private static void RevisarCategorias(List<RangoDeCategoria> categorias, string ruta, List<ErrorDeDocumentoNormativo> errores)
    {
        var esperadas = Enum.GetValues<RatingCategory>();
        if (!categorias.Select(c => c.Categoria).SequenceEqual(esperadas))
        {
            errores.Add(new(ruta, "Las categorías van una vez cada una y en orden: A, B, C, D, E."));
            return;
        }
        if (categorias[0].DiasDesde != 0) errores.Add(new($"{ruta}[0].minDaysPastDue", "La categoría A empieza en 0 días."));
        for (var i = 0; i < categorias.Count; i++)
        {
            var c = categorias[i];
            var ultima = i == categorias.Count - 1;
            if (ultima && c.DiasHasta is not null) errores.Add(new($"{ruta}[{i}].maxDaysPastDue", "La última categoría no tiene tope."));
            if (!ultima && c.DiasHasta is null) errores.Add(new($"{ruta}[{i}].maxDaysPastDue", "Sólo la última categoría va sin tope."));
            if (c.DiasHasta is { } h && h < c.DiasDesde) errores.Add(new($"{ruta}[{i}]", "El rango termina antes de empezar."));
            if (i > 0 && categorias[i - 1].DiasHasta is { } anterior && c.DiasDesde != anterior + 1)
                errores.Add(new($"{ruta}[{i}].minDaysPastDue",
                    c.DiasDesde <= anterior ? "El rango se cruza con el de la categoría anterior." : "Hay un hueco con la categoría anterior."));
        }
    }

    private static PorcentajeDeDeterioro? LeerPorcentaje(JsonElement p, string ruta, List<ErrorDeDocumentoNormativo> errores)
    {
        if (p.ValueKind != JsonValueKind.Object)
        {
            errores.Add(new(ruta, "Cada porcentaje es un objeto."));
            return null;
        }
        var cat = Categoria(p, "category", ruta, errores);
        var sub = RatingSubcategory.None;
        var textoSub = LecturaDeDocumento.Texto(p, "subcategory", ruta, requerido: false, errores);
        if (textoSub is not null && (!Enum.TryParse(textoSub, out sub) || sub == RatingSubcategory.None))
            errores.Add(new($"{ruta}.subcategory", "La subcategoría es E1 o E2."));
        if (sub != RatingSubcategory.None && cat is not RatingCategory.E)
            errores.Add(new($"{ruta}.subcategory", "Sólo la categoría E tiene subcategorías."));

        RangoDeCondicion? condicion = null;
        if (p.TryGetProperty("condition", out var c) && c.ValueKind != JsonValueKind.Null)
            condicion = LeerCondicion(c, $"{ruta}.condition", errores);

        var textoBase = LecturaDeDocumento.Texto(p, "base", ruta, requerido: true, errores);
        var baseDeDeterioro = BaseDeDeterioro.Uncovered;
        if (textoBase is not null && !Enum.TryParse(textoBase, out baseDeDeterioro))
            errores.Add(new($"{ruta}.base", "La base es Uncovered o TotalBalance."));
        var tasa = Fraccion(p, "rate", ruta, errores);
        return cat is null || tasa is null ? null : new(cat.Value, sub, condicion, baseDeDeterioro, tasa.Value);
    }

    private static RangoDeCondicion? LeerCondicion(JsonElement c, string ruta, List<ErrorDeDocumentoNormativo> errores)
    {
        if (c.ValueKind != JsonValueKind.Object)
        {
            errores.Add(new(ruta, "La condición es un objeto."));
            return null;
        }
        var textoCriterio = LecturaDeDocumento.Texto(c, "kind", ruta, requerido: true, errores);
        if (textoCriterio is null || !Enum.TryParse<CriterioDeCondicion>(textoCriterio, out var criterio))
        {
            errores.Add(new($"{ruta}.kind", "El criterio es DaysPastDue o MonthsInE."));
            return null;
        }
        var desde = Entero(c, "from", ruta, requerido: true, errores);
        var hasta = Entero(c, "to", ruta, requerido: false, errores);
        if (desde is null) return null;
        if (hasta is { } h && h < desde) errores.Add(new(ruta, "La condición termina antes de empezar."));
        return new(criterio, desde.Value, hasta);
    }

    private static GarantiaPorMeses? LeerGarantia(JsonElement g, string ruta, List<ErrorDeDocumentoNormativo> errores)
    {
        if (g.ValueKind != JsonValueKind.Object)
        {
            errores.Add(new(ruta, "Cada garantía es un objeto."));
            return null;
        }
        var texto = LecturaDeDocumento.Texto(g, "nature", ruta, requerido: true, errores);
        if (texto is null || !Enum.TryParse<NaturalezaDeGarantia>(texto, out var naturaleza))
        {
            errores.Add(new($"{ruta}.nature", "La naturaleza es NonMortgage o Mortgage."));
            return null;
        }
        var tramos = new List<TramoDeGarantia>();
        var lista = LecturaDeDocumento.Arreglo(g, "brackets", ruta, noVacio: true, errores);
        for (var i = 0; i < lista.Count; i++)
        {
            var r = $"{ruta}.brackets[{i}]";
            var desde = Entero(lista[i], "fromMonths", r, requerido: true, errores);
            var hasta = Entero(lista[i], "toMonths", r, requerido: false, errores);
            var fraccion = Fraccion(lista[i], "admissibleFraction", r, errores);
            if (desde is null || fraccion is null) continue;
            if (tramos.Count == 0 && desde != 0) errores.Add(new($"{r}.fromMonths", "El primer tramo empieza en 0 meses."));
            if (tramos.Count > 0 && tramos[^1].MesesHasta is { } anterior && desde != anterior)
                errores.Add(new($"{r}.fromMonths", desde < anterior ? "El tramo se cruza con el anterior." : "Hay un hueco con el tramo anterior."));
            if (tramos.Count > 0 && tramos[^1].MesesHasta is null) errores.Add(new(r, "Sólo el último tramo va sin tope."));
            if (hasta is { } h && h <= desde) errores.Add(new(r, "El tramo termina antes de empezar."));
            tramos.Add(new(desde.Value, hasta, fraccion.Value));
        }
        return new(naturaleza, tramos);
    }

    // ------------------------------------------------------------------------------------------------- lectores --

    private static JsonElement? Objeto(JsonElement padre, string propiedad, string ruta, List<ErrorDeDocumentoNormativo> errores)
    {
        if (padre.TryGetProperty(propiedad, out var v) && v.ValueKind == JsonValueKind.Object) return v;
        errores.Add(new($"{ruta}.{propiedad}", "Es obligatorio y es un objeto."));
        return null;
    }

    internal static RatingCategory? Categoria(JsonElement objeto, string propiedad, string ruta, List<ErrorDeDocumentoNormativo> errores)
    {
        var texto = LecturaDeDocumento.Texto(objeto, propiedad, ruta, requerido: true, errores);
        if (texto is null) return null;
        if (Enum.TryParse<RatingCategory>(texto, out var cat) && Enum.IsDefined(cat) && texto.Length == 1) return cat;
        errores.Add(new($"{ruta}.{propiedad}", $"«{texto}» no es una categoría (A, B, C, D, E)."));
        return null;
    }

    internal static int? Entero(JsonElement objeto, string propiedad, string ruta, bool requerido, List<ErrorDeDocumentoNormativo> errores)
    {
        if (!objeto.TryGetProperty(propiedad, out var v) || v.ValueKind == JsonValueKind.Null)
        {
            if (requerido) errores.Add(new($"{ruta}.{propiedad}", "Es obligatorio."));
            return null;
        }
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) && n >= 0) return n;
        errores.Add(new($"{ruta}.{propiedad}", "Debe ser un entero mayor o igual que cero."));
        return null;
    }

    /// <summary>Una fracción entre 0 y 1 (los porcentajes nunca se escriben de 0 a 100).</summary>
    internal static decimal? Fraccion(JsonElement objeto, string propiedad, string ruta, List<ErrorDeDocumentoNormativo> errores)
    {
        if (!objeto.TryGetProperty(propiedad, out var v) || v.ValueKind != JsonValueKind.Number || !v.TryGetDecimal(out var d))
        {
            errores.Add(new($"{ruta}.{propiedad}", "Es obligatorio y es un número."));
            return null;
        }
        if (d < 0m || d > 1m)
        {
            errores.Add(new($"{ruta}.{propiedad}", $"{d.ToString(CultureInfo.InvariantCulture)} está fuera de 0..1: va como fracción."));
            return null;
        }
        return d;
    }
}
