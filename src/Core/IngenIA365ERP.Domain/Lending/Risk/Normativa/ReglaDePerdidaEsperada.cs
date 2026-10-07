using IngenIA365ERP.Domain.Common.NationalValues;

namespace IngenIA365ERP.Domain.Lending.Risk.Normativa;

/// <summary>
/// Documento normativo <see cref="RegulatoryDocumentKind.ReglaDePerdidaEsperada"/> tipado (feature 013, T45, R15, T0204; data-model
/// parte 3 §12.4; Circular Externa 100): quiénes están obligados al modelo de pérdida esperada (tipos de organización y categorías de
/// fondos de empleados), qué segmentos cubre, cuántos meses de historial pide el modelo y con cuántos días de antelación se avisa la
/// adopción o el desistimiento voluntario. Entra en K1 para detectar la obligación; el modelo es de K6a. Sólo forma y reglas de forma.
///
/// <para>Esquema 1 (camelCase): <c>{ obligatedOrganizationTypes: [codigo], obligatedEmployeeFundCategories: [codigo],
/// coveredSegments: [codigo], historyMonths, voluntaryNoticeDays }</c>. Códigos en mayúsculas sin repetir; los segmentos cubiertos
/// son de <see cref="SegmentosConocidos"/>; meses y días mayores que cero.</para>
/// </summary>
public sealed class ReglaDePerdidaEsperada
{
    public const short VersionDeEsquemaVigente = 1;

    public static IReadOnlyList<short> VersionesDeEsquema { get; } = [1];

    /// <summary>Los segmentos de pérdida esperada (data-model parte 3 §12.4).</summary>
    public static IReadOnlyList<string> SegmentosConocidos { get; } = ["CONSUMO_LIBRANZA", "CONSUMO_SIN_LIBRANZA", "COMERCIAL_PN"];

    private ReglaDePerdidaEsperada() { }

    public IReadOnlyList<string> TiposObligados { get; private init; } = [];

    public IReadOnlyList<string> CategoriasDeFondoObligadas { get; private init; } = [];

    public IReadOnlyList<string> SegmentosCubiertos { get; private init; } = [];

    public int MesesDeHistorial { get; private init; }

    public int DiasDeAvisoVoluntario { get; private init; }

    public static ResultadoDeDocumento<ReglaDePerdidaEsperada> Leer(string contenido, short versionDeEsquema)
    {
        var errores = new List<ErrorDeDocumentoNormativo>();
        if (!LecturaDeDocumento.VersionSoportada(versionDeEsquema, VersionesDeEsquema, errores)) return new(null, errores);
        using var doc = LecturaDeDocumento.Abrir(contenido, errores);
        if (doc is null) return new(null, errores);
        var raiz = doc.RootElement;

        var tipos = Codigos(raiz, "obligatedOrganizationTypes", noVacio: true, null, errores);
        var categorias = Codigos(raiz, "obligatedEmployeeFundCategories", noVacio: false, null, errores);
        var segmentos = Codigos(raiz, "coveredSegments", noVacio: true, SegmentosConocidos, errores);
        var meses = Anexo1.Entero(raiz, "historyMonths", "$", requerido: true, errores);
        var dias = Anexo1.Entero(raiz, "voluntaryNoticeDays", "$", requerido: true, errores);
        if (meses == 0) errores.Add(new("$.historyMonths", "Los meses de historial son mayores que cero."));
        if (dias == 0) errores.Add(new("$.voluntaryNoticeDays", "Los días de aviso son mayores que cero."));

        return errores.Count > 0
            ? new(null, errores)
            : new(new ReglaDePerdidaEsperada
            {
                TiposObligados = tipos,
                CategoriasDeFondoObligadas = categorias,
                SegmentosCubiertos = segmentos,
                MesesDeHistorial = meses!.Value,
                DiasDeAvisoVoluntario = dias!.Value,
            }, errores);
    }

    private static List<string> Codigos(System.Text.Json.JsonElement raiz, string propiedad, bool noVacio, IReadOnlyList<string>? admitidos,
        List<ErrorDeDocumentoNormativo> errores)
    {
        var codigos = new List<string>();
        var lista = LecturaDeDocumento.Arreglo(raiz, propiedad, "$", noVacio, errores);
        for (var i = 0; i < lista.Count; i++)
        {
            var ruta = $"$.{propiedad}[{i}]";
            var texto = lista[i].ValueKind == System.Text.Json.JsonValueKind.String ? lista[i].GetString()?.Trim() : null;
            if (string.IsNullOrEmpty(texto) || !LecturaDeDocumento.EsCodigo(texto))
            {
                errores.Add(new(ruta, "Cada código va en mayúsculas, dígitos y guion bajo."));
                continue;
            }
            if (admitidos is not null && !admitidos.Contains(texto, StringComparer.Ordinal))
                errores.Add(new(ruta, $"«{texto}» no se conoce; se admiten: {string.Join(", ", admitidos)}."));
            else if (codigos.Contains(texto, StringComparer.Ordinal))
                errores.Add(new(ruta, $"{texto} está repetido."));
            codigos.Add(texto);
        }
        return codigos;
    }
}
