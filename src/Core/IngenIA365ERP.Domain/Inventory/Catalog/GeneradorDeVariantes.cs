namespace IngenIA365ERP.Domain.Inventory.Catalog;

/// <summary>Un valor elegido de un atributo de variante (<c>INV_VariantAttributeValues</c>): código, nombre y orden de presentación. (nuevo)</summary>
public sealed record ValorDeAtributo(string Codigo, string Nombre, int SortOrder);

/// <summary>Un atributo de variante elegido para una plantilla (<c>INV_VariantAttributes</c>) con los valores que se combinan. (nuevo)</summary>
public sealed record AtributoParaVariantes(string Codigo, string Nombre, IReadOnlyList<ValorDeAtributo> Valores);

/// <summary>
/// Lo que entra al generador: el código y el nombre de la plantilla, los atributos elegidos en el orden en que la persona los eligió
/// (así se arman el código y el nombre propuestos) y las <c>VariantKey</c> que la plantilla ya tiene. (nuevo)
/// </summary>
public sealed record PedidoDeVariantes(
    string CodigoDeLaPlantilla,
    string NombreDeLaPlantilla,
    IReadOnlyList<AtributoParaVariantes> Atributos,
    IReadOnlyCollection<string> ClavesExistentes);

/// <summary>Un par atributo–valor de una variante, por sus códigos normalizados. (nuevo)</summary>
public sealed record ValorDeVariante(string Atributo, string Valor);

/// <summary>
/// Una variante propuesta: su <c>VariantKey</c>, el código y el nombre propuestos (la persona puede cambiarlos antes de crearla) y sus
/// valores en el orden elegido. <see cref="CodigoRecortado"/> avisa que el código se cortó al largo del código de producto;
/// <see cref="CodigoRepetido"/>, que el recorte lo dejó igual al de otra propuesta. (nuevo)
/// </summary>
public sealed record VariantePropuesta(
    string VariantKey,
    string Codigo,
    string Nombre,
    IReadOnlyList<ValorDeVariante> Valores,
    bool CodigoRecortado,
    bool CodigoRepetido);

/// <summary>Por qué no se generan variantes. (nuevo)</summary>
public sealed record RechazoDeVariantes(string Codigo, string Mensaje);

/// <summary>Lo que devuelve el generador: las combinaciones que faltan, las que ya existían, o el rechazo. (nuevo)</summary>
public sealed record ResultadoDeVariantes(
    IReadOnlyList<VariantePropuesta> Propuestas,
    IReadOnlyList<string> Existentes,
    RechazoDeVariantes? Rechazo = null);

/// <summary>
/// El generador de variantes (feature 012, I6, T913; US15-1; data-model §1.6, §1.11): motor puro y sin IO. De los atributos elegidos
/// para una plantilla y sus valores produce el producto cartesiano —el primer atributo elegido es el que cambia más despacio; dentro
/// de un atributo, los valores por <c>SortOrder</c> y código— y devuelve sólo las combinaciones que la plantilla todavía no tiene.
/// <list type="bullet">
/// <item><b><c>VariantKey</c></b>: <c>ATRIBUTO=VALOR</c> separados por <c>;</c>, en mayúsculas y <b>ordenados por código de
/// atributo</b> (<c>COLOR=AZUL;TALLA=M</c>), para que una misma combinación tenga una sola clave sin importar el orden en que se
/// eligió; es la que protege <c>UK_INV_Products_Parent_VariantKey</c>.</item>
/// <item><b>Código propuesto</b>: <c>{plantilla}-{valor}-{valor}</c> en el orden elegido, recortado a <see cref="LargoDelCodigo"/>
/// (<c>CodigoDeCatalogo.LargoLargo</c>, el largo de <c>INV_Products.Code</c>) sin dejar un guion colgando; es una propuesta que la
/// aplicación valida como cualquier código (<c>Catalogo.CodigoDuplicado</c>).</item>
/// <item><b>Nombre propuesto</b>: «{plantilla} {valor} {valor}» con los nombres de los valores.</item>
/// <item>Todas las variantes de una plantilla llevan valores para los mismos atributos: si las existentes usan otros,
/// <see cref="CodigoAtributosDistintos"/>.</item>
/// </list>
/// Lo usa <c>GenerateProductVariantsCommand</c> (T919), que crea los productos <c>Variant</c> con lo heredado de la plantilla. (nuevo)
/// </summary>
public static class GeneradorDeVariantes
{
    /// <summary>El largo del código de producto (<c>CodigoDeCatalogo.LargoLargo</c>; Domain no ve Application).</summary>
    public const int LargoDelCodigo = 20;

    /// <summary>Sin atributos, o un atributo sin valores: no hay combinación posible. (nuevo)</summary>
    public const string CodigoSinAtributos = "Inventory.Variant.AttributesRequired";

    /// <summary>El mismo atributo dos veces, o el mismo valor dos veces en un atributo. (nuevo)</summary>
    public const string CodigoAtributoRepetido = "Inventory.Variant.AttributeRepeated";

    /// <summary>Las variantes que ya tiene la plantilla usan otros atributos (data-model §1.11). (nuevo)</summary>
    public const string CodigoAtributosDistintos = "Inventory.Variant.AttributesMismatch";

    private const char SeparadorDePares = ';';
    private const char SeparadorDeValor = '=';
    private const char SeparadorDelCodigo = '-';

