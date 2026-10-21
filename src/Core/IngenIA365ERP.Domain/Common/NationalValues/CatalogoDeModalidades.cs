using System.Text.Json;

namespace IngenIA365ERP.Domain.Common.NationalValues;

/// <summary>
/// Una combinación modalidad + sub-segmento del catálogo, con lo que la norma le asigna: segmento SARC, segmento de
/// pérdida esperada (si lo tiene), la clave de <c>USURA</c> (modalidad certificada) o el tope de vivienda, y si admite UVR.
/// </summary>
public sealed record CombinacionDeModalidad(
    string ModalityCode,
    string? SubsegmentCode,
    string SarcSegment,
    string? ExpectedLossSegment,
    string? UsuryKey,
    bool HousingRateCap,
    bool AdmitsUvr);

/// <summary>Una modalidad de crédito del catálogo nacional y sus combinaciones; <see cref="RetiredFrom"/> cierra las líneas a créditos nuevos.</summary>
public sealed record Modalidad(string Code, string Name, DateOnly? RetiredFrom, bool Verified, IReadOnlyList<CombinacionDeModalidad> Combinaciones);

/// <summary>
/// Documento normativo <see cref="RegulatoryDocumentKind.CatalogoDeModalidades"/> tipado (feature 013, T44, R59; DT
/// §2.21; data-model parte 3 §12.4). Modalidades y sub-segmentos son nacionales, con vigencia: la cooperativa no inventa
/// modalidades y la línea de crédito sella <b>códigos</b>. No hay tablas de modalidades.
///
/// <para>Esquema 1 (camelCase): <c>{ modalities: [ { code, name, retiredFrom?, verified?, combinations: [ { subsegment?,
/// sarcSegment, expectedLossSegment?, usuryKey?, housingRateCap?, admitsUvr? } ] } ] }</c>. Una modalidad con una sola
/// combinación puede no tener sub-segmento; con varias, todas lo llevan y no se repiten. Cada combinación lleva un
/// segmento SARC de <see cref="SegmentosSarc"/>, a lo sumo un segmento de <see cref="SegmentosDePerdidaEsperada"/> y
/// <b>o</b> una clave de usura <b>o</b> el tope de vivienda.</para>
/// </summary>
public sealed class CatalogoDeModalidades
{
    public const short VersionDeEsquemaVigente = 1;

    public static IReadOnlyList<short> VersionesDeEsquema { get; } = [1];

    /// <summary>Segmentos SARC del Anexo 1 que el motor de riesgo conoce.</summary>
    public static IReadOnlyList<string> SegmentosSarc { get; } = ["VIVIENDA", "CONSUMO", "MICROCREDITO", "COMERCIAL_PN", "COMERCIAL_PJ"];

    /// <summary>Segmentos del modelo de pérdida esperada (Anexo 2).</summary>
    public static IReadOnlyList<string> SegmentosDePerdidaEsperada { get; } = ["CONSUMO_LIBRANZA", "CONSUMO_SIN_LIBRANZA", "COMERCIAL_PN"];

    private CatalogoDeModalidades(IReadOnlyList<Modalidad> modalidades) => Modalidades = modalidades;

    public IReadOnlyList<Modalidad> Modalidades { get; }

    /// <summary>Las claves de <c>USURA</c> (modalidades certificadas) que asigna el catálogo, sin repetir.</summary>
    public IReadOnlyList<string> ClavesDeUsura =>
        Modalidades.SelectMany(m => m.Combinaciones).Select(c => c.UsuryKey).OfType<string>().Distinct(StringComparer.Ordinal).ToList();

    /// <summary>La combinación exacta, o nulo si la modalidad no existe o no tiene ese sub-segmento.</summary>
    public CombinacionDeModalidad? Buscar(string? modalidad, string? subsegmento) =>
        Modalidades.FirstOrDefault(m => string.Equals(m.Code, modalidad, StringComparison.Ordinal))?
            .Combinaciones.FirstOrDefault(c => string.Equals(c.SubsegmentCode, subsegmento, StringComparison.Ordinal));

