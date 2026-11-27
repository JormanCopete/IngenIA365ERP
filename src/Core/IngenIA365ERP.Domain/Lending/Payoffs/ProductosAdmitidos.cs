using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Payoffs;

/// <summary>
/// Los productos que un crédito nuevo puede recoger según el parámetro <c>Recogida.ProductosAdmitidos</c> (feature 028, R15). Los nombres son los
/// del texto del parámetro: no se guardan en la base por su número, por eso viven aquí y no en <c>Domain/Enums/Lending</c>. Una cuenta es de uno
/// de los cuatro productos de cuentas según su naturaleza y su clase (<see cref="ProductosAdmitidos.DeCuenta"/>). (nuevo)
/// </summary>
public enum ProductoDeRecogida
{
    Creditos = 1,
    Aportes = 2,
    AhorroALaVista = 3,
    AhorroPermanente = 4,
    AhorroContractual = 5,
    FacturasComerciales = 6,
}

/// <summary>
/// El lector puro del parámetro <c>Recogida.ProductosAdmitidos</c> (feature 028, T003): el texto <c>Producto:modo,modo;Producto:modo</c> → qué
/// productos admite y con qué modos. Los modos del texto son los de la pantalla —<c>Total</c>, <c>Parcial</c>, <c>Cuotas</c>, <c>Abono</c>— y se leen
/// como <see cref="PayoffMode"/> (<c>Total</c>, <c>Partial</c>, <c>Installments</c>, <c>Deposit</c>). Cada producto admite sólo sus modos posibles
/// (<see cref="ModosPosibles"/>): un crédito o una factura, total o parcial; aportes, cuotas o abono; el ahorro a la vista y el permanente, abono; el
/// contractual, cuotas. El CDAT nunca se recoge. Un texto vacío o que no cumple la forma se lee como el defecto (sólo créditos, total o parcial: lo de
/// siempre). Mismo molde que <c>GruposEditablesAlDevolver</c>. (nuevo)
/// </summary>
public static class ProductosAdmitidos
{
    /// <summary>Los modos como se escriben en el texto del parámetro.</summary>
    public const string Total = "Total";

    /// <inheritdoc cref="Total"/>
    public const string Parcial = "Parcial";

    /// <inheritdoc cref="Total"/>
    public const string Cuotas = "Cuotas";

    /// <inheritdoc cref="Total"/>
    public const string Abono = "Abono";

    /// <summary>Los productos en el orden en que se muestran.</summary>
    public static IReadOnlyList<ProductoDeRecogida> Productos { get; } =
    [
        ProductoDeRecogida.Creditos, ProductoDeRecogida.Aportes, ProductoDeRecogida.AhorroALaVista, ProductoDeRecogida.AhorroPermanente,
        ProductoDeRecogida.AhorroContractual, ProductoDeRecogida.FacturasComerciales,
    ];

    /// <summary>Los modos que admite cada producto, en el orden en que se escriben.</summary>
    public static IReadOnlyDictionary<ProductoDeRecogida, IReadOnlyList<PayoffMode>> ModosPosibles { get; } =
        new Dictionary<ProductoDeRecogida, IReadOnlyList<PayoffMode>>
        {
            [ProductoDeRecogida.Creditos] = [PayoffMode.Total, PayoffMode.Partial],
            [ProductoDeRecogida.Aportes] = [PayoffMode.Installments, PayoffMode.Deposit],
            [ProductoDeRecogida.AhorroALaVista] = [PayoffMode.Deposit],
            [ProductoDeRecogida.AhorroPermanente] = [PayoffMode.Deposit],
            [ProductoDeRecogida.AhorroContractual] = [PayoffMode.Installments],
            [ProductoDeRecogida.FacturasComerciales] = [PayoffMode.Total, PayoffMode.Partial],
        };

    /// <summary>El defecto: sólo créditos, total o parcial (lo de siempre).</summary>
    public static AdmisionDeRecogida PorDefecto { get; } =
        new(new Dictionary<ProductoDeRecogida, IReadOnlySet<PayoffMode>>
        {
            [ProductoDeRecogida.Creditos] = new HashSet<PayoffMode> { PayoffMode.Total, PayoffMode.Partial },
        });

    /// <summary>El defecto como texto del parámetro: <c>Creditos:Total,Parcial</c>.</summary>
    public static string Defecto { get; } = Escribir(PorDefecto);

    /// <summary>
    /// La forma del texto: uno o más <c>Producto:modo,modo</c> separados por <c>;</c>, cada producto sólo con sus modos posibles, con espacios
    /// opcionales alrededor de los separadores. El lector no distingue mayúsculas; el patrón sí (la escritura es la de la pantalla).
    /// </summary>
    public static string Patron { get; } = ArmarPatron();

    /// <summary>El nombre de un modo como se escribe en el texto.</summary>
    public static string NombreDelModo(PayoffMode modo) => modo switch
    {
        PayoffMode.Total => Total,
        PayoffMode.Partial => Parcial,
        PayoffMode.Installments => Cuotas,
        PayoffMode.Deposit => Abono,
        _ => throw new ArgumentOutOfRangeException(nameof(modo), modo, "Modo de recogida desconocido."),
    };