    public static ResultadoDeVariantes Generar(PedidoDeVariantes pedido)
    {
        ArgumentNullException.ThrowIfNull(pedido);

        var atributos = pedido.Atributos.Select(a => new AtributoParaVariantes(Codigo(a.Codigo), a.Nombre,
                a.Valores.Select(v => v with { Codigo = Codigo(v.Codigo) })
                    .OrderBy(v => v.SortOrder).ThenBy(v => v.Codigo, StringComparer.Ordinal).ToList()))
            .ToList();

        if (atributos.Count == 0 || atributos.Any(a => a.Valores.Count == 0))
            return Rechazo(CodigoSinAtributos, "Elija al menos un atributo y, en cada atributo, al menos un valor.");
        if (atributos.Select(a => a.Codigo).Distinct(StringComparer.Ordinal).Count() != atributos.Count)
            return Rechazo(CodigoAtributoRepetido, "Un atributo sólo se elige una vez.");
        if (atributos.FirstOrDefault(a => a.Valores.Select(v => v.Codigo).Distinct(StringComparer.Ordinal).Count() != a.Valores.Count) is { } conRepetido)
            return Rechazo(CodigoAtributoRepetido, $"El atributo {conRepetido.Codigo} tiene un valor repetido.");

        var existentes = pedido.ClavesExistentes.Select(Normalizar).ToHashSet(StringComparer.Ordinal);
        var elegidos = string.Join(SeparadorDePares, atributos.Select(a => a.Codigo).Order(StringComparer.Ordinal));
        if (existentes.FirstOrDefault(k => AtributosDe(k) != elegidos) is { } distinta)
            return Rechazo(CodigoAtributosDistintos,
                $"Las variantes de la plantilla llevan los atributos {AtributosDe(distinta).Replace(SeparadorDePares, ',')}; " +
                $"las nuevas deben llevar los mismos (se eligieron {elegidos.Replace(SeparadorDePares, ',')}).");

        var plantilla = Codigo(pedido.CodigoDeLaPlantilla);
        var propuestas = new List<VariantePropuesta>();
        var yaExistian = new List<string>();
        foreach (var combinacion in Combinaciones(atributos))
        {
            var valores = combinacion.Select(x => new ValorDeVariante(x.Atributo.Codigo, x.Valor.Codigo)).ToList();
            var clave = ClaveDe(valores.Select(v => (v.Atributo, v.Valor)));
            if (existentes.Contains(clave))
            {
                yaExistian.Add(clave);
                continue;
            }

            var completo = string.Join(SeparadorDelCodigo, new[] { plantilla }.Concat(valores.Select(v => v.Valor)));
            var codigo = completo.Length <= LargoDelCodigo ? completo : completo[..LargoDelCodigo].TrimEnd(SeparadorDelCodigo);
            var nombre = string.Join(' ', new[] { pedido.NombreDeLaPlantilla.Trim() }.Concat(combinacion.Select(x => x.Valor.Nombre.Trim())));
            propuestas.Add(new VariantePropuesta(clave, codigo, nombre, valores, codigo.Length < completo.Length, false));
        }

        var repetidos = propuestas.GroupBy(p => p.Codigo, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);
        return new ResultadoDeVariantes(
            propuestas.Select(p => repetidos.Contains(p.Codigo) ? p with { CodigoRepetido = true } : p).ToList(), yaExistian);
    }

    /// <summary>La <c>VariantKey</c> de una combinación: pares en mayúsculas, ordenados por código de atributo.</summary>
    public static string ClaveDe(IEnumerable<(string Atributo, string Valor)> pares) =>
        string.Join(SeparadorDePares, pares.Select(p => (Atributo: Codigo(p.Atributo), Valor: Codigo(p.Valor)))
            .OrderBy(p => p.Atributo, StringComparer.Ordinal)
            .Select(p => $"{p.Atributo}{SeparadorDeValor}{p.Valor}"));

    /// <summary>Una <c>VariantKey</c> escrita en cualquier orden o con minúsculas, a su forma canónica.</summary>
    public static string Normalizar(string variantKey)
    {
        ArgumentNullException.ThrowIfNull(variantKey);
        return ClaveDe(variantKey.Split(SeparadorDePares, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(par =>
            {
                var partes = par.Split(SeparadorDeValor, 2, StringSplitOptions.TrimEntries);
                return partes.Length == 2
                    ? (partes[0], partes[1])
                    : throw new ArgumentException($"La clave de variante «{variantKey}» no tiene la forma ATRIBUTO=VALOR;…", nameof(variantKey));
            }));
    }

    private static string AtributosDe(string claveNormalizada) =>
        string.Join(SeparadorDePares, claveNormalizada.Split(SeparadorDePares).Select(p => p.Split(SeparadorDeValor)[0]));

    private static IEnumerable<IReadOnlyList<(AtributoParaVariantes Atributo, ValorDeAtributo Valor)>> Combinaciones(
        IReadOnlyList<AtributoParaVariantes> atributos)
    {
        IEnumerable<IReadOnlyList<(AtributoParaVariantes, ValorDeAtributo)>> parciales = [[]];
        foreach (var atributo in atributos)
            parciales = parciales.SelectMany(p => atributo.Valores.Select(v =>
                (IReadOnlyList<(AtributoParaVariantes, ValorDeAtributo)>)[.. p, (atributo, v)]));
        return parciales;
    }

    private static string Codigo(string codigo) => (codigo ?? string.Empty).Trim().ToUpperInvariant();

    private static ResultadoDeVariantes Rechazo(string codigo, string mensaje) => new([], [], new RechazoDeVariantes(codigo, mensaje));
}
