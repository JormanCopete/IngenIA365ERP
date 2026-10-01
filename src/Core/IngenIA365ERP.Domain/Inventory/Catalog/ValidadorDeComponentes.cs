using IngenIA365ERP.Domain.Enums.Inventory;

namespace IngenIA365ERP.Domain.Inventory.Catalog;

/// <summary>Un producto del grafo de componentes: su Id, su código (para los mensajes), su clase y los decimales de su unidad base. (nuevo)</summary>
public sealed record ProductoDelGrafo(int ProductId, string Codigo, ProductKind Kind, int DecimalesDeUnidadBase);

/// <summary>Un componente que se quiere poner: el producto y la cantidad por unidad del combo o del kit, en su unidad base. (nuevo)</summary>
public sealed record ComponentePropuesto(int ComponentProductId, decimal Quantity);

/// <summary>
/// Lo que entra al validador: el combo o kit, la lista nueva de componentes (reemplaza la actual), los productos que intervienen
/// (el propio y cada componente) y el grafo de componentes ya registrado —de cada combo o kit, sus componentes— para buscar ciclos.
/// La aplicación lo arma de <c>INV_ProductComponents</c>; la entrada del propio producto se ignora, porque la lista nueva la
/// reemplaza. (nuevo)
/// </summary>
public sealed record PedidoDeComponentes(
    int ProductId,
    IReadOnlyList<ComponentePropuesto> Componentes,
    IReadOnlyDictionary<int, ProductoDelGrafo> Productos,
    IReadOnlyDictionary<int, IReadOnlyList<int>> Grafo);

/// <summary>Un error de la lista de componentes, con el componente que lo causa (nulo si es de la lista entera). (nuevo)</summary>
public sealed record ErrorDeComponente(string Codigo, string Mensaje, int? ComponentProductId);

/// <summary>Lo que devuelve el validador: todos los errores, no sólo el primero. (nuevo)</summary>
public sealed record ResultadoDeComponentes(IReadOnlyList<ErrorDeComponente> Errores)
{
    public bool Valido => Errores.Count == 0;
}

/// <summary>
/// El validador de los componentes de un combo o kit (feature 012, I6, T914; data-model §1.11; FR-023): puro y sin IO.
/// <list type="bullet">
/// <item>sólo un <see cref="ProductKind.Combo"/> o un <see cref="ProductKind.Kit"/> lleva componentes, y siempre al menos uno;</item>
/// <item>un componente es <see cref="ProductKind.Inventoriable"/> o <see cref="ProductKind.Variant"/>: ni combo, ni kit, ni plantilla,
/// ni servicio (<see cref="CodigoClaseInvalida"/>), y no se repite en la lista;</item>
/// <item>la cantidad es mayor que cero y no tiene más decimales que la unidad base del componente;</item>
/// <item>sin ciclos: recorre el grafo recibido desde cada componente y rechaza el que llega de vuelta al producto. Con las reglas de
/// clase un ciclo no debería poder formarse, pero el grafo viene de la base (y de cargas masivas) y se revisa igual.</item>
/// </list>
/// Lo usan <c>SetProductComponentsCommand</c> (T920) y la plantilla de componentes si el dueño la aprueba (T922). (nuevo)
/// </summary>
public static class ValidadorDeComponentes
{
    /// <summary>El componente es de una clase que no puede ser componente (T920). (nuevo)</summary>
    public const string CodigoClaseInvalida = "Inventory.Component.InvalidKind";

    /// <summary>El componente lleva, por el grafo, de vuelta al producto (T920). (nuevo)</summary>
    public const string CodigoCiclo = "Inventory.Component.Cycle";

    /// <summary>Un combo o kit sin componentes. (nuevo)</summary>
    public const string CodigoSinComponentes = "Inventory.Component.Required";