    public static ResultadoDeDocumento<CatalogoDeModalidades> Leer(string contenido, short versionDeEsquema)
    {
        var errores = new List<ErrorDeDocumentoNormativo>();
        if (!LecturaDeDocumento.VersionSoportada(versionDeEsquema, VersionesDeEsquema, errores))
            return new(null, errores);

        using var doc = LecturaDeDocumento.Abrir(contenido, errores);
        if (doc is null) return new(null, errores);

        var modalidades = new List<Modalidad>();
        var codigos = new HashSet<string>(StringComparer.Ordinal);
        var lista = LecturaDeDocumento.Arreglo(doc.RootElement, "modalities", "$", noVacio: true, errores);
        for (var i = 0; i < lista.Count; i++)
        {
            var ruta = $"$.modalities[{i}]";
            var m = lista[i];
            if (m.ValueKind != JsonValueKind.Object)
            {
                errores.Add(new(ruta, "Cada modalidad es un objeto."));
                continue;
            }

            var code = LecturaDeDocumento.Texto(m, "code", ruta, requerido: true, errores);
            if (code is not null && !LecturaDeDocumento.EsCodigo(code))
                errores.Add(new($"{ruta}.code", "El código va en mayúsculas, dígitos y guion bajo."));
            else if (code is not null && !codigos.Add(code))
                errores.Add(new($"{ruta}.code", $"La modalidad {code} está repetida."));

            var name = LecturaDeDocumento.Texto(m, "name", ruta, requerido: true, errores);
            var retirada = LecturaDeDocumento.Fecha(m, "retiredFrom", ruta, errores);
            var verificada = LecturaDeDocumento.Booleano(m, "verified", ruta, false, errores);
            var combinaciones = LeerCombinaciones(m, ruta, code ?? string.Empty, errores);
            modalidades.Add(new Modalidad(code ?? string.Empty, name ?? string.Empty, retirada, verificada, combinaciones));
        }

        return errores.Count > 0 ? new(null, errores) : new(new CatalogoDeModalidades(modalidades), errores);
    }

    private static List<CombinacionDeModalidad> LeerCombinaciones(JsonElement modalidad, string rutaModalidad, string codigo, List<ErrorDeDocumentoNormativo> errores)
    {
        var resultado = new List<CombinacionDeModalidad>();
        var lista = LecturaDeDocumento.Arreglo(modalidad, "combinations", rutaModalidad, noVacio: true, errores);
        var subsegmentos = new HashSet<string>(StringComparer.Ordinal);
        for (var j = 0; j < lista.Count; j++)
        {
            var ruta = $"{rutaModalidad}.combinations[{j}]";
            var c = lista[j];
            if (c.ValueKind != JsonValueKind.Object)
            {
                errores.Add(new(ruta, "Cada combinación es un objeto."));
                continue;
            }

            var sub = LecturaDeDocumento.Texto(c, "subsegment", ruta, requerido: false, errores);
            if (sub is null && lista.Count > 1)
                errores.Add(new($"{ruta}.subsegment", "Con varias combinaciones, cada una lleva su sub-segmento."));
            else if (sub is not null && !subsegmentos.Add(sub))
                errores.Add(new($"{ruta}.subsegment", $"El sub-segmento {sub} está repetido en la modalidad."));

            var sarc = LecturaDeDocumento.Texto(c, "sarcSegment", ruta, requerido: true, errores);
            if (sarc is not null && !SegmentosSarc.Contains(sarc))
                errores.Add(new($"{ruta}.sarcSegment", $"«{sarc}» no es un segmento SARC; se admiten: {string.Join(", ", SegmentosSarc)}."));

            var perdida = LecturaDeDocumento.Texto(c, "expectedLossSegment", ruta, requerido: false, errores);
            if (perdida is not null && !SegmentosDePerdidaEsperada.Contains(perdida))
                errores.Add(new($"{ruta}.expectedLossSegment", $"«{perdida}» no es un segmento de pérdida esperada; se admiten: {string.Join(", ", SegmentosDePerdidaEsperada)}."));

            var usura = LecturaDeDocumento.Texto(c, "usuryKey", ruta, requerido: false, errores);
            if (usura is not null && !LecturaDeDocumento.EsCodigo(usura))
                errores.Add(new($"{ruta}.usuryKey", "La clave de usura va en mayúsculas, dígitos y guion bajo."));

            var tope = LecturaDeDocumento.Booleano(c, "housingRateCap", ruta, false, errores);
            if ((usura is not null) == tope)
                errores.Add(new(ruta, "Cada combinación lleva una clave de usura o el tope de vivienda, y sólo uno de los dos."));

            var uvr = LecturaDeDocumento.Booleano(c, "admitsUvr", ruta, false, errores);
            resultado.Add(new CombinacionDeModalidad(codigo, sub, sarc ?? string.Empty, perdida, usura, tope, uvr));
        }
        return resultado;
    }
}

/// <summary>El validador de la plataforma para <see cref="RegulatoryDocumentKind.CatalogoDeModalidades"/>.</summary>
public sealed class ValidadorDeCatalogoDeModalidades : IValidadorDeDocumentoNormativo
{
    public RegulatoryDocumentKind Clase => RegulatoryDocumentKind.CatalogoDeModalidades;

    public IReadOnlyList<short> VersionesDeEsquema => CatalogoDeModalidades.VersionesDeEsquema;

    public IReadOnlyList<ErrorDeDocumentoNormativo> Validar(string contenido, short versionDeEsquema) =>
        CatalogoDeModalidades.Leer(contenido, versionDeEsquema).Errores;
}
