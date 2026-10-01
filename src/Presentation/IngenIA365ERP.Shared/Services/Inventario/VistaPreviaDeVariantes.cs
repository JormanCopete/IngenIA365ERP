namespace IngenIA365ERP.Shared.Services.Inventario;

/// <summary>
/// La vista previa de las combinaciones que la ficha de una plantilla muestra antes de generar (feature 012, I6, T937; US15-1): el producto
/// cartesiano de los valores elegidos, en el orden en que se eligieron, con la <c>VariantKey</c> como la arma el servidor
/// (<c>GeneradorDeVariantes.ClaveDe</c>: atributo=valor en mayúsculas, ordenado por código de atributo y separado por «;»), el código propuesto
/// (<c>{plantilla}-{valor}-{valor}</c>, hasta 20) y el nombre (<c>{plantilla} {valor} {valor}</c>), y si ya existe. Es sólo lo que se ve: la
/// generación la decide el servidor, que vuelve a proponer y rechaza lo repetido. (nuevo)
/// </summary>
public static class VistaPreviaDeVariantes
{
    /// <summary>El largo del código de un producto (<c>CodigoDeCatalogo.LargoLargo</c>).</summary>
    public const int LargoDelCodigo = 20;

    /// <summary>Un valor elegido: código y nombre.</summary>
    public sealed record Valor(string Codigo, string Nombre);

    /// <summary>Un atributo elegido con sus valores, en orden.</summary>
    public sealed record Atributo(string Codigo, IReadOnlyList<Valor> Valores);

    /// <summary>Una combinación propuesta.</summary>
    public sealed record Combinacion(string VariantKey, string Codigo, string Nombre, bool Existe);

    /// <summary>
    /// Las combinaciones de <paramref name="atributos"/> sobre la plantilla <paramref name="plantilla"/> (su código, que también encabeza el
    /// nombre de la vista previa), marcando las que están en <paramref name="existentes"/>. Sin atributos, o con uno sin valores, ninguna.
    /// </summary>
    public static IReadOnlyList<Combinacion> Combinaciones(string plantilla, IReadOnlyList<Atributo> atributos, IEnumerable<string> existentes,
        string? nombreDeLaPlantilla = null)
    {
        if (atributos.Count == 0 || atributos.Any(a => a.Valores.Count == 0)) return [];
        var ya = existentes.Select(Normalizar).ToHashSet(StringComparer.Ordinal);
        var codigoDePlantilla = Mayusculas(plantilla);
        var nombre = (nombreDeLaPlantilla ?? plantilla).Trim();

        IEnumerable<IReadOnlyList<(string Atributo, Valor Valor)>> parciales = [[]];
        foreach (var atributo in atributos)
            parciales = parciales.SelectMany(p => atributo.Valores.Select(v => (IReadOnlyList<(string, Valor)>)[.. p, (Mayusculas(atributo.Codigo), v)]));

        return parciales.Select(c =>
        {
            var clave = Clave(c.Select(x => (x.Atributo, x.Valor.Codigo)));
            var completo = string.Join('-', new[] { codigoDePlantilla }.Concat(c.Select(x => Mayusculas(x.Valor.Codigo))));
            var codigo = completo.Length <= LargoDelCodigo ? completo : completo[..LargoDelCodigo].TrimEnd('-');
            return new Combinacion(clave, codigo, string.Join(' ', new[] { nombre }.Concat(c.Select(x => x.Valor.Nombre.Trim()))), ya.Contains(clave));
        }).ToList();
    }

    /// <summary>La <c>VariantKey</c> de unos pares atributo–valor: en mayúsculas y por código de atributo.</summary>
    public static string Clave(IEnumerable<(string Atributo, string Valor)> pares) =>
        string.Join(';', pares.Select(p => (Atributo: Mayusculas(p.Atributo), Valor: Mayusculas(p.Valor)))
            .OrderBy(p => p.Atributo, StringComparer.Ordinal)
            .Select(p => $"{p.Atributo}={p.Valor}"));

    private static string Normalizar(string clave) =>
        Clave(clave.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(par => par.Split('=', 2, StringSplitOptions.TrimEntries))
            .Where(p => p.Length == 2)
            .Select(p => (p[0], p[1])));

    private static string Mayusculas(string texto) => (texto ?? string.Empty).Trim().ToUpperInvariant();
}
