using System.Text.Json;

namespace IngenIA365ERP.Domain.Common.NationalValues;

/// <summary>Qué dice el tipo de organización sobre el modelo de pérdida esperada: no lo admite, lo admite (voluntario) o lo exige.</summary>
public enum PerdidaEsperadaDelTipo
{
    NotAdmitted = 1,
    Admitted = 2,
    Required = 3,
}

/// <summary>Un tipo de organización solidaria de la taxonomía del supervisor.</summary>
public sealed record TipoDeOrganizacion(string Code, string Name, bool IsEmployeeFund, PerdidaEsperadaDelTipo ExpectedLoss, bool Verified);

/// <summary>Una categoría de los fondos de empleados.</summary>
public sealed record CategoriaDeFondoDeEmpleados(string Code, string Name);

/// <summary>
/// Documento normativo <see cref="RegulatoryDocumentKind.TiposDeOrganizacion"/> tipado (feature 013, T45, T50, R15;
/// data-model parte 3 §4.5, §12.4). El perfil regulatorio de la cooperativa guarda un <c>OrganizationTypeCode</c> de este
/// documento (no es enum: la taxonomía del supervisor cambia) y, si es fondo de empleados, su categoría.
///
/// <para>Esquema 1 (camelCase): <c>{ employeeFundCategories: [ { code, name } ], organizationTypes: [ { code, name,
/// isEmployeeFund, expectedLoss: NotAdmitted | Admitted | Required, verified? } ] }</c>. Códigos únicos; si algún tipo es
/// fondo de empleados, las categorías no pueden faltar.</para>
/// </summary>
public sealed class TiposDeOrganizacion
{
    public const short VersionDeEsquemaVigente = 1;

    public static IReadOnlyList<short> VersionesDeEsquema { get; } = [1];

    private TiposDeOrganizacion(IReadOnlyList<TipoDeOrganizacion> tipos, IReadOnlyList<CategoriaDeFondoDeEmpleados> categorias)
    {
        Tipos = tipos;
        CategoriasDeFondoDeEmpleados = categorias;
    }

    public IReadOnlyList<TipoDeOrganizacion> Tipos { get; }

    public IReadOnlyList<CategoriaDeFondoDeEmpleados> CategoriasDeFondoDeEmpleados { get; }

    public TipoDeOrganizacion? Buscar(string? codigo) =>
        Tipos.FirstOrDefault(t => string.Equals(t.Code, codigo, StringComparison.Ordinal));

    /// <summary>
    /// Si la categoría declarada es válida para el tipo: un fondo de empleados declara una categoría del documento; los
    /// demás tipos no declaran ninguna. Un tipo inexistente nunca es válido.
    /// </summary>
    public bool CategoriaValida(string? tipo, string? categoria)
    {
        var t = Buscar(tipo);
        if (t is null) return false;
        if (!t.IsEmployeeFund) return categoria is null;
        return categoria is not null && CategoriasDeFondoDeEmpleados.Any(c => string.Equals(c.Code, categoria, StringComparison.Ordinal));
    }