    /// <summary>La clase de producto que se guarda en la recogida: créditos, cuentas o facturas.</summary>
    public static PayoffProductKind Clase(ProductoDeRecogida producto) => producto switch
    {
        ProductoDeRecogida.Creditos => PayoffProductKind.Loan,
        ProductoDeRecogida.FacturasComerciales => PayoffProductKind.CommercialReceivable,
        _ => PayoffProductKind.Account,
    };

    /// <summary>
    /// El producto de una cuenta según la naturaleza de su línea y, en captaciones, su clase; nulo si no se recoge nunca (el CDAT, o una línea de
    /// colocación, que no es una cuenta).
    /// </summary>
    public static ProductoDeRecogida? DeCuenta(LineNature naturaleza, DepositClass? clase) => naturaleza switch
    {
        LineNature.Contribution => ProductoDeRecogida.Aportes,
        LineNature.Deposit => clase switch
        {
            DepositClass.Demand => ProductoDeRecogida.AhorroALaVista,
            DepositClass.Permanent => ProductoDeRecogida.AhorroPermanente,
            DepositClass.Contractual => ProductoDeRecogida.AhorroContractual,
            _ => null,
        },
        _ => null,
    };

    /// <summary>Lee el texto del parámetro; vacío o con otra forma, <see cref="PorDefecto"/>.</summary>
    public static AdmisionDeRecogida Leer(string? texto) => IntentarLeer(texto) ?? PorDefecto;

    /// <summary>
    /// Lee el texto del parámetro; nulo si está vacío, si nombra un producto o un modo desconocido o si da a un producto un modo que no admite. Un
    /// producto repetido junta sus modos.
    /// </summary>
    public static AdmisionDeRecogida? IntentarLeer(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var modos = new Dictionary<ProductoDeRecogida, HashSet<PayoffMode>>();
        foreach (var tramo in texto.Split(';', StringSplitOptions.TrimEntries))
        {
            var partes = tramo.Split(':', StringSplitOptions.TrimEntries);
            if (partes.Length != 2 || partes[0].Length == 0 || partes[1].Length == 0) return null;
            var producto = Productos.Cast<ProductoDeRecogida?>()
                .FirstOrDefault(p => string.Equals(p.ToString(), partes[0], StringComparison.OrdinalIgnoreCase));
            if (producto is null) return null;
            if (!modos.TryGetValue(producto.Value, out var delProducto)) modos[producto.Value] = delProducto = [];
            foreach (var nombre in partes[1].Split(',', StringSplitOptions.TrimEntries))
            {
                var modo = ModosPosibles[producto.Value].Cast<PayoffMode?>()
                    .FirstOrDefault(m => string.Equals(NombreDelModo(m!.Value), nombre, StringComparison.OrdinalIgnoreCase));
                if (modo is null) return null;
                delProducto.Add(modo.Value);
            }
        }
        return new AdmisionDeRecogida(modos.ToDictionary(kv => kv.Key, kv => (IReadOnlySet<PayoffMode>)kv.Value));
    }

    /// <summary>La admisión como texto del parámetro, en el orden de <see cref="Productos"/> y de <see cref="ModosPosibles"/>.</summary>
    public static string Escribir(AdmisionDeRecogida admision) =>
        string.Join(";", Productos.Where(admision.Admite)
            .Select(p => $"{p}:{string.Join(",", ModosPosibles[p].Where(m => admision.Admite(p, m)).Select(NombreDelModo))}"));

    private static string ArmarPatron()
    {
        var tramos = Productos.Select(p =>
        {
            var modos = string.Join("|", ModosPosibles[p].Select(NombreDelModo));
            return $@"{p}\s*:\s*({modos})(\s*,\s*({modos}))*";
        });
        var tramo = $"({string.Join("|", tramos)})";
        return $@"^{tramo}(\s*;\s*{tramo})*$";
    }
}

/// <summary>Lo que admite el parámetro <c>Recogida.ProductosAdmitidos</c>: cada producto admitido con sus modos. (nuevo)</summary>
public sealed class AdmisionDeRecogida(IReadOnlyDictionary<ProductoDeRecogida, IReadOnlySet<PayoffMode>> modos)
{
    private static readonly IReadOnlySet<PayoffMode> Ninguno = new HashSet<PayoffMode>();

    /// <summary>Los productos admitidos con sus modos (sólo los que tienen al menos uno).</summary>
    public IReadOnlyDictionary<ProductoDeRecogida, IReadOnlySet<PayoffMode>> Modos { get; } =
        modos.Where(kv => kv.Value.Count > 0).ToDictionary(kv => kv.Key, kv => kv.Value);

    /// <summary>Si el producto se puede recoger de algún modo.</summary>
    public bool Admite(ProductoDeRecogida producto) => Modos.ContainsKey(producto);

    /// <summary>Si el producto se puede recoger de ese modo.</summary>
    public bool Admite(ProductoDeRecogida producto, PayoffMode modo) => ModosDe(producto).Contains(modo);

    /// <summary>Los modos admitidos del producto; vacío si no se admite.</summary>
    public IReadOnlySet<PayoffMode> ModosDe(ProductoDeRecogida producto) => Modos.GetValueOrDefault(producto) ?? Ninguno;
}