    /// <summary>El producto al que se le ponen componentes no es un combo ni un kit. (nuevo)</summary>
    public const string CodigoNoEsComboNiKit = "Inventory.Component.NotAComboOrKit";

    /// <summary>Cantidad cero, negativa o con más decimales que la unidad base del componente. (nuevo)</summary>
    public const string CodigoCantidadInvalida = "Inventory.Component.InvalidQuantity";

    /// <summary>El mismo componente dos veces (<c>UK (ProductId, ComponentProductId)</c>). (nuevo)</summary>
    public const string CodigoRepetido = "Inventory.Component.Duplicate";

    public static ResultadoDeComponentes Validar(PedidoDeComponentes pedido)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        var producto = Producto(pedido, pedido.ProductId);
        var errores = new List<ErrorDeComponente>();

        if (producto.Kind is not (ProductKind.Combo or ProductKind.Kit))
            errores.Add(new(CodigoNoEsComboNiKit, $"{producto.Codigo} no es un combo ni un kit: no lleva componentes.", null));
        else if (pedido.Componentes.Count == 0)
            errores.Add(new(CodigoSinComponentes, $"{producto.Codigo} necesita al menos un componente.", null));

        var vistos = new HashSet<int>();
        foreach (var c in pedido.Componentes)
        {
            var componente = Producto(pedido, c.ComponentProductId);
            if (!vistos.Add(c.ComponentProductId))
            {
                errores.Add(new(CodigoRepetido, $"{componente.Codigo} está más de una vez: sume las cantidades en una sola línea.", c.ComponentProductId));
                continue;
            }

            if (c.ComponentProductId == pedido.ProductId || LlegaA(pedido, c.ComponentProductId, pedido.ProductId))
                errores.Add(new(CodigoCiclo, $"{componente.Codigo} contiene (directa o indirectamente) a {producto.Codigo}: formaría un ciclo.",
                    c.ComponentProductId));
            else if (componente.Kind is not (ProductKind.Inventoriable or ProductKind.Variant))
                errores.Add(new(CodigoClaseInvalida,
                    $"{componente.Codigo} es {componente.Kind}: un componente sólo puede ser un producto inventariable o una variante.",
                    c.ComponentProductId));

            if (c.Quantity <= 0m)
                errores.Add(new(CodigoCantidadInvalida, $"La cantidad de {componente.Codigo} debe ser mayor que cero.", c.ComponentProductId));
            else if (Math.Round(c.Quantity, componente.DecimalesDeUnidadBase, MidpointRounding.AwayFromZero) != c.Quantity)
                errores.Add(new(CodigoCantidadInvalida,
                    $"La unidad base de {componente.Codigo} admite {componente.DecimalesDeUnidadBase} decimales; la cantidad {c.Quantity} tiene más.",
                    c.ComponentProductId));
        }

        return new ResultadoDeComponentes(errores);
    }

    /// <summary>¿Desde <paramref name="desde"/>, siguiendo el grafo (sin la entrada del producto que se reemplaza), se llega a <paramref name="destino"/>?</summary>
    private static bool LlegaA(PedidoDeComponentes pedido, int desde, int destino)
    {
        var visitados = new HashSet<int>();
        var pendientes = new Stack<int>();
        pendientes.Push(desde);
        while (pendientes.Count > 0)
        {
            var actual = pendientes.Pop();
            if (!visitados.Add(actual) || actual == pedido.ProductId && actual != desde) continue;
            if (!pedido.Grafo.TryGetValue(actual, out var hijos)) continue;
            foreach (var hijo in hijos)
            {
                if (hijo == destino) return true;
                pendientes.Push(hijo);
            }
        }

        return false;
    }

    private static ProductoDelGrafo Producto(PedidoDeComponentes pedido, int id) =>
        pedido.Productos.TryGetValue(id, out var p)
            ? p
            : throw new ArgumentException($"El producto {id} no viene en el grafo: la aplicación debe cargar todos los que intervienen.", nameof(pedido));
}