    public static ResultadoDeDocumento<TiposDeOrganizacion> Leer(string contenido, short versionDeEsquema)
    {
        var errores = new List<ErrorDeDocumentoNormativo>();
        if (!LecturaDeDocumento.VersionSoportada(versionDeEsquema, VersionesDeEsquema, errores))
            return new(null, errores);

        using var doc = LecturaDeDocumento.Abrir(contenido, errores);
        if (doc is null) return new(null, errores);

        var categorias = new List<CategoriaDeFondoDeEmpleados>();
        var codigosCategoria = new HashSet<string>(StringComparer.Ordinal);
        var listaCategorias = LecturaDeDocumento.Arreglo(doc.RootElement, "employeeFundCategories", "$", noVacio: false, errores);
        for (var i = 0; i < listaCategorias.Count; i++)
        {
            var ruta = $"$.employeeFundCategories[{i}]";
            if (listaCategorias[i].ValueKind != JsonValueKind.Object)
            {
                errores.Add(new(ruta, "Cada categoría es un objeto."));
                continue;
            }
            var code = Codigo(listaCategorias[i], ruta, codigosCategoria, "La categoría", errores);
            var name = LecturaDeDocumento.Texto(listaCategorias[i], "name", ruta, requerido: true, errores);
            categorias.Add(new(code ?? string.Empty, name ?? string.Empty));
        }

        var tipos = new List<TipoDeOrganizacion>();
        var codigosTipo = new HashSet<string>(StringComparer.Ordinal);
        var listaTipos = LecturaDeDocumento.Arreglo(doc.RootElement, "organizationTypes", "$", noVacio: true, errores);
        for (var i = 0; i < listaTipos.Count; i++)
        {
            var ruta = $"$.organizationTypes[{i}]";
            var t = listaTipos[i];
            if (t.ValueKind != JsonValueKind.Object)
            {
                errores.Add(new(ruta, "Cada tipo de organización es un objeto."));
                continue;
            }
            var code = Codigo(t, ruta, codigosTipo, "El tipo", errores);
            var name = LecturaDeDocumento.Texto(t, "name", ruta, requerido: true, errores);
            var fondo = LecturaDeDocumento.Booleano(t, "isEmployeeFund", ruta, false, errores);
            var textoPerdida = LecturaDeDocumento.Texto(t, "expectedLoss", ruta, requerido: true, errores);
            if (textoPerdida is not null && !Enum.GetNames<PerdidaEsperadaDelTipo>().Contains(textoPerdida, StringComparer.Ordinal))
                errores.Add(new($"{ruta}.expectedLoss", $"«{textoPerdida}» no es válido; se admiten: {string.Join(", ", Enum.GetNames<PerdidaEsperadaDelTipo>())}."));
            var perdida = textoPerdida is not null && Enum.TryParse<PerdidaEsperadaDelTipo>(textoPerdida, out var p) ? p : PerdidaEsperadaDelTipo.NotAdmitted;
            var verificado = LecturaDeDocumento.Booleano(t, "verified", ruta, false, errores);
            tipos.Add(new(code ?? string.Empty, name ?? string.Empty, fondo, perdida, verificado));
        }

        if (tipos.Any(t => t.IsEmployeeFund) && categorias.Count == 0)
            errores.Add(new("$.employeeFundCategories", "Hay fondos de empleados: el documento debe declarar sus categorías."));

        return errores.Count > 0 ? new(null, errores) : new(new TiposDeOrganizacion(tipos, categorias), errores);
    }

    private static string? Codigo(JsonElement objeto, string ruta, HashSet<string> vistos, string que, List<ErrorDeDocumentoNormativo> errores)
    {
        var code = LecturaDeDocumento.Texto(objeto, "code", ruta, requerido: true, errores);
        if (code is null) return null;
        if (!LecturaDeDocumento.EsCodigo(code))
            errores.Add(new($"{ruta}.code", "El código va en mayúsculas, dígitos y guion bajo."));
        else if (!vistos.Add(code))
            errores.Add(new($"{ruta}.code", $"{que} {code} está repetido."));
        return code;
    }
}

/// <summary>El validador de la plataforma para <see cref="RegulatoryDocumentKind.TiposDeOrganizacion"/>.</summary>
public sealed class ValidadorDeTiposDeOrganizacion : IValidadorDeDocumentoNormativo
{
    public RegulatoryDocumentKind Clase => RegulatoryDocumentKind.TiposDeOrganizacion;

    public IReadOnlyList<short> VersionesDeEsquema => TiposDeOrganizacion.VersionesDeEsquema;

    public IReadOnlyList<ErrorDeDocumentoNormativo> Validar(string contenido, short versionDeEsquema) =>
        TiposDeOrganizacion.Leer(contenido, versionDeEsquema).Errores;
}
